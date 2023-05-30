using System.Text.RegularExpressions;

namespace client2;

class Program
{
    static void Main()
    {
        var nats = NatsClient.Connect().Start();

        while (true)
        {
            var cmd = Console.ReadLine();

            if (Regex.IsMatch(cmd, @"^\s*q(uit)?\s*$"))
            {
                break;
            }
        }
        
        Console.WriteLine("bye");
    }
}