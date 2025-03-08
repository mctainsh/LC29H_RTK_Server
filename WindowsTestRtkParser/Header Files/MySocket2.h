#pragma once
/*
#include <windows.h>
#include <winsock2.h>
#include <ws2tcpip.h>
#include <iostream>

#pragma comment(lib, "Ws2_32.lib")

#define BUFFER_SIZE 1024

// Function declarations
bool InitializeWinsock();
SOCKET CreateSocket();
bool ConnectToServer(SOCKET& ConnectSocket, const char* ipAddress, int port);
bool SendData(SOCKET& ConnectSocket, const char* data);
bool ReceiveData(SOCKET& ConnectSocket, char* buffer, int bufferSize);
void Cleanup(SOCKET& ConnectSocket);
*/