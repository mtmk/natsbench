namespace client1;

/// <summary>
/// Atomic Boolean
/// </summary>
/// <example>
/// Define using the explicit operator:
/// <code>
/// var ack = (AtomicBool)false;
/// </code>
/// </example>
/// <example>
/// Test directly in if statements:
/// <code>
/// if (ack)
///     DoAck();
/// </code>
/// </example>
public class AtomicBool
{
    private int _i;

    public AtomicBool(bool value = false) => _i = value ? 1 : 0;

    public void Set(bool value = true) => Interlocked.Exchange(ref _i, value ? 1 : 0);
        
    public void Toggle() => Set(!True);

    public bool True => Volatile.Read(ref _i) == 1;
        
    public static implicit operator bool(AtomicBool a) => a.True;
        
    public static explicit operator AtomicBool(bool b) => new(b);

    public override string ToString() => True ? "ON" : "OFF";
}