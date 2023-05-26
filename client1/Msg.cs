namespace client1;

public record struct Msg
{
    public string Subject { get; set; }
    public int Sid { get; set; }
    public string ReplyTo { get; set; }
    public string Payload { get; set; }
    public string[] Headers { get; set; }
}