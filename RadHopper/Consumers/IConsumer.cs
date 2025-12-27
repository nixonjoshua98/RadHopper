namespace RadHopper.Consumers;

public interface IConsumer<T> : IConsumerRoot<T>
where T : class
{
    Task Consume(HopMessage<T> message);
}