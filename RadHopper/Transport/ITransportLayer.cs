namespace RadHopper.Transport;

public interface ITransportLayer
{
    Task SetupConnection(IServiceProvider sp);
    IPublisher GetPublisher();
}