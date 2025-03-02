#pragma once

#include <vector>
#include <string>


///////////////////////////////////////////////////////////////////////////////
// Holds a collection of commands
// Verifies when a response matches the current queue item,
//	.. the next item is sent
//  .. command responses look like $command,version,response: OK*24
class GpsCommandQueue
{
private:
	std::vector<std::string> _strings;
	int _timeSent = 0;
	std::string _deviceType;					// Device type like UM982
	std::string _deviceFirmware = "UNKNOWN";	// Firmware version
	std::string _deviceSerial = "UNKNOWN";		// Serial number

public:
	GpsCommandQueue()
	{
	}

	inline const std::string& GetDeviceType() const { return _deviceType; }
	inline const std::string& GetDeviceFirmware() const { return _deviceFirmware; }
	inline const std::string& GetDeviceSerial() const { return _deviceSerial; }

	///////////////////////////////////////////////////////////////////////////
	/// @brief Process a line of text from the GPS unit checking got version information
	///	$PQTMVERNO,LC29HDANR11A02S_RSA,2024/01/31,15:17:36*2E
	inline void CheckForVersion(const std::string& str)
	{
		if (!StartsWith(str, "#VERSION"))
			return;

		auto sections = Split(str, ";");
		if (sections.size() < 1)
		{
			LogX("DANGER 301 : Unknown sections '%s' Detected", str.c_str());
			return;
		}

		auto parts = Split(sections[1], ",");
		if (parts.size() < 5)
		{
			LogX("DANGER 302 : Unknown split '%s' Detected", str.c_str());
			return;
		}
		_deviceType = parts[0];
		_deviceFirmware = parts[1];
		auto serialPart = Split(parts[3], "-");
		_deviceSerial = serialPart[0];

		// New documentation for Unicore. The new firmware (Build17548) has 50 Hz and QZSS L6 reception instead of Galileo E6.
		// .. From now on, we install the Build17548 firmware on all new UM980 receivers. So we have a new advantage - you can
		// .. enable L6 instead of E6 by changing SIGNALGOUP from 2 to 10. Another thing is that this is only needed in Japan
		// .. and countries close to it.

		// Setup signal groups
		std::string command = "CONFIG SIGNALGROUP 3 6"; // Assume for UM982)
		if (_deviceType == "UM982")
		{
			LogX("UM982 Detected");
		}
		else if (_deviceType == "UM980")
		{
			LogX("UM980 Detected");
			command = "CONFIG SIGNALGROUP 2"; // (for UM980)
		}
		else
		{
			LogX("DANGER 303 Unknown Device '%s' Detected in %s", _deviceType.c_str(), str.c_str());
		}
		_strings.push_back(command);
	}

	///////////////////////////////////////////////////////////////////////////
	inline bool VerifyChecksum(const std::string& str)
	{
		size_t asterisk_pos = str.find_last_of('*');
		if (asterisk_pos == std::string::npos || asterisk_pos + 3 > str.size())
		{
			LogX("ERROR : GPS Checksum error in %s", str.c_str());
			return false;
		}

		// Extract the data and the checksum
		std::string data = str.substr(1, asterisk_pos - 1);
		std::string provided_checksum_str = str.substr(asterisk_pos + 1, 2);

		// Convert the provided checksum from hex to an integer
		unsigned int provided_checksum;
		std::stringstream ss;
		ss << std::hex << provided_checksum_str;
		ss >> provided_checksum;

		// Calculate the checksum of the data
		unsigned char calculated_checksum = CalculateChecksum(data);

		return calculated_checksum == static_cast<unsigned char>(provided_checksum);
	}

	// ////////////////////////////////////////////////////////////////////////
	// Check if the first item in the list matches the given string
	bool IsCommandResponse(const std::string& str)
	{
		if (_strings.empty())
			return false; // List is empty, no match

		// TODO : Skip GPS here


		// Verify checksum
		if (!VerifyChecksum(str))
		{
			LogX("ERROR : GPS Checksum error in %s", str.c_str());
			return false;
		}

		// Skip GPS here
		if (StartsWith(str, "$G"))
			return false;

		std::string match = "$" + _strings.front();

		// Good config is "$PQTMCFGSVIN,OK*70"
		if (StartsWith(str, "$PQTMCFGSVIN,OK"))
		{
			LogX("GPS Configured : %s", str.c_str());
		}
		else if (StartsWith(str, "$PQTMCFGSVIN"))
		{
			LogX("ERROR GPS NOT Configured : %s", str.c_str());
		}
		// Check for PAIR commands ($PAIR436,1*26 is retrurned as $PAIR001,436,0*3A)
		else if (StartsWith(str, "$PAIR001"))
		{
			if (str.length() < 15)
			{
				LogX("ERROR : PAIR001 too short %s", str.c_str());
				return false;
			}
			if (match.length() < 10)
			{
				LogX("ERROR : %s too short for PAIR", str.c_str());
				return false;
			}
			if (match[5] != str[9] || match[6] != str[10] || match[7] != str[11])
			{
				LogX("ERROR : PAIR001 mismatch %s and %s", str.c_str(), match.c_str());
				return false;
			}
		}
		else
		{
			// Check it start correctly
			if (str.find(match) != 0)
				return false;
		}

		// Clear the sent command
		_strings.erase(_strings.begin());

		// Process according to command type
		if (match.find("$PQTMVERNO") == 0)
			CheckForVersion(str);

		// Log the complete send
		if (_strings.empty())
			LogX("GPS Startup Commands Complete");

		// Send next command
		SendTopCommand();
		return true;
	}

	///////////////////////////////////////////////////////////////////////////
	// Start the initialise process by filling the queue
	void StartInitialiseProcess()
	{
		// Load the commands
		LogX("GPS Queue StartInitialiseProcess");
		_strings.clear();

		// Setup RTCM V3
		// Used to determine device type
		_strings.push_back("PQTMVERNO");

		// The Survey-in mode (<Mode> = 1) determines the receiver’s position by building a weighted mean of all
		// valid 3D positioning solutions.You can set values of <MinDur> and <3D_AccLimit> to define the
		// minimum observation time and 3D position standard deviation used for the position estimation.
		_strings.push_back("PQTMCFGSVIN,W,1,43200,0,0,0,0");

		// The $PAIR432 command can enable / disable MSM4 / MSM7(1074, 1077, 1084, 1087, 1094,
		// 1097, 1114, 1117, 1124 and 1127) messages if the corresponding constellation is enabled.
		_strings.push_back("PAIR432,1");

		// The $PAIR434 command can enable / disable Stationary RTK Reference Station ARP(1005) message.
		_strings.push_back("PAIR434,1");

		// The $PAIR436 command can enable / disable ephemeris(1019, 1020, 1042, 1044 and 1046)
		// messages if the corresponding constellation is enabled.
		_strings.push_back("PAIR436,1");

		//_strings.push_back("MODE BASE TIME 60 5"); // Set base mode with 60 second startup and 5m optimized save error
		//		_strings.push_back("RTCM1005 30"); // Base station antenna reference point (ARP) coordinates
		//		_strings.push_back("RTCM1033 30"); // Receiver and antenna description
		//		_strings.push_back("RTCM1077 1");  // GPS MSM7. The type 7 Multiple Signal Message format for the USA’s GPS system, popular.
		//		_strings.push_back("RTCM1087 1");  // GLONASS MSM7. The type 7 Multiple Signal Message format for the Russian GLONASS system.
		//		_strings.push_back("RTCM1097 1");  // Galileo MSM7. The type 7 Multiple Signal Message format for Europe’s Galileo system.
		//		_strings.push_back("RTCM1117 1");  // QZSS MSM7. The type 7 Multiple Signal Message format for Japan’s QZSS system.
		//		_strings.push_back("RTCM1127 1");  // BeiDou MSM7. The type 7 Multiple Signal Message format for China’s BeiDou system.
		//		_strings.push_back("RTCM1137 1");  // NavIC MSM7. The type 7 Multiple Signal Message format for India’s NavIC system.

		SendTopCommand();
	}

	//////////////////////////////////////////////////////////////////////////
	// Check queue for timeouts
	void CheckForTimeouts()
	{
		if (_strings.empty())
			return;

		if ((millis() - _timeSent) > 8000)
		{
			LogX("E940 - Timeout on %s", _strings.front().c_str());
			SendTopCommand();
		}
	}

	///////////////////////////////////////////////////////////////////////////////////////
	// Send the command and check set timeouts
	void SendTopCommand()
	{
		if (_strings.empty())
			return;
		LogX("GPS -> %s", _strings.front().c_str());
		SendCommand(_strings.front().c_str());
		//	Serial1.println(_strings.front().c_str());
		_timeSent = millis();
	}
};
