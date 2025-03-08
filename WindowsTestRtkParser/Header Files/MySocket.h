#pragma once

#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <iostream>

#pragma comment(lib, "Ws2_32.lib")

#define BUFFER_SIZE 1024

bool InitializeWinsock()
{
	// Initialize Winsock
	WSADATA wsaData;
	int result = WSAStartup(MAKEWORD(2, 2), &wsaData);
	if (result != 0)
	{
		std::cerr << "WSAStartup failed with error: " << result << std::endl;
		return false;
	}
	return true;
}

SOCKET CreateSocket()
{
	SOCKET ConnectSocket = INVALID_SOCKET;
	ConnectSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
	if (ConnectSocket == INVALID_SOCKET)
	{
		std::cerr << "socket failed with error: " << WSAGetLastError() << std::endl;
		WSACleanup();
	}
	return ConnectSocket;
}

void Test()
{

	if( !InitializeWinsock() )
		return;

	// Create a socket
	SOCKET ConnectSocket = CreateSocket();
	if (ConnectSocket == INVALID_SOCKET)
		return;

	// Setup the server address structure
	struct sockaddr_in serverAddress;
	serverAddress.sin_family = AF_INET;
	serverAddress.sin_port = htons(80);
	inet_pton(AF_INET, "115.70.239.2", &serverAddress.sin_addr);

	// Connect to the server
	int result = connect(ConnectSocket, (struct sockaddr*)&serverAddress, sizeof(serverAddress));
	if (result == SOCKET_ERROR) 
	{
		std::cerr << "connect failed with error: " << WSAGetLastError() << std::endl;
		closesocket(ConnectSocket);
		WSACleanup();
		return;
	}

	std::cout << "Connected to server at server:2170" << std::endl;

	// Send some text to the server
	const char* sendbuf = "Hello, Server!";
	result = send(ConnectSocket, sendbuf, (int)strlen(sendbuf), 0);
	if (result == SOCKET_ERROR) 
	{
		std::cerr << "send failed with error: " << WSAGetLastError() << std::endl;
		closesocket(ConnectSocket);
		WSACleanup();
		return;
	}

	std::cout << "Message sent to server: " << sendbuf << std::endl;

	// Listen for response from server
	while (true)
	{
		char recvbuf[BUFFER_SIZE];
		int recvbuflen = BUFFER_SIZE;
		result = recv(ConnectSocket, recvbuf, recvbuflen, 0);
		if (result > 0)
		{
			std::cout << "Received response from server: " << std::string(recvbuf, result) << std::endl;
		}
		else if (result == 0)
		{
			std::cout << "Connection closed by server" << std::endl;
		}
		else
		{
			std::cerr << "recv failed with error: " << WSAGetLastError() << std::endl;
		}
	}

	// Clean up and shutdown
	closesocket(ConnectSocket);
	WSACleanup();

	return;
}

