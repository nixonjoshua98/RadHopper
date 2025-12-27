namespace RadHopper.Transport;

public interface IPublisher
{
    Task Publish<TO>(TO data, CancellationToken? cancellationToken = null);
    Task Publish<TO>(string queue, TO data, CancellationToken? cancellationToken = null);
    Task PublishRaw(string queue, string data, CancellationToken? cancellationToken = null);
}