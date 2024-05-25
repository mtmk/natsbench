
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;

var caCertPem = File.ReadAllText("certs/ca-cert.pem");
var inter1CertPem = File.ReadAllText("certs/intermediate01-cert.pem");
var inter2CertPem = File.ReadAllText("certs/intermediate02-cert.pem");
var clientCertPem = File.ReadAllText("certs/leafclient-cert.pem");
var clientKeyPem = File.ReadAllText("certs/leafclient-key.pem");

// Console.WriteLine($"caCertPem={caCertPem}");
// Console.WriteLine($"caCertPem={File.ReadAllText(caCertPem)}");
// var p1 = X509Certificate2.CreateFromPem(File.ReadAllText(caCertPem));
// Console.WriteLine($"p1={p1}");
// return;

var tcpClient = new TcpClient("localhost", 4222);
Stream stream = tcpClient.GetStream();
var reader = new StreamReader(stream, encoding: Encoding.ASCII);
var writer = new StreamWriter(stream, encoding: Encoding.ASCII);

var connected = new TaskCompletionSource();

var readerTask = Task.Run(async () =>
{
    try
    {
        while (await Volatile.Read(ref reader).ReadLineAsync() is { } line)
        {
            if (line.StartsWith("INFO"))
            {
                // Console.WriteLine(line);
                if (line.Contains("\"tls_required\":true"))
                {
                    Log("Upgrade to TLS");
                    var sslStream = new SslStream(stream);
                    var clientCertificates = new X509Certificate2Collection();

                    // https://github.com/dotnet/runtime/issues/66283#issuecomment-1061014225
                    // https://github.com/dotnet/runtime/blob/380a4723ea98067c28d54f30e1a652483a6a257a/src/libraries/System.Net.Security/tests/FunctionalTests/TestHelper.cs#L192-L197
                    var caFromPemFile = X509Certificate2.CreateFromPem(caCertPem);
                    var caPem = caFromPemFile.Export(X509ContentType.Pfx);
                    var ca = new X509Certificate2(caPem);
                    var cert = new X509Certificate2(X509Certificate2.CreateFromPem(clientCertPem, clientKeyPem).Export(X509ContentType.Pfx));
                    clientCertificates.Add(cert);
                    clientCertificates.Add(new X509Certificate2(X509Certificate2.CreateFromPem(inter2CertPem).Export(X509ContentType.Pfx)));
                    clientCertificates.Add(new X509Certificate2(X509Certificate2.CreateFromPem(inter1CertPem).Export(X509ContentType.Pfx)));
                    clientCertificates.Add(new X509Certificate2(caPem));
                    X509ChainPolicy? policy = new()
                    {
                        RevocationMode = X509RevocationMode.Online,
                        TrustMode = X509ChainTrustMode.CustomRootTrust,
                    };;
                    SslStreamCertificateContext? streamCertificateContext = SslStreamCertificateContext.Create(
                        clientCertificates[0],
                        clientCertificates,
                        trust: SslCertificateTrust.CreateForX509Collection(new X509Certificate2Collection(ca)));

                    
                    var sslClientAuthenticationOptions = new SslClientAuthenticationOptions
                    {
                        ClientCertificateContext = streamCertificateContext,
                        CertificateChainPolicy = policy,
                        EnabledSslProtocols = SslProtocols.Tls13 | SslProtocols.Tls12,
                        ClientCertificates = clientCertificates,
                        LocalCertificateSelectionCallback = (sender, host, certificates, certificate, issuers) =>
                        {
                            return certificates is { Count: > 0 } ? certificates[0] : null!;
                        },
                        RemoteCertificateValidationCallback = (sender, certificate, chain, errors) =>
                        {
                            return true;
                        },
                    };
                    
                    await sslStream.AuthenticateAsClientAsync(sslClientAuthenticationOptions);
                    
                    writer = new StreamWriter(sslStream, Encoding.ASCII);
                    Interlocked.Exchange(ref reader, new StreamReader(sslStream, Encoding.ASCII));
                    stream = sslStream;
                }

                Log("Sending CONNECT");
                await writer.WriteAsync("CONNECT {\"verbose\":false,\"pedantic\":false,\"name\":\"tls-test\"}\r\n");
                await writer.FlushAsync();
                await writer.WriteAsync("PING\r\n");
                await writer.FlushAsync();

                connected.SetResult();
            }
            else if (line.StartsWith("PING"))
            {
                Log("Ping-pong");
                await writer.WriteAsync("PONG\r\n");
                await writer.FlushAsync();
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

await writer.WriteAsync("PUB x 1\r\nx\r\n");
await writer.FlushAsync();

Console.ReadLine();

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