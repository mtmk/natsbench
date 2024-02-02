using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace app_pipelines;

public class AdvanceExaminedAndCompleted
{
    public async Task Run()
    {
        const int unit = 1;

        var pipe = new Pipe(new PipeOptions(
            pauseWriterThreshold: unit * 64, // default 65536
            resumeWriterThreshold: unit * 32, // default 65536 / 2
            //minimumSegmentSize: unit * 4, // default 4096
            useSynchronizationContext: false));
        var reader = pipe.Reader;
        var writer = pipe.Writer;
        
        Console.WriteLine($"reader:{reader.GetType()}");
        _ = Task.Run(async () =>
        {
            while (true)
            {
                writer.Write(Encoding.ASCII.GetBytes("0123456789_10_456789_20_456789012345678_32"));
                var result = await writer.FlushAsync();
                Console.WriteLine($"[WRITER] Flushed. cancel:{result.IsCanceled} complete:{result.IsCompleted}");
            }
        });
        await Task.Delay(1000);

        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;
            Console.WriteLine($"[READER] 1 ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");
            var examined = buffer.GetPosition(32 > buffer.Length ? buffer.Length : 32);
            var consumed = buffer.Start;
            Console.WriteLine($"[READER] 1 consumed: {consumed.GetInteger()} examined: {examined.GetInteger()}");
            reader.AdvanceTo(consumed, examined);
        }
        await Task.Delay(1000);
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;
            Console.WriteLine($"[READER] 2 ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");
            var examined = buffer.GetPosition(48 > buffer.Length ? buffer.Length : 48);
            var consumed = buffer.Start;
            Console.WriteLine($"[READER] 2 consumed: {consumed.GetInteger()} examined: {examined.GetInteger()}");
            reader.AdvanceTo(consumed, examined);
        }
        await Task.Delay(1000);

        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;
            Console.WriteLine($"[READER] 3 ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");
            var start = buffer.Start;
            var consumed = buffer.GetPosition(48 > buffer.Length ? buffer.Length : 48);
            var segment = (ReadOnlySequenceSegment<byte>)consumed.GetObject()!;
            Console.WriteLine($"[READER] 3 start: {start.GetInteger()} consumed: {segment.RunningIndex}/{consumed.GetInteger()}");
            reader.AdvanceTo(consumed);
        }
        await Task.Delay(1000);

        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;
            Console.WriteLine($"[READER] 4 ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");
            var start = buffer.Start;
            var consumed = buffer.GetPosition(32 > buffer.Length ? buffer.Length : 32);
            var segment = (ReadOnlySequenceSegment<byte>)consumed.GetObject()!;
            Console.WriteLine($"[READER] 4 start: {start.GetInteger()} consumed: {segment.RunningIndex}/{consumed.GetInteger()}");
            reader.AdvanceTo(consumed);
        }
        await Task.Delay(1000);

    }
}