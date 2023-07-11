class Program
{
    static void Main()
    {
        Console.WriteLine("Hi!");
        var obj = new My();
        if (obj is IMy imy)
        {
            Console.WriteLine($"IMy:{imy.GetType().Name}");
        }
        if (obj is MyAbs myAbs)
        {
            Console.WriteLine($"MyAbs:{myAbs.GetType().Name}");
        }
    }
}

interface IMy{}
abstract class MyAbs : IMy {}
class My : MyAbs {}