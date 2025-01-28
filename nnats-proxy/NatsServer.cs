using System.Diagnostics;

namespace nnats_proxy;

public class NatsServer : IDisposable
{
    private Process? _process;

    public NatsServer Start(int port)
    {
        Console.WriteLine($"Starting nats-server");
        var started = new ManualResetEventSlim();
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "nats-server",
                Arguments = $"-p {port} -js",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            }
        };

        void DataReceived(object _, DataReceivedEventArgs e)
        {
            if (e.Data == null) return;
            Console.WriteLine(e.Data);
            if (e.Data.Contains("Server is ready"))
                started.Set();
        }

        _process.OutputDataReceived += DataReceived;
        _process.ErrorDataReceived += DataReceived;
        _process.Start();
        _process.BeginErrorReadLine();
        _process.BeginOutputReadLine();
        ChildProcessTracker.AddProcess(_process);

        if (!started.Wait(5000))
        {
            throw new Exception("Error: Can't see nats-server started");
        }

        return this;
    }

    public void Dispose()
    {
        _process?.Dispose();
    }
}