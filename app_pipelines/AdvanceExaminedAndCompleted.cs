using System.Buffers;
using System.IO.Pipelines;

namespace app_pipelines;

public class AdvanceExaminedAndCompleted
{
    public async Task Run()
    {
        const int unit = 1000;

        var pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: unit * 10, // default 65536
            resumeWriterThreshold: unit * 5, // default 65536 / 2
            minimumSegmentSize: unit * 1, // default 4096
            useSynchronizationContext: false));
        var reader = pipe.Reader;
        var writer = pipe.Writer;

        Console.WriteLine($"reader:{reader.GetType()}");
        
        // _ = Task.Run(async () =>
        // {
        //     while (true)
        //     {
        //         writer.Write(new byte[unit * 16]);
        //         var result = await writer.FlushAsync();
        //         Console.WriteLine($"[WRITER] cancel:{result.IsCanceled} complete:{result.IsCompleted}");
        //     }
        // });

        Task.Run(async () =>
        {
            for (var i = 0; ; i++)
            {
                for (int j = 0; j < 100; j++)
                    writer.Write(new byte[unit]);
                var result = await writer.FlushAsync();
                Console.WriteLine($"[WRITER] {i} cancel:{result.IsCanceled} complete:{result.IsCompleted}");
            }
        });

        for (var i = 0;; i++)
        {
            await Task.Delay(1000);
            var result = await reader.ReadAsync();
            ReadOnlySequence<byte> buffer = result.Buffer;
            var count = 0;
            foreach (var readOnlyMemory in buffer)
            {
                count++;
            }
            Console.WriteLine($"[READER] ({i}) count:{count} complete:{result.IsCompleted} cancel:{result.IsCanceled} buffer:{buffer.Length}");
            reader.AdvanceTo(buffer.GetPosition(buffer.Length / 2));
        }
    }
}