using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

var pipe = new Pipe(new PipeOptions(
    pauseWriterThreshold: 1024*1024*32,
    resumeWriterThreshold: 1024*1024*16,
    minimumSegmentSize: 1024*64,
    useSynchronizationContext: false));
var reader = pipe.Reader;
var writer = pipe.Writer;

var read = new List<long>();
ManualResetEventSlim mre = new ManualResetEventSlim();
Task.Run(async () =>
{
    long size = 0;
    while (true)
    {
        var result = await reader.ReadAsync();
        var buffer = result.Buffer;
        if (!buffer.IsEmpty)
        {
            lock (read) read.Add(buffer.Length);
            size += buffer.Length;
            await Task.Delay(1);
        }
        
        // foreach (ReadOnlyMemory<byte> memory in buffer)
        // {
        //     Console.WriteLine($">>{Encoding.ASCII.GetString(memory.Span)}<<");
        // }

        reader.AdvanceTo(buffer.End);
        
        if (result.IsCompleted)
        {
            break;
        }
    }

    lock (read)
    {
        var bytes = (double)size / read.Count;
        Console.WriteLine(
            $"rcv size: {size:n0} ({read.Count:n0}) {bytes:n0} bytes / {bytes / (1024.0 ):n2}KB per msg");
    }

    mre.Set();
});

long totalLen = 0;
var written = new List<long>();
for (int i = 0; i < 100_000; i++)
{
    var len = 100; // Random.Shared.Next(8, 1024*8);
    var buffer = writer.GetMemory(len);
    Random.Shared.NextBytes(buffer.Span);
    writer.Advance(len);
    await writer.FlushAsync();
    totalLen += len;
    written.Add(len);
}

writer.Complete();

mre.Wait();

lock (read)
{
    Console.WriteLine($"snt size: {totalLen:n0} ({written.Count:n0})");

    if (totalLen != read.Sum())
    {
        Console.WriteLine($"ERROR totalLen: {totalLen} read.Sum(): {read.Sum()}");
    }

    if (read.Sum() != written.Sum())
    {
        Console.WriteLine($"ERROR read.Sum(): {read.Sum()} written.Sum(): {written.Sum()}");
    }
}
// lock (read)
// {
//     for (var index = 0; index < read.Count; index++)
//     {
//         //Console.WriteLine($"index: {index} read: {read[index],10:n0} written: {written[index],10:n0}");
//         var r = read[index];
//         var w = written[index];
//         if (r != w)
//         {
//             //Console.WriteLine($"ERROR {index} {r} {w}");
//         }
//     }
// }

// var generator = new RandomSentenceGenerator();
// for (int i = 0; i < 10; i++)
// {
//     WriteMessage(writer, generator.GenerateRandomParagraph());
//     await writer.FlushAsync();
//     await Task.Delay(1000);
// }

void WriteMessage(PipeWriter pipeWriter, string message)
{
    var payload = Encoding.ASCII.GetBytes(message);
    var span = pipeWriter.GetSpan(payload.Length);
    payload.CopyTo(span);
    pipeWriter.Advance(payload.Length);
}

public class RandomSentenceGenerator
{
    private Random random = new Random();

    private string[] subjects = { "The cat", "A dog", "She", "He", "The car", "An apple", "Our team", "The sun", "A bird", "The computer" };
    private string[] verbs = { "runs", "jumps", "drives", "flies", "eats", "sleeps", "sings", "dances", "explodes", "laughs" };
    private string[] adjectives = { "quickly", "lazily", "noisily", "silently", "happily", "sadly", "brightly", "darkly", "eagerly", "calmly" };


    public string GenerateRandomSentence()
    {
        string subject = subjects[random.Next(subjects.Length)];
        string verb = verbs[random.Next(verbs.Length)];
        string adjective = adjectives[random.Next(adjectives.Length)];

        return $"{subject} {verb} {adjective}.";
    }
    
    public string GenerateRandomParagraph(int minSentences = 3, int maxSentences = 7)
    {
        int sentenceCount = random.Next(minSentences, maxSentences + 1);
        StringBuilder paragraph = new StringBuilder();

        for (int i = 0; i < sentenceCount; i++)
        {
            paragraph.Append(GenerateRandomSentence());
        }

        var generateRandomParagraph = paragraph.ToString();

        using var sha256 = SHA256.Create();
        var hash = Base58Converter.Base58Encode(sha256.ComputeHash(Encoding.ASCII.GetBytes(generateRandomParagraph)))
            .Substring(0, 16);
        return $"{hash}:{hash.Length}:{generateRandomParagraph}";
    }
}

public class Base58Converter
{
    private const string Base58Alphabet = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";

    public static string Base58Encode(byte[] array)
    {
        BigInteger intData = 0;
        for (int i = 0; i < array.Length; i++)
        {
            intData = intData * 256 + array[i];
        }

        string result = "";
        while (intData > 0)
        {
            int remainder = (int)(intData % 58);
            intData /= 58;
            result = Base58Alphabet[remainder] + result;
        }

        // Append `1` for each leading 0 byte
        for (int i = 0; i < array.Length && array[i] == 0; i++)
        {
            result = '1' + result;
        }

        return result;
    }

    public static byte[] Base58Decode(string s)
    {
        BigInteger intData = 0;
        for (int i = 0; i < s.Length; i++)
        {
            int digit = Base58Alphabet.IndexOf(s[i]); // Slow
            if (digit < 0)
                throw new FormatException($"Invalid Base58 character `{s[i]}` at position {i}");
            intData = intData * 58 + digit;
        }

        // Count leading zeros
        int leadingZeroCount = s.TakeWhile(c => c == '1').Count();
        var leadingZeros = Enumerable.Repeat((byte)0, leadingZeroCount);
        var bytesWithoutLeadingZeros = intData.ToByteArray().Reverse().SkipWhile(b => b == 0); // BigInteger adds an extra 0 byte

        return leadingZeros.Concat(bytesWithoutLeadingZeros).ToArray();
    }
}