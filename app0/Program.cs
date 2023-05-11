// See https://aka.ms/new-console-template for more information

using System.Text;

Console.WriteLine("Hello, World!");

string s = "ABC€𤭢";

foreach (char c in s)
{
    uint u = c;
    byte b = (byte)u;
    char c2 = (char)b;
    Console.WriteLine($"{c} {u:X} {b} {c2}");
}

foreach (var b in Encoding.ASCII.GetBytes(s))
{
    Console.WriteLine($"{b}{(char)b}");
}
