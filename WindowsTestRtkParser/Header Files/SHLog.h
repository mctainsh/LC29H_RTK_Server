#pragma once
#include <stdio.h>


/// <summary>
/// Simple logging class
/// </summary>
class SHLog
{
public:
	static void Log(const char* message)
	{
		printf("%s\n", message);
	}
};

