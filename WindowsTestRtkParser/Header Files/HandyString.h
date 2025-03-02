#pragma once

#include <string>
#include <iostream>
#include <cstdarg>
#include <cstdio>
#include <chrono>
#include <iomanip>
#include <sstream>
#include <fstream> // Add this line to include the fstream header

// Global file stream for logging
std::ofstream _logFile;

/// <summary>
/// Function to get the current timestamp as a string suitable for filenames
/// </summary>
std::string GetCurrentTimestampForFilename()
{
	auto now = std::chrono::system_clock::now();
	auto now_time_t = std::chrono::system_clock::to_time_t(now);
	auto now_ms = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()) % 1000;
	std::tm now_tm;
	localtime_s(&now_tm, &now_time_t);

	std::ostringstream oss;
	oss << std::put_time(&now_tm, "%Y-%m-%d_%H-%M-%S");
	oss << '.' << std::setfill('0') << std::setw(3) << now_ms.count();
	return oss.str();
}


/// <summary>
/// Function to initialize the log file
/// </summary>
void InitializeLogFile()
{
	std::string filename = "x64\\WinTestRtk " + GetCurrentTimestampForFilename() + ".txt";
	_logFile.open(filename, std::ios::out | std::ios::app);
	if (!_logFile.is_open())
	{
		std::cerr << "Error opening log file: " << filename << std::endl;
	}
}



//template<typename... Args>
//std::string StringPrintf(const std::string& format, Args... args);

bool StartsWith(const std::string& fullString, const std::string& startString)
{
	if (startString.length() > fullString.length())
		return false;
	return std::equal(startString.begin(), startString.end(), fullString.begin());
}

bool StartsWith(const char* szA, const char* szB)
{
	while (*szA && *szB)
	{
		if (*szA != *szB)
			return false;
		szA++;
		szB++;
	}
	return *szB == '\0';
}


//std::string ToThousands(int number);

/// @brief Convert a array of bytes to string of hex numbers
std::string HexDump(const unsigned char* data, int len)
{
	char szTemp[5];
	std::string hex;
	for (int n = 0; n < len; n++)
	{
		snprintf(szTemp, sizeof(szTemp), "%02x ", data[n]);
		hex += szTemp;
	}
	return hex;
}

///////////////////////////////////////////////////////////////////////////
/// @brief Convert a array of bytes to string of hex numbers and ASCII characters
/// Format: "00 01 02 03 04 05 06 07-08 09 0a 0b 0c 0d 0e 0f 0123456789abcdef" 
std::string HexAsciDump(const unsigned char* data, int len)
{
	if (len == 0)
		return "";
	std::string lines;
	const int SIZE = 64;
	char szText[SIZE + 1];
	for (int n = 0; n < len; n++)
	{
		auto index = n % 16;
		if (index == 0)
		{
			szText[SIZE - 1] = '\0';
			if (n > 0)
				lines += (std::string(szText) + "\r\n");

			// Fill the szText with spaces
			memset(szText, ' ', SIZE);
		}

		// Add the hex value to szText as position 3 * n
		auto offset = 3 * index;
		snprintf(szText + offset, 3, "%02x", data[n]);
		szText[offset + 2] = ' ';
		szText[3 * 16 + 1 + index] = data[n] < 0x20 ? 0xfa : data[n];
	}
	szText[SIZE - 1] = '\0';
	lines += std::string(szText);
	return lines;
}


///////////////////////////////////////////////////////////////////////////
// Split the string
std::vector<std::string> Split(const std::string& s, const std::string delimiter)
{
	size_t pos_start = 0;
	size_t pos_end;
	size_t delim_len = delimiter.length();
	std::string token;
	std::vector<std::string> res;

	while ((pos_end = s.find(delimiter, pos_start)) != std::string::npos)
	{
		token = s.substr(pos_start, pos_end - pos_start);
		pos_start = pos_end + delim_len;
		res.push_back(token);
	}

	res.push_back(s.substr(pos_start));
	return res;
}


// 
//void FormatNumber(int number, int width, std::string& result);
//bool EndsWith(const std::string& fullString, const std::string& ending);
//bool IsValidHex(const std::string& str);


bool IsValidDouble(const char* str, double* pVal)
{
	if (str == NULL || *str == '\0')
		return false;

	char* endptr;
	*pVal = strtod(str, &endptr);

	// Check if the entire string was converted
	if (*endptr != '\0')
		return false;

	// Check for special cases like NaN and infinity
	if (*pVal == 0.0 && endptr == str)
		return false;

	return true;
}

//const char* WifiStatus(wl_status_t status);
//std::string ReplaceNewlineWithTab(const std::string& input);
//std::string Replace(const std::string &input, const std::string &search, const std::string &replace);
//void RemoveLastLfCr(std::string &str);
//void ReplaceCrLfEncode(std::string &str);
//


/// <summary>
/// Function to get the current timestamp as a string
/// </summary>
std::string GetCurrentTimestamp()
{
	auto now = std::chrono::system_clock::now();
	auto now_time_t = std::chrono::system_clock::to_time_t(now);
	auto now_ms = std::chrono::duration_cast<std::chrono::milliseconds>(now.time_since_epoch()) % 1000;
	std::tm now_tm;
	localtime_s(&now_tm, &now_time_t);

	std::ostringstream oss;
	oss << std::put_time(&now_tm, "%Y-%m-%d %H:%M:%S");
	oss << '.' << std::setfill('0') << std::setw(3) << now_ms.count();
	return oss.str();
}

// Function to get the milliseconds since the program started
uint64_t millis()
{
	SYSTEMTIME st;
	FILETIME ft;
	ULARGE_INTEGER uli;

	// Get the current system time
	GetSystemTime(&st);
	SystemTimeToFileTime(&st, &ft);

	// Convert FILETIME to ULARGE_INTEGER
	uli.LowPart = ft.dwLowDateTime;
	uli.HighPart = ft.dwHighDateTime;

	// Convert to milliseconds
	return (uli.QuadPart / 10000ULL);
}


/// <summary>
/// Log to screen and file with timestamp
/// </summary>
void LogX(const char* format, ...)
{
	std::string timestamp = GetCurrentTimestamp();
	std::cout << "[" << timestamp << "] ";

	if (_logFile.is_open())
	{
		_logFile << "[" << timestamp << "] ";
	}

	va_list args;
	va_start(args, format);
	vprintf(format, args);
	if (_logFile.is_open())
	{
		char buffer[10240];
		vsnprintf(buffer, sizeof(buffer), format, args);
		_logFile << buffer;
	}
	va_end(args);

	std::cout << std::endl;
	if (_logFile.is_open())
	{
		_logFile << std::endl;
	}
}
/// <summary>
/// Log to screen and file with timestamp
/// </summary>
void Log(const char* format, ...)
{
	va_list args;
	va_start(args, format);
	vprintf(format, args);
	if (_logFile.is_open())
	{
		char buffer[10240];
		vsnprintf(buffer, sizeof(buffer), format, args);
		_logFile << buffer;
	}
	va_end(args);
}

extern HANDLE g_hSerial;

///////////////////////////////////////////////////////////////////////////////////////
// Get the GPS checksum
inline static unsigned char CalculateChecksum(const std::string& data)
{
	unsigned char checksum = 0;
	for (char c : data)
		checksum ^= c;
	return checksum;
}

//////////////////////////////////////////////////////////////////////////
// Send a command to the GPS unit
//	Builds 
//		PQTMVERNO
// into
//		$PQTMVERNO*58\r\n
// $PAIR432,1*22
void SendCommand(const char* command)
{
	// Make checksum
	unsigned char checksum = CalculateChecksum(command);

	// Final string starting with $ + command + * + checksum in hex + \r\n
	std::stringstream ss;
	ss << "$" << command << "*" << std::hex << std::uppercase << (int)checksum << "\r\n";
	std::string finalCommand = ss.str();

	DWORD dwBytesWritten;
	if (!WriteFile(g_hSerial, finalCommand.c_str(), finalCommand.length(), &dwBytesWritten, NULL))
	{
		SHLog::Log("Error writing to serial port");
		CloseHandle(g_hSerial);
		exit(1);
	}
}
