// See https://aka.ms/new-console-template for more information

using System.Buffers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using NATS.Client.Core;

class Program
{
    static async Task Main()
    {
        await using var nats = new NatsConnection();
        // await using var sub = await nats.SubscribeAsync("foo", new NatsSubOpts{MaxMsgs = 2});
        // await nats.PublishAsync("foo", 123);
        // await nats.PublishAsync("foo", 456);
        // await foreach (var msg in sub.Msgs.ReadAllAsync())
        // {
        //     Console.WriteLine(Encoding.ASCII.GetString(msg.Data.Span));
        // }

        {
            var response = await nats.ReqJson(subject: "$JS.API.STREAM.INFO.events");
            Console.WriteLine(response);
        }
        {
            var response = await nats.ReqJson(subject: "$JS.API.INFO");
            Console.WriteLine(response);
        }
    }
}

public static class NatsExtensions
{
    public static async Task<string> ReqStr(this NatsConnection nats, string subject, string? request = default)
    {
        ReadOnlySequence<byte> payload = new ReadOnlySequence<byte>();
        if (!string.IsNullOrWhiteSpace(request))
            payload = new ReadOnlySequence<byte>(Encoding.UTF8.GetBytes(request));
        
        var msg = await nats.RequestAsync(subject, payload: payload);
        
        if (msg == null)
            return string.Empty;
        
        return Encoding.UTF8.GetString(msg.Value.Data.Span);
    }

    public static async Task<string> ReqJson(this NatsConnection nats, string subject, string? request = default)
    {
        var json = await nats.ReqStr(subject, request);
        var jsonNode = JsonNode.Parse(json);
        return jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }
}


public record StreamInfoResponse
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("total")]
        public long Total { get; set; }

        [JsonPropertyName("offset")]
        public long Offset { get; set; }

        [JsonPropertyName("limit")]
        public long Limit { get; set; }

        [JsonPropertyName("config")]
        public Config Config { get; set; }

        [JsonPropertyName("created")]
        public DateTimeOffset Created { get; set; }

        [JsonPropertyName("state")]
        public State State { get; set; }

        [JsonPropertyName("cluster")]
        public Cluster Cluster { get; set; }
    }

    public record Cluster
    {
        [JsonPropertyName("leader")]
        public string Leader { get; set; }
    }

    public record Config
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("subjects")]
        public string[] Subjects { get; set; }

        [JsonPropertyName("retention")]
        public string Retention { get; set; }

        [JsonPropertyName("max_consumers")]
        public long MaxConsumers { get; set; }

        [JsonPropertyName("max_msgs")]
        public long MaxMsgs { get; set; }

        [JsonPropertyName("max_bytes")]
        public long MaxBytes { get; set; }

        [JsonPropertyName("max_age")]
        public long MaxAge { get; set; }

        [JsonPropertyName("max_msgs_per_subject")]
        public long MaxMsgsPerSubject { get; set; }

        [JsonPropertyName("max_msg_size")]
        public long MaxMsgSize { get; set; }

        [JsonPropertyName("discard")]
        public string Discard { get; set; }

        [JsonPropertyName("storage")]
        public string Storage { get; set; }

        [JsonPropertyName("num_replicas")]
        public long NumReplicas { get; set; }

        [JsonPropertyName("duplicate_window")]
        public long DuplicateWindow { get; set; }

        [JsonPropertyName("allow_direct")]
        public bool AllowDirect { get; set; }

        [JsonPropertyName("mirror_direct")]
        public bool MirrorDirect { get; set; }

        [JsonPropertyName("sealed")]
        public bool Sealed { get; set; }

        [JsonPropertyName("deny_delete")]
        public bool DenyDelete { get; set; }

        [JsonPropertyName("deny_purge")]
        public bool DenyPurge { get; set; }

        [JsonPropertyName("allow_rollup_hdrs")]
        public bool AllowRollupHdrs { get; set; }
    }

    public record State
    {
        [JsonPropertyName("messages")]
        public long Messages { get; set; }

        [JsonPropertyName("bytes")]
        public long Bytes { get; set; }

        [JsonPropertyName("first_seq")]
        public long FirstSeq { get; set; }

        [JsonPropertyName("first_ts")]
        public DateTimeOffset FirstTs { get; set; }

        [JsonPropertyName("last_seq")]
        public long LastSeq { get; set; }

        [JsonPropertyName("last_ts")]
        public DateTimeOffset LastTs { get; set; }

        [JsonPropertyName("num_subjects")]
        public long NumSubjects { get; set; }

        [JsonPropertyName("consumer_count")]
        public long ConsumerCount { get; set; }
    }