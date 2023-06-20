using System.Collections.Concurrent;
using JetBrains.dotMemoryUnit;

namespace test1;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        // https://www.jetbrains.com/dotmemory/unit/
        // https://blog.jetbrains.com/dotnet/2018/10/04/unit-testing-memory-leaks-using-dotmemory-unit/
        
        void Isolator()
        {
            var myObject = new MyClass(this);
            Console.WriteLine($"{myObject.GetType()}");
        }
        
        Isolator();

        GC.Collect();
        
        dotMemory.Check(memory =>
        {
            var count = memory.GetObjects(where => where.Type.Is<MyClass>()).ObjectsCount;
            Console.WriteLine($"COUNT={count}");
            Assert.That(count, Is.EqualTo(0));
        });
    }
    
    [Test]
    public void Test2()
    {
        NewMethod();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        Thread.Sleep(1_000);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        dotMemory.Check(memory =>
        {
            var count = memory.GetObjects(where => where.Type.Is<MyClass>()).ObjectsCount;
            Console.WriteLine($"COUNT={count}");
            Assert.That(count, Is.EqualTo(0));
        });
    }

    private void NewMethod()
    {
        Task.Run(async () =>
        {
            var myObject = new MyClass(this);
            Console.WriteLine($"{myObject.GetType()}");
            await Task.Delay(10_000);
        });
    }
    
    [Test]
    public async Task Test3()
    {
        var connection = new Connection();

        Task.Run(async ()=> await connection.Sub(1));

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        dotMemory.Check(memory =>
        {
            var count = memory.GetObjects(where => where.Type.Is<Sub>()).ObjectsCount;
            Console.WriteLine($"COUNT={count}");
            Assert.That(count, Is.EqualTo(0));
        });
    }


    private WeakReference<MyClass>? _weak;

    public MyClass? MyObj
    {
        get
        {
            if (_weak.TryGetTarget(out var target))
            {
                return target;
            }

            return null;
        }
        set
        {
            if (value == null)
            {
                _weak = null;
                return;
            }
            _weak = new WeakReference<MyClass>(value);
        }
    }
}

public class MyClass
{
    private readonly Tests _tests;

    public MyClass(Tests tests)
    {
        _tests = tests;
        _tests.MyObj = this;
    }
}

public class Connection
{
    private SubManager _manager = new();
    public Task<Sub> Sub(int id)
    {
        return _manager.Add(id);
    }
}

public class SubManager
{
    private ConcurrentDictionary<int, WeakReference<Sub>> _subs = new();

    public Task<Sub> Add(int id)
    {
        var sub = new Sub(id, this);
        _subs[id] = new WeakReference<Sub>(sub);
        return Task.FromResult(sub);
    }

}

public class Sub
{
    private readonly int _id;
    private readonly SubManager _manager;

    public Sub(int id, SubManager manager)
    {
        _id = id;
        _manager = manager;
    }
}