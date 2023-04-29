using System.Net.Sockets;

namespace client1;

public class NatsClient
{
    private readonly NetworkStream _stream;

    public NatsClient()
    {
        // System.IO.pipeli
        var client = new TcpClient();
        client.Connect("localhost", 4222);
        _stream = client.GetStream();

        Task.Run(async delegate
        {

        });
    }
}