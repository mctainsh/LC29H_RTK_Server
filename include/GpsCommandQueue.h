#pragma once

#include <vector>
#include <string>

extern MyDisplay _display;

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
	std::function<void(std::string)> _logToGps; // Log to GPS function

public:
	GpsCommandQueue(std::function<void(std::string)> logFunc)
	{
		_logToGps = logFunc;
	}

	inline const std::string &GetDeviceType() const { return _deviceType; }
	inline const std::string &GetDeviceFirmware() const { return _deviceFirmware; }
	inline const std::string &GetDeviceSerial() const { return _deviceSerial; }

	///////////////////////////////////////////////////////////////////////////
	/// @brief Process a line of text from the GPS unit checking got version information
	///	#VERSION,0,GPS,UNKNOWN,0,0,0,0,0,1261;UM982,R4.10Build11826,HRPT00-S10C-P,2310415000012-LR23A0225104240,ff27289609cf869d,2023/11/24*4d0ec3ba
	inline void CheckForVersion(const std::string &str)
	{
		if (!StartsWith(str, "#VERSION"))
			return;

		auto sections = Split(str, ";");
		if (sections.size() < 1)
		{
			Logf("DANGER 301 : Unknown sections '%s' Detected", str.c_str());
			return;
		}

		auto parts = Split(sections[1], ",");
		if (parts.size() < 5)
		{
			Logf("DANGER 302 : Unknown split '%s' Detected", str.c_str());
			return;
		}
		_deviceType = parts[0];
		_deviceFirmware = parts[1];
		auto serialPart = Split(parts[3], "-");
		_deviceSerial = serialPart[0];
		_display.RefreshScreen();

		// New documentation for Unicore. The new firmware (Build17548) has 50 Hz and QZSS L6 reception instead of Galileo E6.
		// .. From now on, we install the Build17548 firmware on all new UM980 receivers. So we have a new advantage - you can
		// .. enable L6 instead of E6 by changing SIGNALGOUP from 2 to 10. Another thing is that this is only needed in Japan
		// .. and countries close to it.

		// Setup signal groups
		std::string command = "CONFIG SIGNALGROUP 3 6"; // Assume for UM982)
		if (_deviceType == "UM982")
		{
			Logln("UM982 Detected");
		}
		else if (_deviceType == "UM980")
		{
			Logln("UM980 Detected");
			command = "CONFIG SIGNALGROUP 2"; // (for UM980)
		}
		else
		{
			Logf("DANGER 303 Unknown Device '%s' Detected in %s", _deviceType.c_str(), str.c_str());
		}
		_strings.push_back(command);
	}

	///////////////////////////////////////////////////////////////////////////////////////
	// Get the GPS checksum
	inline unsigned char CalculateChecksum(const std::string &data)
	{
		unsigned char checksum = 0;
		for (char c : data)
			checksum ^= c;
		return checksum;
	}
	inline bool VerifyChecksum(const std::string &str)
	{
		size_t asterisk_pos = str.find_last_of('*');
		if (asterisk_pos == std::string::npos || asterisk_pos + 3 > str.size())	
			return false; // Invalid format

		// Extract the data and the checksum
		std::string data = str.substr(0, asterisk_pos);
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
	bool IsCommandResponse(const std::string &str)
	{
		if (_strings.empty())
			return false; // List is empty, no match

		std::string match = "$command,";

		// Check it start correctly
		if (str.find(match) != 0)
			return false;

		// Verify checksum
		if (!VerifyChecksum(str))
		{
			Logf("GPS Checksum error in %s", str.c_str());
			return false;
		}

		// Check for a command match
		match += _strings.front();
		match += ",response: OK*";

		if (str.compare(0, match.size(), match) != 0)
			return false;

		// Clear the sent command
		_strings.erase(_strings.begin());

		if (_strings.empty())
		{
			Logf("GPS Startup Commands Complete");
		}

		// Send next command
		SendTopCommand();
		return true;
	}

	///////////////////////////////////////////////////////////////////////////
	// Check if the GPS receiver has reset itself and send all the commands
	// .. reset command looks like "$devicename,COM1*67"
	bool HasDeviceReset(const std::string &str)
	{
		const std::string match = "$devicename,COM";
		if (str.compare(0, match.size(), match) != 0)
			return false;

		_display.UpdateGpsStarts(false, true);
		StartInitialiseProcess();
		return true;
	}

	///////////////////////////////////////////////////////////////////////////
	// Issue RESET command
	void IssueFReset()
	{
		_strings.push_back("FRESET");
		SendTopCommand();
	}

	///////////////////////////////////////////////////////////////////////////
	// Start the initialise process by filling the queue
	void StartInitialiseProcess()
	{
		// Load the commands
		Logf("GPS Queue StartInitialiseProcess");
		_strings.clear();

		// Setup RTCM V3
		_strings.push_back("VERSION"); // Used to determine device type
		//_strings.push_back("MODE BASE TIME 60 5"); // Set base mode with 60 second startup and 5m optimized save error
		_strings.push_back("RTCM1005 30"); // Base station antenna reference point (ARP) coordinates
		_strings.push_back("RTCM1033 30"); // Receiver and antenna description
		_strings.push_back("RTCM1077 1");  // GPS MSM7. The type 7 Multiple Signal Message format for the USA’s GPS system, popular.
		_strings.push_back("RTCM1087 1");  // GLONASS MSM7. The type 7 Multiple Signal Message format for the Russian GLONASS system.
		_strings.push_back("RTCM1097 1");  // Galileo MSM7. The type 7 Multiple Signal Message format for Europe’s Galileo system.
		_strings.push_back("RTCM1117 1");  // QZSS MSM7. The type 7 Multiple Signal Message format for Japan’s QZSS system.
		_strings.push_back("RTCM1127 1");  // BeiDou MSM7. The type 7 Multiple Signal Message format for China’s BeiDou system.
		_strings.push_back("RTCM1137 1");  // NavIC MSM7. The type 7 Multiple Signal Message format for India’s NavIC system.

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
			Logf("E940 - Timeout on %s", _strings.front().c_str());
			SendTopCommand();
		}
	}

	///////////////////////////////////////////////////////////////////////////////////////
	// Send the command and check set timeouts
	void SendTopCommand()
	{
		if (_strings.empty())
			return;
		_logToGps("GPS -> " + _strings.front());
		Serial1.println(_strings.front().c_str());
		_timeSent = millis();
	}
};
