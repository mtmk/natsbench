public partial class StreamInfoResponse
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

public partial class Cluster
{
    [JsonPropertyName("leader")]
    public string Leader { get; set; }
}

public partial class Config
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

public partial class State
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
