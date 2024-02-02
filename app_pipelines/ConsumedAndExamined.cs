using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace app_pipelines;

public class ConsumedAndExamined
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
        
        // write 32 bytes
        {
            writer.Write(Encoding.ASCII.GetBytes(new string('0', 32)));
            var result = await writer.FlushAsync();
            Console.WriteLine($"[WRITER] Flushed. cancel:{result.IsCanceled} complete:{result.IsCompleted}");            
        }

        // consume 16, examine 24
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;
            Console.WriteLine($"[READER] ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");
            var consumed = buffer.GetPosition(buffer.Length < 16 ? buffer.Length : 16);
            var examined = buffer.GetPosition(buffer.Length < 24 ? buffer.Length : 24);
            Console.WriteLine($"[READER] consumed: {consumed.GetInteger()} examined: {examined.GetInteger()}");
            reader.AdvanceTo(consumed, examined);
        }
        
        // should be 16 left
        // consume 0, examine 8 (same offset as before)
        // read should complete synchronously, returning the 16 bytes each time
        {
            for (var i = 0; i < 3; i++)
            {
                var result = await reader.ReadAsync();
                var buffer = result.Buffer;
                Console.WriteLine($"[READER] ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");
                var consumed = buffer.Start;
                var examined = buffer.GetPosition(buffer.Length < 8 ? buffer.Length : 8);
                Console.WriteLine($"[READER] consumed: {consumed.GetInteger()} examined: {examined.GetInteger()}");
                reader.AdvanceTo(consumed, examined);
            }
        }
        
        // should be 16 left
        // consume 0, examine 16 (entire buffer)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;
            Console.WriteLine($"[READER] ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");
            var consumed = buffer.Start;
            var examined = buffer.GetPosition(buffer.Length < 16 ? buffer.Length : 16);
            Console.WriteLine($"[READER] consumed: {consumed.GetInteger()} examined: {examined.GetInteger()}");
            reader.AdvanceTo(consumed, examined);
        }
        
        // this call should block, because everything has been examined
        {
            var blocking = Task.Run(async () =>
            {
                var result = await reader.ReadAsync();
                var buffer = result.Buffer;
                Console.WriteLine($"[READER] ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");
            });
        
            // give it 1 second then cancel the read
            await Task.Delay(1000);
            if (!blocking.IsCompletedSuccessfully)
            {
                reader.CancelPendingRead();
                Console.WriteLine("[READER] CancelPendingRead() called as expected");
                await blocking;                
            }
        }
        
        // write 1 more byte
        {
            writer.Write(Encoding.ASCII.GetBytes(new string('0', 1)));
            var result = await writer.FlushAsync();
            Console.WriteLine($"[WRITER] Flushed. cancel:{result.IsCanceled} complete:{result.IsCompleted}");            
        }
        
        // this call should not block, because there is a new byte to be read
        // should be 17 left
        // consume and examine 17 (entire buffer)
        {
            var result = await reader.ReadAsync();
            var buffer = result.Buffer;
            Console.WriteLine($"[READER] ReadAsync() complete:{result.IsCompleted} cancelled:{result.IsCanceled} buffer:{buffer.Length}");
            var consumed = buffer.End;
            var examined = buffer.End;
            Console.WriteLine($"[READER] consumed: {consumed.GetInteger()} examined: {examined.GetInteger()}");
            reader.AdvanceTo(consumed, examined);
        }
    }
}