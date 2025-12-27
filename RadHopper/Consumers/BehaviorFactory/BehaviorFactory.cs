using RadHopper.Consumers.Behaviors;
using RadHopper.Transport;

namespace RadHopper.Consumers.BehaviorFactory;

internal class BehaviorFactory : IBehaviorFactory
{
    IConsumerBehavior<TM> IBehaviorFactory.Create<C, TM>(IServiceProvider serviceProvider, TransportConfig config)
    {
        var consumerType = typeof(C);
        
        if (typeof(IConsumer<TM>).IsAssignableFrom(consumerType))
            return new DefaultBehavior<TM>(serviceProvider, consumerType, config);
        
        if (typeof(IBatchConsumer<TM>).IsAssignableFrom(consumerType))
            return new BatchBehavior<TM>(serviceProvider, consumerType, config);

        throw new InvalidOperationException("Cannot create behavior for " + consumerType);
    }
}