using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;

namespace WinLC29H_Server
{
	public class GpsParser
	{
		private const int GPS_TIMEOUT_MS = 60000;
		private const bool VERBOSE = true;
		private const bool PROCESS_ALL_PACKETS = true;
		private const int MAX_BUFF = 1200;

		private static readonly uint[] tbl_CRC24Q = new uint[]
		{
			0x000000, 0x864CFB, 0x8AD50D, 0x0C99F6, 0x93E6E1, 0x15AA1A, 0x1933EC, 0x9F7F17,
			0xA18139, 0x27CDC2, 0x2B5434, 0xAD18CF, 0x3267D8, 0xB42B23, 0xB8B2D5, 0x3EFE2E,
			0xC54E89, 0x430272, 0x4F9B84, 0xC9D77F, 0x56A868, 0xD0E493, 0xDC7D65, 0x5A319E,
			0x64CFB0, 0xE2834B, 0xEE1ABD, 0x685646, 0xF72951, 0x7165AA, 0x7DFC5C, 0xFBB0A7,
			0x0CD1E9, 0x8A9D12, 0x8604E4, 0x00481F, 0x9F3708, 0x197BF3, 0x15E205, 0x93AEFE,
			0xAD50D0, 0x2B1C2B, 0x2785DD, 0xA1C926, 0x3EB631, 0xB8FACA, 0xB4633C, 0x322FC7,
			0xC99F60, 0x4FD39B, 0x434A6D, 0xC50696, 0x5A7981, 0xDC357A, 0xD0AC8C, 0x56E077,
			0x681E59, 0xEE52A2, 0xE2CB54, 0x6487AF, 0xFBF8B8, 0x7DB443, 0x712DB5, 0xF7614E,
			0x19A3D2, 0x9FEF29, 0x9376DF, 0x153A24, 0x8A4533, 0x0C09C8, 0x00903E, 0x86DCC5,
			0xB822EB, 0x3E6E10, 0x32F7E6, 0xB4BB1D, 0x2BC40A, 0xAD88F1, 0xA11107, 0x275DFC,
			0xDCED5B, 0x5AA1A0, 0x563856, 0xD074AD, 0x4F0BBA, 0xC94741, 0xC5DEB7, 0x43924C,
			0x7D6C62, 0xFB2099, 0xF7B96F, 0x71F594, 0xEE8A83, 0x68C678, 0x645F8E, 0xE21375,
			0x15723B, 0x933EC0, 0x9FA736, 0x19EBCD, 0x8694DA, 0x00D821, 0x0C41D7, 0x8A0D2C,
			0xB4F302, 0x32BFF9, 0x3E260F, 0xB86AF4, 0x2715E3, 0xA15918, 0xADC0EE, 0x2B8C15,
			0xD03CB2, 0x567049, 0x5AE9BF, 0xDCA544, 0x43DA53, 0xC596A8, 0xC90F5E, 0x4F43A5,
			0x71BD8B, 0xF7F170, 0xFB6886, 0x7D247D, 0xE25B6A, 0x641791, 0x688E67, 0xEEC29C,
			0x3347A4, 0xB50B5F, 0xB992A9, 0x3FDE52, 0xA0A145, 0x26EDBE, 0x2A7448, 0xAC38B3,
			0x92C69D, 0x148A66, 0x181390, 0x9E5F6B, 0x01207C, 0x876C87, 0x8BF571, 0x0DB98A,
			0xF6092D, 0x7045D6, 0x7CDC20, 0xFA90DB, 0x65EFCC, 0xE3A337, 0xEF3AC1, 0x69763A,
			0x578814, 0xD1C4EF, 0xDD5D19, 0x5B11E2, 0xC46EF5, 0x42220E, 0x4EBBF8, 0xC8F703,
			0x3F964D, 0xB9DAB6, 0xB54340, 0x330FBB, 0xAC70AC, 0x2A3C57, 0x26A5A1, 0xA0E95A,
			0x9E1774, 0x185B8F, 0x14C279, 0x928E82, 0x0DF195, 0x8BBD6E, 0x872498, 0x016863,
			0xFAD8C4, 0x7C943F, 0x700DC9, 0xF64132, 0x693E25, 0xEF72DE, 0xE3EB28, 0x65A7D3,
			0x5B59FD, 0xDD1506, 0xD18CF0, 0x57C00B, 0xC8BF1C, 0x4EF3E7, 0x426A11, 0xC426EA,
			0x2AE476, 0xACA88D, 0xA0317B, 0x267D80, 0xB90297, 0x3F4E6C, 0x33D79A, 0xB59B61,
			0x8B654F, 0x0D29B4, 0x01B042, 0x87FCB9, 0x1883AE, 0x9ECF55, 0x9256A3, 0x141A58,
			0xEFAAFF, 0x69E604, 0x657FF2, 0xE33309, 0x7C4C1E, 0xFA00E5, 0xF69913, 0x70D5E8,
			0x4E2BC6, 0xC8673D, 0xC4FECB, 0x42B230, 0xDDCD27, 0x5B81DC, 0x57182A, 0xD154D1,
			0x26359F, 0xA07964, 0xACE092, 0x2AAC69, 0xB5D37E, 0x339F85, 0x3F0673, 0xB94A88,
			0x87B4A6, 0x01F85D, 0x0D61AB, 0x8B2D50, 0x145247, 0x921EBC, 0x9E874A, 0x18CBB1,
			0xE37B16, 0x6537ED, 0x69AE1B, 0xEFE2E0, 0x709DF7, 0xF6D10C, 0xFA48FA, 0x7C0401,
			0x42FA2F, 0xC4B6D4, 0xC82F22, 0x4E63D9, 0xD11CCE, 0x575035, 0x5BC9C3, 0xDD8538
		};

		private enum BuildState
		{
			BuildStateNone,
			BuildStateBinary,
			BuildStateAscii
		}

		private DateTime _timeOfLastMessage;
		private byte[] _byteArray = new byte[MAX_BUFF + 1];
		private int _binaryIndex = 0;
		private int _binaryLength = 0;
		private List<string> _logHistory = new List<string>();
		private BuildState _buildState = BuildState.BuildStateNone;
		private byte[] _skippedArray = new byte[MAX_BUFF + 2];
		private int _skippedIndex = 0;
		private Dictionary<int, int> _msgTypeTotals = new Dictionary<int, int>();
		private int _readErrorCount = 0;
		private int _missedBytesDuringError = 0;
		private int _maxBufferSize = 0;
		internal GpsCommandQueue CommandQueue { private set; get; }

		//	private LocationAverage _LocationAverage = new LocationAverage();

		public bool _gpsConnected = false;
		private SerialPort _port;

		internal List<NTRIPServer> NtripCasters { get; } = new List<NTRIPServer>();

		/// <summary>
		/// Constructor
		/// </summary>
		/// <param name="port">Serial port to communicate on</param>
		public GpsParser(SerialPort port)
		{
			// Load NTRIP casters
			for (int index = 0; index < 1000; index++)
			{
				var caster = new NTRIPServer(index);
				if (!caster.LoadSettings())
					break;
				NtripCasters.Add(caster);
			}

			// Add receiver event handler
			_port = port;
			_port.DataReceived += OnSerialData;

			CommandQueue = new GpsCommandQueue(port);

			_timeOfLastMessage = DateTime.Now;

			// Don't initialise. Wait for GPS to timeout that way we won't loos datum
			//_commandQueue.StartInitialiseProcess();
		}

		/// <summary>
		/// Socket summary
		/// </summary>
		override public string ToString()
		{
			const string NL = "\r\n\t";
			string counts = "";
			lock (_msgTypeTotals)
			{
				foreach (var item in _msgTypeTotals)
					counts += $"\t{item.Key} : {item.Value:N0}{NL}";
			}

			return $"GPS {_port.PortName} - {_gpsConnected}{NL}" +
			$"Read - {_readErrorCount:N0}{NL}" +
			$"Max buff {_maxBufferSize}{NL}" +
			$"{counts}";
		}

		//		public void LogMeanLocations() => _LocationAverage.LogMeanLocations();
		public int GetReadErrorCount() => _readErrorCount;
		public int GetMaxBufferSize() => _maxBufferSize;

		/// <summary>
		/// Receove serial data from COM port
		/// </summary>
		private void OnSerialData(object sender, SerialDataReceivedEventArgs e)
		{
			// Read available data from port
			var port = (SerialPort)sender;
			var bytes = port.BytesToRead;
			var data = new byte[bytes];
			port.Read(data, 0, bytes);

			// Process the data
			ProcessStream(data, data.Length);
		}


		bool ProcessStream(byte[] pSourceData, int dataSize)
		{
			var pData = new byte[dataSize];
			Array.Copy(pSourceData, pData, dataSize);

			for (int n = 0; n < dataSize; n++)
			{
				if (ProcessGpsSerialByte(pData[n]))
					continue;

				_buildState = BuildState.BuildStateNone;

				if (VERBOSE)
				{
					Log.Ln($"IN  BUFF {_binaryIndex} : {HexDump(_byteArray, _binaryIndex)}");
					Log.Ln($"IN  DATA {n} : {HexDump(pData, dataSize)}");
				}

				n += 1;

				if (_binaryIndex <= n)
				{
					n = n - _binaryIndex;
				}
				else
				{
					int oldBufferSize = _binaryIndex - 1;
					int remainder = dataSize - n;
					int totalSize = oldBufferSize + remainder;

					if (totalSize > 0)
					{
						var pTempData = new byte[totalSize + 1];
						if (oldBufferSize > 0)
							Array.Copy(_byteArray, 1, pTempData, 0, oldBufferSize);
						if (remainder > 0)
							Array.Copy(pData, n, pTempData, oldBufferSize, remainder);

						pData = pTempData;
						n = -1;
						dataSize = totalSize;
					}
				}
				_binaryIndex = 0;

				if (VERBOSE)
				{
					Log.Ln($"OUT DATA {n} : {HexDump(pData, dataSize)}");
				}
			}
			return true;
		}

		private bool ProcessGpsSerialByte(byte ch)
		{
			switch (_buildState)
			{
				case BuildState.BuildStateNone:
					switch (ch)
					{
						case (byte)'$':
						case (byte)'#':
							DumpSkippedBytes();
							_binaryIndex = 1;
							_byteArray[0] = ch;
							_buildState = BuildState.BuildStateAscii;
							return true;

						case 0xD3:
							DumpSkippedBytes();
							_buildState = BuildState.BuildStateBinary;
							_binaryLength = 0;
							_binaryIndex = 1;
							_byteArray[0] = ch;
							return true;

						default:
							AddToSkipped(ch);
							return true;
					}

				case BuildState.BuildStateBinary:
					return BuildBinary(ch);

				case BuildState.BuildStateAscii:
					return BuildAscii(ch);

				default:
					AddToSkipped(ch);
					Log.Ln($"Unknown state {_buildState}");
					_buildState = BuildState.BuildStateNone;
					return true;
			}
		}

		private void DumpSkippedBytes()
		{
			if (VERBOSE && _skippedIndex > 0)
			{
				Log.Ln($"Skipped {_skippedIndex} : {HexDump(_skippedArray, _skippedIndex)}");
				_skippedIndex = 0;
			}
		}

		private void AddToSkipped(byte ch)
		{
			if (_skippedIndex >= MAX_BUFF)
			{
				Log.Ln("Skip buffer overflowed");
				_skippedIndex = 0;
			}
			_skippedArray[_skippedIndex++] = ch;
		}

		private bool BuildBinary(byte ch)
		{
			_byteArray[_binaryIndex++] = ch;

			if (_binaryIndex < 12)
				return true;

			if (_binaryLength == 0 && _binaryIndex >= 4)
			{
				uint lengthPrefix = GetUInt(8, 14 - 8);
				if (lengthPrefix != 0)
				{
					Log.Ln($"Binary length prefix too big {_byteArray[0]:X2} {_byteArray[1]:X2}");
					return false;
				}
				_binaryLength = (int)(GetUInt(14, 10) + 6);
				if (_binaryLength == 0 || _binaryLength >= MAX_BUFF)
				{
					Log.Ln($"Binary length too big {_binaryLength}");
					return false;
				}
				return true;
			}
			if (_binaryIndex >= MAX_BUFF)
			{
				Log.Ln($"Buffer overflow {_binaryIndex}");
				return false;
			}

			if (_binaryIndex >= _binaryLength)
			{
				uint parity = GetUInt((_binaryLength - 3) * 8, 24);
				uint calculated = RtkCrc24();
				uint type = GetUInt(24, 12);
				if (parity != calculated)
				{
					Log.Ln($"Checksum {type} ({parity:X6} != {calculated:X6}) [{_binaryIndex}] {HexDump(_byteArray, _binaryIndex)}");
					return false;
				}

				_gpsConnected = true;
				_timeOfLastMessage = DateTime.Now;

				if (_missedBytesDuringError > 0)
				{
					_readErrorCount++;
					Log.Ln($" >> E: {_readErrorCount} - Skipped {_missedBytesDuringError}");
					_missedBytesDuringError = 0;
				}

				// Update the totals counts
				lock (_msgTypeTotals)
				{
					if (_msgTypeTotals.ContainsKey((int)type))
						_msgTypeTotals[(int)type]++;
					else
						_msgTypeTotals.Add((int)type, 1);
				}
				//Log.Ln($"GOOD {type}[{_binaryIndex}]");
				Console.Write($"\r{type}[{_binaryIndex}]    \r");

				// Send to NTRIP casters (Actually just queue)
				var sendData = new byte[_binaryLength];
				Array.Copy(_byteArray, sendData, _binaryLength);
				foreach (var _caster in NtripCasters)
					_caster.Send(sendData);

				_buildState = BuildState.BuildStateNone;
			}
			return true;
		}

		/// <summary>
		/// Process the ASCII data
		/// </summary>
		private bool BuildAscii(byte ch)
		{
			if (ch == '\r')
				return true;

			if (ch == '\n')
			{
				_byteArray[_binaryIndex] = 0;
				ProcessLine(Encoding.ASCII.GetString(_byteArray, 0, _binaryIndex));
				_buildState = BuildState.BuildStateNone;
				return true;
			}

			if (_binaryIndex > 254)
			{
				Log.Ln($"ASCII Overflowing {HexAsciDump(_byteArray, _binaryIndex)}");
				_buildState = BuildState.BuildStateNone;
				return false;
			}

			_byteArray[_binaryIndex++] = ch;

			if (ch < 32 || ch > 126)
			{
				Log.Ln($"Non-ASCII {HexDump(_byteArray, _binaryIndex)}");
				_buildState = BuildState.BuildStateNone;
				return false;
			}
			return true;
		}

		private void ProcessLine(string line)
		{
			if (line.Length < 1)
			{
				Log.Ln("W700 - GPS ASCII Too short");
				return;
			}

			//if (line.StartsWith("$GNGGA"))
			//{
			//				Log.Ln(_LocationAverage.ProcessGGALocation(line));
			//}
			//else
			//{
			Log.Ln($"GPS <- '{line}'");
			//}

			CommandQueue.IsCommandResponse(line);
		}

		private uint GetUInt(int pos, int len)
		{
			uint bits = 0;
			for (int i = pos; i < pos + len; i++)
				bits = (uint)((bits << 1) + ((_byteArray[i / 8] >> (7 - i % 8)) & 1u));
			return bits;
		}

		private uint RtkCrc24()
		{
			uint crc = 0;
			for (int i = 0; i < _binaryLength - 3; i++)
				crc = ((crc << 8) & 0xFFFFFF) ^ tbl_CRC24Q[(crc >> 16) ^ _byteArray[i]];
			return crc;
		}

		private string HexDump(byte[] data, int length)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < length; i++)
			{
				sb.AppendFormat("{0:X2} ", data[i]);
			}
			return sb.ToString();
		}

		private string HexAsciDump(byte[] data, int length)
		{
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < length; i++)
			{
				sb.AppendFormat("{0:X2} ", data[i]);
			}
			return sb.ToString();
		}

		/// <summary>
		/// Has comms stalled
		/// </summary>
		internal void CheckTimeouts()
		{
			// Check for Queue timeout
			if (CommandQueue.CheckForTimeouts())
				return;

			// Check for GPS timeout
			if ((DateTime.Now - _timeOfLastMessage).TotalMilliseconds > GPS_TIMEOUT_MS)
			{
				_gpsConnected = false;
				Log.Ln("W700 - GPS Timeout");
				CommandQueue.StartInitialiseProcess();
				_timeOfLastMessage = DateTime.Now;
			}
		}

	}
}
