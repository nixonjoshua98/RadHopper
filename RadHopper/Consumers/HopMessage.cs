namespace RadHopper.Consumers;

public class HopMessage<T>
where T : class
{
    public CancellationToken CancellationToken { get; internal set; }
    public Dictionary<string, string> Headers { get; internal set; }
    public T Message { get; internal set; }
}