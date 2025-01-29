using System.Text;

class BinReader
{
    public void Read()
    {
        // var file = "d:/tmp/go-output-20250128-181633.bin";
        var file = "d:/tmp/go-ws-d-20250128-192611.bin";
        using var sr = new StreamReader(file, Encoding.Latin1);
        var n = 0;
        while (sr.ReadLine() is { } line)
        {
            n++;
            if (line == "A") continue;
            if (line == "MSG testing.x 1 1") continue;
            Console.WriteLine($"LINE {n}: {line}");
        }
    }
    
    public void ReadWs()
    {
        var file = "d:/tmp/go-ws-20250128-184238.bin";
        using var sr = new StreamReader(file, Encoding.Latin1);

        var payloadStream = new MemoryStream();
        try
        {
            new WsAnalyser(false).Analyse(sr, payloadStream);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
        
        Console.WriteLine("======================================================");
        payloadStream.Seek(0, SeekOrigin.Begin);
        using var psr = new StreamReader(payloadStream, Encoding.Latin1);
        while (psr.ReadLine() is { } line)
        {
            if (line == "A") continue;
            if (line == "MSG testing.x 1 1") continue;
            Console.WriteLine($"LINE: {line}");
        }
    }
}