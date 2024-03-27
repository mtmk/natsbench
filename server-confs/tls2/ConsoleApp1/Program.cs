// See https://aka.ms/new-console-template for more information

using NATS.Client.Core;

Console.WriteLine("Hello, World!");

const string caFile = "certs/ca-cert.pem";
const string clientCertFile = "certs/chainedclient-cert.pem";
const string clientKeyFile = "certs/chainedclient-key.pem";

var nats = new NatsConnection(new NatsOpts
{
    Url = "127.0.0.1",
    TlsOpts = new NatsTlsOpts
    {
        CaFile = caFile,
        CertFile = clientCertFile,
        KeyFile = clientKeyFile,
    }
});

await nats.ConnectAsync();
var rtt = await nats.PingAsync();
Console.WriteLine($"---");
Console.WriteLine($"{nats.ServerInfo}");
Console.WriteLine($"---");
Console.WriteLine($"rtt={rtt}");