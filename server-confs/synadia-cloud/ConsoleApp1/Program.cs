using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using NATS.Client.Core.Internal;


// var d = @"C:\Users\mtmk\src\Ed25519\Chaos.NaCl";
// var f = @"C:\Users\mtmk\src\natsbench\server-confs\synadia-cloud\ConsoleApp1\nacl.cs";
//
// using var sw = new StreamWriter(f);
// sw.WriteLine("// Borrowed from https://github.com/CryptoManiac/Ed25519");
//
// var files = Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories)
//     .Where(file => !file.Contains("AssemblyInfo"))
//     .ToList();
//
// // first pass collect usings
// var usings = new HashSet<string>();
// foreach (var file in files)
// {
//     Console.WriteLine($"file: {file}");
//     foreach (var line in File.ReadAllLines(file).Where(x => x.StartsWith("using")))
//     {
//         usings.Add(line);
//     }
// }
//
// sw.WriteLine();
// foreach (var @using in usings)
// {
//     sw.WriteLine(@using);
// }
//
// // contents concat 
// sw.WriteLine();
// foreach (var file in files)
// {
//     sw.WriteLine($"// file: {Path.GetFileName(file)}");
//     var sr = new StreamReader(file);
//     while (true)
//     {
//         var line = sr.ReadLine();
//         if (line == null)
//         {
//             break;
//         }
//         if (line.StartsWith("using"))
//             continue;
//         sw.WriteLine(line);
//     }
//     sw.WriteLine();
// }



var tcpClient = new TcpClient("connect.ngs.global", 4222);
Stream stream = tcpClient.GetStream();
var reader = new StreamReader(stream, encoding: Encoding.UTF8, false, 4096, true);
var writer = new StreamWriter(stream, encoding: Encoding.ASCII);

var connected = new TaskCompletionSource();

var readerTask = Task.Run(async () =>
{
    try
    {
        while (Volatile.Read(ref reader).ReadLine() is { } line)
        {
            if (line.StartsWith("INFO"))
            {
                Console.WriteLine(line);
                if (line.Contains("\"tls_required\":true"))
                {
                    Log("Upgrade to TLS");
                    var sslStream = new SslStream(stream);
                    
                    await sslStream.AuthenticateAsClientAsync("connect.ngs.global");

                    writer = new StreamWriter(sslStream, Encoding.ASCII);
                    Interlocked.Exchange(ref reader, new StreamReader(sslStream, Encoding.ASCII));
                    stream = sslStream;
                }


                if (line.Contains("\"auth_required\":true"))
                {
                    Console.WriteLine("Auth required");
                    var creds = new UserCredentials(credsFile: "C:/users/mtmk/.keys/NGS-a1-u1.creds");
                    var m = Regex.Match(line, @"""nonce"":""([^""]+)""");
                    if (m.Success)
                    {
                        var nonce = m.Groups[1].Value;
                        Console.WriteLine($"found nonce: {nonce}");
                        var sig = creds.Sign(nonce);
                        Console.WriteLine($"JWT={creds.Jwt}");
                        Console.WriteLine($"SIG={sig}");
                        Log("Sending CONNECT with auth");
                        await writer.WriteAsync("CONNECT {\"verbose\":false," +
                                                "\"pedantic\":false," +
                                                "\"name\":\"tls-test\"," +
                                                "\"lang\":\"c#\"," +
                                                "\"version\":\"1.0.0\"," +
                                                $"\"jwt\":\"{creds.Jwt}\"," +
                                                $"\"sig\":\"{sig}\"" +
                                                "}\r\n");
                    }
                }
                else
                {
                    Log("Sending CONNECT");
                    await writer.WriteAsync("CONNECT {\"verbose\":false,\"pedantic\":false,\"name\":\"tls-test\"}\r\n");
                }

                await writer.FlushAsync();
                Log("CONNECT sent");
                
                await writer.WriteAsync("PING\r\n");
                await writer.FlushAsync();
                Log("CONNECT PING sent");
                
                
            }
            else if (line.StartsWith("PING"))
            {
                Log("Ping-pong");
                await writer.WriteAsync("PONG\r\n");
                await writer.FlushAsync();
            }
            else if (line.StartsWith("PONG"))
            {
                Log("PONG received");
                connected.SetResult();
            }
            else
            {
                Log("Unknown server message");
            }
        }
    }
    catch (ObjectDisposedException)
    {
    }
    catch(Exception e)
    {
        Log($"Reader loop error: {e}");
    }
});

await connected.Task;

Log("Connected");

while (true)
{
    var msg = $"Hello, world! {DateTime.Now:O}";
    await writer.WriteAsync($"PUB x {msg.Length}\r\n{msg}\r\n");
    await writer.FlushAsync();

    Console.ReadLine();
}

Log("Closing connection");

stream.Close();
tcpClient.Close();

try
{
    await readerTask;
}
catch (Exception e)
{
    Log($"Error: {e.GetBaseException().Message}");
}

Log("Bye");

void Log(string message)
{
    Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message}");
}