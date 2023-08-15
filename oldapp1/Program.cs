internal class Program
{
    public static void Main(string[] args)
    {
        var inner = new Parent.Inner();
        var other = new Impl();
        if (other is Parent.Inner)
        {
            System.Console.WriteLine($"yay {inner}");
        }
    }
}

public class Parent
{
    public class Inner
    {
    }
}

public class Impl : Parent.Inner
{
}