// WindowsTestRtkParser.cpp : This file contains the 'main' function. Program execution begins and ends there.
// Run program: Ctrl + F5 or Debug > Start Without Debugging menu
// Debug program: F5 or Debug > Start Debugging menu

// Tips for Getting Started: 
//   1. Use the Solution Explorer window to add/manage files
//   2. Use the Team Explorer window to connect to source control
//   3. Use the Output window to see build output and other messages
//   4. Use the Error List window to view errors
//   5. Go to Project > Add New Item to create new code files, or Project > Add Existing Item to add existing code files to the project
//   6. In the future, to open this project again, go to File > Open > Project and select the .sln file

#include <iostream>
#include <windows.h>
#include "Header Files/SHLog.h"
#include "Header Files/GpsParser.h"
#include <conio.h>

HANDLE g_hSerial;

int main()
{
	SHLog::Log("Hello from WindowsTestRtkParser");
	InitializeLogFile();

	// Open a serial port to start listening for RS232 data at 115200 baud
	g_hSerial = CreateFile("COM8",
		GENERIC_READ | GENERIC_WRITE,
		0,
		0,
		OPEN_EXISTING,
		FILE_ATTRIBUTE_NORMAL,
		0);

	if (g_hSerial == INVALID_HANDLE_VALUE)
	{
		SHLog::Log("Error opening serial port");
		return 1;
	}

	DCB dcbSerialParams = { 0 };
	dcbSerialParams.DCBlength = sizeof(dcbSerialParams);

	if (!GetCommState(g_hSerial, &dcbSerialParams))
	{
		SHLog::Log("Error getting serial port state");
		CloseHandle(g_hSerial);
		return 1;
	}

	dcbSerialParams.BaudRate = CBR_115200;
	dcbSerialParams.ByteSize = 8;
	dcbSerialParams.StopBits = ONESTOPBIT;
	dcbSerialParams.Parity = NOPARITY;

	if (!SetCommState(g_hSerial, &dcbSerialParams))
	{
		SHLog::Log("Error setting serial port state");
		CloseHandle(g_hSerial);
		return 1;
	}

	// Send a message to the GPS unit to start sending data
	DWORD dwBytesWritten;

	GpsParser _parser;

	const int BUFF_SIZE = 1024;
	unsigned char szBuff[BUFF_SIZE + 1];
	DWORD dwBytesRead = 0;
	long totalBytes = 0;

	while (true)
	{
		// Check if a key has been pressed
		if (_kbhit())
		{
			// If the key pressed is 'q', break the loop
			if (_getch() == 'q')
			{
				break;
			}
		}

		// Read the data and parse it
		if (!ReadFile(g_hSerial, szBuff, BUFF_SIZE, &dwBytesRead, NULL))
		{
			SHLog::Log("Error reading from serial port");
			CloseHandle(g_hSerial);
			return 1;
		}
		if (dwBytesRead < 1)
			continue;
		totalBytes += dwBytesRead;
		szBuff[dwBytesRead] = NULL;

		// Check for almost full buffer
		//if (dwBytesRead > BUFF_SIZE - 10)
		//	LogX("WARNING L GPS - Serial Buffer overflow %d", dwBytesRead);

		// Limit the number of bytes we read
		//available = __min(available, MAX_BUFF);

		// Read the available bytes from stream
		_parser.ProcessStream(szBuff, dwBytesRead);

		// Log total bytes
		printf("\rTotal bytes read %d", totalBytes);
	}

	// Close the serial port
	CloseHandle(g_hSerial);

	// Log the totals
	LogX("=====================================");
	LogX("Total Read Errors : %d", _parser.GetReadErrorCount());
	auto& totals = _parser.GetMsgTypeTotals();
	for (auto& total : totals)
		LogX("Total %d : %d", total.first, total.second);

	// Log the mean locations
	_parser.LogMeanLocations();

	// We are done happy
	return 0;
}

