namespace RadHopper.Consumers;

internal interface IConsumerBehavior<TM>
    where TM : class
{
    Task Consume(HopMessage<TM> message);
    void RegisterCompletionCallback(Func<HopMessage<TM>, Task> callback);
    void RegisterErrorCallback(Func<HopMessage<TM>, Task> callback);
    int PrefetchHint { get; }
}