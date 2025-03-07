#pragma once
#include <string>
#include <vector>
#include "HandyString.h"

#define LOCATION_SET_SIZE 60


////////////////////////////////////////////////////////////////////////////////
// Build an average for the location
// Average is built up using a total and standard deviation calculation
class LocationAverage
{
private:
	// Large totals
	double _dLngOrg;
	double _dLatOrg;
	double _dZOrg;
	double _dLngTotal = 0;
	double _dLatTotal = 0;
	double _dZTotal = 0;
	unsigned int _count = 0;

	// Set totals
	double _dLats[LOCATION_SET_SIZE];
	double _dLngs[LOCATION_SET_SIZE];
	double _dZs[LOCATION_SET_SIZE];
	unsigned int _setIndex = 0;

	// True if all well
	bool _gpsConnected = false;

public:
	///////////////////////////////////////////////////////////////////////////
	// Extract location for summing totals
	//		$GNGGA, 020816.00,2734.21017577,S,15305.98006651,E,4,34,0.6,34.9570,M,41.1718,M, 1.0,0*4A
	//		$GNGGA,232306.00,,,,,0,00,9999.0,,,,,,*4E
	std::string ProcessGGALocation(const std::string& line)
	{
		std::string result;
		std::vector<std::string> parts = Split(line, ",");
		//std::vector<std::string> parts = Split("$GNGGA,232306.00,,,,,0,00,9999.0,,,,,,", ",");
		//std::vector<std::string> parts = Split("$GNGGA, 020816.00,2734.21017577,S,15305.98006651,E,4,34,0.6,34.9570,M,41.1718,M, 1.0,0", ",");

		if (parts.size() < 14)
		{
			LogX("\tPacket length too short %d %s\r\n", parts.size(), line);
			return "Packt too short";
		}

		// Read time
		std::string time = parts.at(1);
		if (time.length() > 7)
		{
			time.insert(4, ":");
			time.insert(2, ":");
			time = time.substr(0, 8);
		}
		//_display.SetTime(time);

		// Read GPS Quality
		std::string quality = parts.at(6);
		int nQuality = 0;
		if (quality.length() > 0)
		{
			nQuality = std::stoi(quality);
			//_display.SetFixMode(nQuality);
		}

		// Location
		double lat = ParseLatLong(parts.at(2), 2, parts.at(3) == "S");
		double lng = ParseLatLong(parts.at(4), 3, parts.at(3) == "E");

		// Height
		double height = 0;
		if (!IsValidDouble(parts.at(9).c_str(), &height))
			height = -1;

		//_display.SetPosition(lng, lat, height);

		// Satellite count
		std::string satellites = parts.at(7);
		int sat = 0;
		if (satellites.length() > 0)
		{
			sat = std::stoi(satellites);
			//_display.SetSatellites(sat);
		}

		// Skip if we have not data
		if (lng == 0.0 || lat == 0.0 || nQuality < 1)
		{
			LogX("No location data in %s", line.c_str());
			return "No location";
		}

		_gpsConnected = true;

        result = "H:" + std::to_string(height) + " #" + satellites + " Q:" + quality;

		// Build the set totals
		if (_setIndex < LOCATION_SET_SIZE)
		{
			_dLngs[_setIndex] = lng;
			_dLats[_setIndex] = lat;
			_dZs[_setIndex] = height;
			_setIndex++;
			return result;
		}

		// Make the mean and standard deviations
		double dLngMean = 0;
		double dLatMean = 0;
		double dZMean = 0;
		LogMeanAndStandardDeviations(dLngMean, dLatMean, dZMean);

		// Start the process
		if (_count == 0)
		{
			_dLngOrg = dLngMean;
			_dLatOrg = dLatMean;
			_dZOrg = dZMean;
		}
		else
		{
			_dLngTotal += (dLngMean - _dLngOrg);
			_dLatTotal += (dLatMean - _dLatOrg);
			_dZTotal += (dZMean - _dZOrg);
		}
		_count++;

		//_gpsSender.SendHttpData(lat, lng, height, sat, nQuality);
		return result;
	}

	///////////////////////////////////////////////////////////////////////////
	// Log the mean and standard deviations
	void LogMeanAndStandardDeviations(double& dLngMean, double& dLatMean, double& dZMean)
	{
		for (int i = 0; i < LOCATION_SET_SIZE; i++)
		{
			dLngMean += _dLngs[i];
			dLatMean += _dLats[i];
			dZMean += _dZs[i];
		}
		dLngMean /= LOCATION_SET_SIZE;
		dLatMean /= LOCATION_SET_SIZE;
		dZMean /= LOCATION_SET_SIZE;

		// Calculate the standard deviation
		double dLngDev = 0;
		double dLatDev = 0;
		double dZDev = 0;
		for (int i = 0; i < LOCATION_SET_SIZE; i++)
		{
			dLngDev += (_dLngs[i] - dLngMean) * (_dLngs[i] - dLngMean);
			dLatDev += (_dLats[i] - dLatMean) * (_dLats[i] - dLatMean);
			dZDev += (_dZs[i] - dZMean) * (_dZs[i] - dZMean);
		}
		dLngDev = sqrt(dLngDev / LOCATION_SET_SIZE);
		dLatDev = sqrt(dLatDev / LOCATION_SET_SIZE);
		dZDev = sqrt(dZDev / LOCATION_SET_SIZE);

		LogX("Location %d : %f %f %.4f : %lf %lf %lf", _count, dLatMean, dLngMean, dZMean, dLatDev * 1000.0, dLngDev * 1000.0, dZDev);
		_setIndex = 0;
	}

	///////////////////////////////////////////////////////////////////////////
	// Log the mean locations
	// Called after the program finishes
	void LogMeanLocations() const
	{
		if (_count == 0)
			return;
		LogX("Location Mean %d", _count);
		LogX("\tLatitude  %lf ", _dLatOrg + (_dLatTotal / _count));
		LogX("\tLongitude %lf ", _dLngOrg + _dLngTotal / _count);
		LogX("\tHeight    %lfm ", _dZOrg + (_dZTotal / _count));
	}


	///////////////////////////////////////////////////////////////////////////
	// Parse the longitude or latitude from
	//		Latitude   = 2734.21017577,S,
	//		Longitude = 15305.98006651,E,
	double ParseLatLong(const std::string& text, int degreeDigits, bool isNegative)
	{
		if (text.length() < degreeDigits)
			return 0.0;
		std::string degree = text.substr(0, degreeDigits);
		std::string minutes = text.substr(degreeDigits);
		double value = std::stod(degree) + (std::stod(minutes) / 60.0);
		return isNegative ? value * -1 : value;
	}
};

