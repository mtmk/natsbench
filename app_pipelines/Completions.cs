using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace app_pipelines;

public class Completions
{
    public async Task Run()
    {
        const int unit = 8;

        var pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: unit * 64, // default 65536
            resumeWriterThreshold: unit * 32, // default 65536 / 2
            minimumSegmentSize: unit * 4, // default 4096
            useSynchronizationContext: false));
        var reader = pipe.Reader;
        var writer = pipe.Writer;

        var cts = new CancellationTokenSource();
        var t1 = Task.Run(async () =>
        {
            long total = 0;
            for (int i = 0; i < 10; i++)
            {
                Console.WriteLine($"Sending {i}...");
                var sizeHint = unit * 16;
                Console.WriteLine($"GetMemory() {i}...");
                var buffer = writer.GetMemory(sizeHint);
                Encoding.ASCII.GetBytes(new string($"{i}"[0], sizeHint)).CopyTo(buffer.Span);
                Console.WriteLine($"Advance() {i}...");
                writer.Advance(sizeHint);
                total += sizeHint;
                Console.WriteLine($"Flushing {i}... [{total}]");
                try
                {
                    Console.WriteLine($"FlushAsync() {i}...");
                    var result = await writer.FlushAsync(cts.Token);
                    Console.WriteLine($"Flushed {i}. cancel:{result.IsCanceled} complete:{result.IsCompleted}");
                }
                catch (OperationCanceledException e)
                {
                    Console.WriteLine($"Flushed {i}. exception:{e.Message}");
                    break;
                }
            }
        });

        await Task.Delay(2000);
        
        Console.WriteLine("hit enter");
        Console.ReadLine();

        await cts.CancelAsync();

        var result = await reader.ReadAsync();
        var buffer = result.Buffer;
        Console.WriteLine($"ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");

        var ob = new byte[buffer.Length];
        buffer.CopyTo(ob);
        Console.WriteLine(Encoding.ASCII.GetString(ob));
        
        await writer.CompleteAsync();
        
        await Task.WhenAll(t1);
    }
}