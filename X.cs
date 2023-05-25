using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NATS.Client.Core;

public static class X
{
    private static readonly Stopwatch Timer = Stopwatch.StartNew();

    public static void Log(
        string message,
        [CallerMemberName] string memberName = "",
        [CallerFilePath] string sourceFilePath = "",
        [CallerLineNumber] int sourceLineNumber = 0)
    {
        var file = Path.GetFileNameWithoutExtension(sourceFilePath);
        Console.Error.WriteLine($"{Timer.Elapsed} {file}:{sourceLineNumber} [{memberName}] {message}");
    }
}
