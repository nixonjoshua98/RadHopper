using RadHopper.Transport;

namespace RadHopper.Consumers.BehaviorFactory;

internal interface IBehaviorFactory
{
    internal IConsumerBehavior<TM> Create<C, TM>(IServiceProvider serviceProvider, TransportConfig config)
        where C : IConsumerRoot<TM>
        where TM : class;
}