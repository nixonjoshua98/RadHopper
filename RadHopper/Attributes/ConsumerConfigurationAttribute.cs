namespace RadHopper.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class ConsumerConfigurationAttribute : Attribute
{
    public int? BatchSize { get; }
    public int? WaitTimeMs { get; }

    public ConsumerConfigurationAttribute(int batchSize = 0, int waitTimeMs = 0)
    {
        BatchSize = batchSize > 0 ? batchSize : null;
        WaitTimeMs = waitTimeMs > 0 ? waitTimeMs : null;
    }
}