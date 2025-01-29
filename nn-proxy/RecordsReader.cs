using System.IO.Compression;
using System.Net.Sockets;
using System.Text;

class RecordsReader
{
    public async Task ReadAsync()
    {
        var file = "d:/tmp/NN-1.txt";
        // var file = "d:/tmp/NN-2.txt";
        Console.WriteLine($"Reading {file}");
        
        using var memoryStream1 = new MemoryStream();
        using var sr = new StreamReader(file);
        while (await sr.ReadLineAsync() is { } line)
        {
            var strings = line.Split(' ');
            var data = Convert.FromBase64String(strings[3]);
            Console.WriteLine($"READ: {data.Length} bytes");
            if (strings[2] == "S")
            {
                memoryStream1.Write(data);
            }
        }
        
        memoryStream1.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(memoryStream1, Encoding.Latin1);
        var payloadStream = new MemoryStream();
        try
        {
            new WsAnalyser().Analyse(reader, payloadStream);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        
        Console.WriteLine("======================================================");
        payloadStream.Seek(0, SeekOrigin.Begin);
        using var psr = new StreamReader(payloadStream, Encoding.Latin1);
        while (psr.ReadLine() is { } line)
        {
            if (line == "A") continue;
            if (line == "MSG testing.x 1 1") continue;
            Console.WriteLine($"LINE: {line}");
        }
        
    }
}

class WsAnalyser
{
    private bool _http;

    public WsAnalyser(bool http = true)
    {
        _http = http;
    }

    public void Analyse(StreamReader sr, Stream payloadStream)
    {
        while (true)
        {
            if (_http)
            {
                var http = new StringBuilder();
                var httpAscii = new StringBuilder();
                while (true)
                {
                    var line = sr.ReadLine();
                    if (line == null) throw new EndOfStreamException();
                    http.Append(line + "\r\n");
                    httpAscii.Append("  ");
                    httpAscii.AppendLine(line);
                    if (line == string.Empty) break;
                }

                // proxyServer.WriteBin(sw, dir, "http", http.ToString(), httpAscii.ToString());
                Console.WriteLine("HTTP");
                Console.WriteLine(httpAscii.ToString());
                _http = false;
            }
            
            // Websocket frame
            WsFrame frame = new();
            var headerBuffer = ReadAsBytes(cs => frame.OriginalChars.AddRange(cs), sr, 2);

            frame.ReadHeader(headerBuffer);

            // Set Reserved Flag 1
            // headerBuffer[0] |= 0b01000000;
            // frame[0] = (char)headerBuffer[0];
            
            var extendedPayloadLength = Array.Empty<byte>();
            if (frame.PayloadLength == 126)
            {
                extendedPayloadLength = ReadAsBytes(cs => frame.OriginalChars.AddRange(cs), sr, 2);
                frame.PayloadLength = BitConverter.ToUInt16(extendedPayloadLength.Reverse().ToArray(), 0);
            }
            else if (frame.PayloadLength == 127)
            {
                extendedPayloadLength = ReadAsBytes(cs => frame.OriginalChars.AddRange(cs), sr, 8);
                frame.PayloadLength = (int)BitConverter.ToUInt64(extendedPayloadLength.Reverse().ToArray(), 0);
            }

            byte[] maskingKey = null;
            if (frame.IsMasked)
            {
                maskingKey = ReadAsBytes(cs => frame.OriginalChars.AddRange(cs), sr, 4);
            }

            frame.MaskingKey = maskingKey;

            var payloadData = ReadAsBytes(cs => frame.OriginalChars.AddRange(cs), sr, frame.PayloadLength);

            if (frame.IsMasked && maskingKey != null)
            {
                for (int i = 0; i < payloadData.Length; i++)
                {
                    payloadData[i] = (byte)(payloadData[i] ^ maskingKey[i % 4]);
                }
            }

            //var payloadText = Encoding.UTF8.GetString(payloadData);
            //Console.WriteLine($"FIN: {fin}, Opcode: {opcode}, Payload: {payloadText}");

            var deflated = false;
            if (frame.Rsv1)
            {
                try
                {
                    using var memoryStream = new MemoryStream(payloadData);
                    using var deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress);
                    using var resultStream = new MemoryStream();

                    deflateStream.CopyTo(resultStream);
                    payloadData = resultStream.ToArray();
                    deflated = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Deflate error: {ex.Message}");
                    throw;
                }
            }

            frame.PayloadData = payloadData;
            
            // var payloadStr = Encoding.Latin1.GetString(payloadData);
            // if (orig == 'S')
            // {
            //     Console.WriteLine($"Original SERVER string: {payloadStr}");
            //     if (payloadStr.StartsWith("PONG"))
            //     {
            //         frame.PayloadData = Encoding.Latin1.GetBytes("PONN\r\n");
            //     }
            // }
            
            payloadStream.Write(payloadData);
            
            var hexDumpString = HexDumpString(payloadData);

            Console.WriteLine($"FIN:{frame.Fin} OP:{frame.OpCode} R1:{frame.Rsv1} R2:{frame.Rsv2} R3:{frame.Rsv3} Masked:{frame.IsMasked} [Deflated={deflated}]");
            Console.WriteLine(hexDumpString);
        }
    }
    
    static byte[] ReadAsBytes(Action<char[]> bufferAction, StreamReader reader, int length)
    {
        var charBuffer = new char[length];
        var byteBuffer = new byte[length];
                
        var totalBytesRead = 0;
        while (totalBytesRead < length)
        {
            var currentBytesRead = reader.Read(charBuffer, totalBytesRead, length - totalBytesRead);
            if (currentBytesRead == 0) throw new IOException("Unexpected end of stream");
            totalBytesRead += currentBytesRead;
        }

        // Convert characters to bytes
        for (var i = 0; i < length; i++)
        {
            byteBuffer[i] = (byte)charBuffer[i];
        }
                
        bufferAction(charBuffer);
        return byteBuffer;
    }
    
    private static string HexDumpString(ReadOnlySpan<byte> span)
    {
        var ascii = new StringBuilder();
        var hexDump = new StringBuilder();
        var indent = true;
        for (int i = 0; i < span.Length; i++)
        {
            if (indent)
            {
                hexDump.Append("  ");
                indent = false;
            }
            
            byte currentByte = (byte)span[i];
            hexDump.AppendFormat("{0:X2} ", currentByte);

            if (currentByte > 31 && currentByte < 127) // Printable ASCII range
            {
                ascii.Append((char)currentByte);
            }
            else
            {
                ascii.Append('.');
            }

            // Add a new line every 16 bytes for formatting
            if ((i + 1) % 16 == 0)
            {
                hexDump.Append(" ");
                hexDump.Append(ascii);
                hexDump.AppendLine();
                indent = true;
                ascii.Clear();
            }
        }

        // Append remaining bytes if total length wasn't a multiple of 16
        if (span.Length % 16 != 0)
        {
            int padding = (16 - (span.Length % 16)) * 3;
            hexDump.Append(' ', padding);
            hexDump.Append(" ");
            hexDump.Append(ascii);
        }

        var hexDumpString = hexDump.ToString();
        return hexDumpString;
    }
    
        class WsFrame
    {
        public List<char> OriginalChars = new();
        public bool Fin;
        public bool IsMasked;
        public int OpCode;
        public int PayloadLength;
        public bool Rsv1;
        public bool Rsv2;
        public bool Rsv3;
        public byte[]? MaskingKey;
        public byte[] PayloadData;

        public void ReadHeader(byte[] headerBuffer)
        {
            Fin = (headerBuffer[0] & 0b10000000) != 0;
            OpCode = headerBuffer[0] & 0b00001111;
            IsMasked = (headerBuffer[1] & 0b10000000) != 0;
            PayloadLength = headerBuffer[1] & 0b01111111;

            Rsv1 = (headerBuffer[0] & 0b01000000) != 0; // Reserved Flag 1
            Rsv2 = (headerBuffer[0] & 0b00100000) != 0; // Reserved Flag 2
            Rsv3 = (headerBuffer[0] & 0b00010000) != 0; // Reserved Flag 3
        }

        public char[] Build()
        {
            var frameBuilder = new List<byte>();

            // First byte: FIN, RSV1, RSV2, RSV3, and OpCode
            byte firstByte = 0;
            if (Fin) firstByte |= 0b10000000;
            if (Rsv1) firstByte |= 0b01000000;
            if (Rsv2) firstByte |= 0b00100000;
            if (Rsv3) firstByte |= 0b00010000;
            firstByte |= (byte)(OpCode & 0b00001111);
            frameBuilder.Add(firstByte);

            // Second byte: Mask bit and Payload Length
            byte secondByte = 0;
            if (IsMasked) secondByte |= 0b10000000;

            var payload = new byte[PayloadData.Length];
            Array.Copy(PayloadData, payload, PayloadData.Length);
            
            // Compress payload if Rsv1 is set
            if (Rsv1)
            {
                using var memoryStream = new MemoryStream();
                using (var deflateStream = new DeflateStream(memoryStream, CompressionLevel.Optimal))
                {
                    deflateStream.Write(payload, 0, payload.Length);
                }
                memoryStream.Flush();
                payload = memoryStream.ToArray();
            }
            
            if (IsMasked && MaskingKey != null)
            {
                for (int i = 0; i < payload.Length; i++)
                {
                    payload[i] ^= MaskingKey[i % MaskingKey.Length];
                }
            }
            
            if (payload.Length <= 125)
            {
                secondByte |= (byte)payload.Length;
                frameBuilder.Add(secondByte);
            }
            else if (payload.Length <= ushort.MaxValue)
            {
                secondByte |= 126;
                frameBuilder.Add(secondByte);

                var extendedPayloadLength = BitConverter.GetBytes((ushort)payload.Length);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(extendedPayloadLength);
                }
                frameBuilder.AddRange(extendedPayloadLength);
            }
            else
            {
                secondByte |= 127;
                frameBuilder.Add(secondByte);

                var extendedPayloadLength = BitConverter.GetBytes((ulong)payload.Length);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(extendedPayloadLength);
                }
                frameBuilder.AddRange(extendedPayloadLength);
            }

            // Masking Key
            if (IsMasked && MaskingKey != null)
            {
                frameBuilder.AddRange(MaskingKey);
            }

            // Payload Data
            frameBuilder.AddRange(payload);

            var charBuffer = new char[frameBuilder.Count];
            for (var index = 0; index < frameBuilder.Count; index++)
            {
                var b = frameBuilder[index];
                charBuffer[index] = (char)b;
            }

            return charBuffer;
        }
    }

}