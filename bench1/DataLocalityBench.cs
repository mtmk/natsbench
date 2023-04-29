using BenchmarkDotNet.Attributes;

namespace bench1;

public struct SmallStruct
{
    public int Value1;
    public int Value2;
}
public class SmallClass
{
    public int Value1;
    public int Value2;
}

[MemoryDiagnoser]
public class DataLocalityBench
{
    // both arrays are initialized with one million elements
    private SmallClass[] classes = new SmallClass[items];
    private SmallStruct[] structs = new SmallStruct[items];
    private const int items = 1_000_000;

    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < items; i++)
        {
            classes[i] = new SmallClass();
            classes[i].Value1 = i % 100;
            structs[i].Value1 = i % 100;
        }
    }
    
    [Benchmark]
    public int StructArrayAccess()
    {
        int result = 0;
        for (int i = 0; i < items; i++)
            result += Helper1(structs, i);
        return result;
    }
    [Benchmark]
    public int ClassArrayAccess()
    {
        int result = 0;
        for (int i = 0; i < items; i++)
            result += Helper2(classes, i);
        return result;
    }
    public int Helper1(SmallStruct [] data, int index)
    {
        return data[index].Value1;
    }
    public int Helper2(SmallClass [] data, int index)
    {
        return data[index].Value1;
    }
}