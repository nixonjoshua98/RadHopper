using RadHopper.Attributes;
using RadHopper.Consumers.BehaviorFactory;

namespace RadHopper.Transport;

public class TransportConfig
{
    internal TransportConfig(IBehaviorFactory behaviorFactory)
    {
        BehaviorFactory = behaviorFactory;
    }
    
    public int? DefaultBatchSize { get; set; }
    public int? DefaultWaitTimeMs { get; set; }
    public bool RequeueOnError { get; set; } = true;
    public bool NeverDiscard { get; set; } = false;

    internal int GetBatchSize(Type consumerType)
    {
        var config = consumerType
            .GetCustomAttributes(typeof(ConsumerConfigurationAttribute), true)
            .Cast<ConsumerConfigurationAttribute>()
            .FirstOrDefault();

        var result = config?.BatchSize ?? DefaultBatchSize ?? Environment.ProcessorCount;
        return result > 0 ? result : 1;
    }
    
    internal int GetWaitTimeMs(Type consumerType)
    {
        var config = consumerType
            .GetCustomAttributes(typeof(ConsumerConfigurationAttribute), true)
            .Cast<ConsumerConfigurationAttribute>()
            .FirstOrDefault();

        var result = config?.WaitTimeMs ?? DefaultWaitTimeMs ?? 1000;
        return result > 0 ? result : 1000;
    }
    
    internal IBehaviorFactory BehaviorFactory { get; private set; }
}