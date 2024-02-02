using System.Buffers;
using System.IO.Pipelines;

namespace app_pipelines;

public class AdvanceExaminedAndCompleted
{
    public async Task Run()
    {
        const int unit = 1024;

        var pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: unit * 64, // default 65536
            resumeWriterThreshold: unit * 32, // default 65536 / 2
            // minimumSegmentSize: unit * 4, // default 4096
            useSynchronizationContext: false));
        var reader = pipe.Reader;
        var writer = pipe.Writer;

        Console.WriteLine($"reader:{reader.GetType()}");
        
        _ = Task.Run(async () =>
        {
            while (true)
            {
                writer.Write(new byte[unit * 16]);
                var result = await writer.FlushAsync();
                Console.WriteLine($"[WRITER] cancel:{result.IsCanceled} complete:{result.IsCompleted}");
            }
        });

        for (var i = 0;; i++)
        {
            await Task.Delay(1000);
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;
            Console.WriteLine($"[READER] ({i}) complete:{result.IsCompleted} cancel:{result.IsCanceled} buffer:{buffer.Length}");
            reader.AdvanceTo(buffer.Start, buffer.End);
        }
    }
}