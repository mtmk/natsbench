using System.Buffers;
using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;

namespace bench1;

/*
|           Method |     Mean |    Error |   StdDev |   Median | Allocated |
|----------------- |---------:|---------:|---------:|---------:|----------:|
|   UsingByteArray | 10.51 ns | 0.240 ns | 0.267 ns | 10.40 ns |         - |
| UsingStringBytes | 12.37 ns | 0.151 ns | 0.134 ns | 12.33 ns |         - |
|   UsingCharArray | 17.57 ns | 0.383 ns | 0.711 ns | 17.27 ns |         - |
|      UsingString | 19.83 ns | 0.155 ns | 0.145 ns | 19.76 ns |         - |
*/

[MemoryDiagnoser]
public class JsonTypeDeserializeBench
{ 
    private ReadOnlySequence<byte> _sequence;

    public struct JsonTypeStruct
    {
        public string Type { get; set; }
    }
    public class JsonTypeClass
    {
        public string Type { get; set; }
    }
    [GlobalSetup]
    public void Setup()
    {
        var json =
            @"{""type"":""io.nats.jetstream.api.v1.stream_info_response"",""total"":0,""offset"":0,""limit"":0,""config"":{""name"":""events"",""subjects"":[""events""],""retention"":""limits"",""max_consumers"":-1,""max_msgs"":-1,""max_bytes"":-1,""max_age"":0,""max_msgs_per_subject"":-1,""max_msg_size"":-1,""discard"":""old"",""storage"":""file"",""num_replicas"":1,""duplicate_window"":120000000000,""allow_direct"":false,""mirror_direct"":false,""sealed"":false,""deny_delete"":false,""deny_purge"":false,""allow_rollup_hdrs"":false},""created"":""2023-07-10T14:44:42.0142871Z"",""state"":{""messages"":6,""bytes"":246,""first_seq"":1,""first_ts"":""2023-07-10T14:46:18.4292666Z"",""last_seq"":6,""last_ts"":""2023-07-10T15:09:40.3502807Z"",""num_subjects"":1,""consumer_count"":0},""cluster"":{""leader"":""NABHDZUU6XON6DKACZVJK4U6PIDX35VZWC4SXB424NX3RFKXOGF52VMF""}}";
        _sequence = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(json));

    }
    
    [Benchmark]
    public JsonTypeStruct UsingStruct()
    {
        var reader = new Utf8JsonReader(_sequence);
        return JsonSerializer.Deserialize<JsonTypeStruct>(ref reader);
    }
    
    [Benchmark]
    public JsonTypeClass UsingClass()
    {
        var reader = new Utf8JsonReader(_sequence);
        return JsonSerializer.Deserialize<JsonTypeClass>(ref reader);
    }
}