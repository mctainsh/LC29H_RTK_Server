#include "Header Files/MySocket2.h"

/*
bool InitializeWinsock() 
{
    WSADATA wsaData;
    int result = WSAStartup(MAKEWORD(2, 2), &wsaData);
    if (result != 0) {
        std::cerr << "WSAStartup failed with error: " << result << std::endl;
        return false;
    }
    return true;
}

SOCKET CreateSocket() 
{
    SOCKET ConnectSocket = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
    if (ConnectSocket == INVALID_SOCKET) 
	{
        std::cerr << "socket failed with error: " << WSAGetLastError() << std::endl;
        WSACleanup();
    }
    return ConnectSocket;
}

bool ConnectToServer(SOCKET& ConnectSocket, const char* ipAddress, int port) 
{
    struct sockaddr_in serverAddress;
    serverAddress.sin_family = AF_INET;
    serverAddress.sin_port = htons(port);
    inet_pton(AF_INET, ipAddress, &serverAddress.sin_addr);

    int result = connect(ConnectSocket, (struct sockaddr*)&serverAddress, sizeof(serverAddress));
    if (result == SOCKET_ERROR) 
	{
        std::cerr << "connect failed with error: " << WSAGetLastError() << std::endl;
        closesocket(ConnectSocket);
        WSACleanup();
        return false;
    }
    return true;
}

bool SendData(SOCKET& ConnectSocket, const char* data) 
{
    int result = send(ConnectSocket, data, (int)strlen(data), 0);
    if (result == SOCKET_ERROR) 
	{
        std::cerr << "send failed with error: " << WSAGetLastError() << std::endl;
        closesocket(ConnectSocket);
        WSACleanup();
        return false;
    }
    return true;
}

bool ReceiveData(SOCKET& ConnectSocket, char* buffer, int bufferSize) 
{
    int result = recv(ConnectSocket, buffer, bufferSize, 0);
    if (result > 0) {
        std::cout << "Received response from server: " << std::string(buffer, result) << std::endl;
        return true;
    } else if (result == 0) {
        std::cout << "Connection closed by server" << std::endl;
    } else {
        std::cerr << "recv failed with error: " << WSAGetLastError() << std::endl;
    }
    return false;
}

void Cleanup(SOCKET& ConnectSocket) {
    closesocket(ConnectSocket);
    WSACleanup();
}
*/
// MYSOCKET_H


