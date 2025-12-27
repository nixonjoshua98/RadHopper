namespace RadHopper.Consumers;

public interface IBatchConsumer<T> : IConsumerRoot<T>
where T : class
{
    Task Consume(HopMessage<T>[] context);
}