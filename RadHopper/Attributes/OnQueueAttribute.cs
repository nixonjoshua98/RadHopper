namespace RadHopper.Attributes;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public class OnQueueAttribute : Attribute
{
    private string QueueName { get; }

    public OnQueueAttribute(string queueName)
    {
        QueueName = queueName;
    }
    
    internal static string GetQueueName(Type t)
    {
        var attr = t
            .GetCustomAttributes(typeof(OnQueueAttribute), true)
            .Cast<OnQueueAttribute>()
            .FirstOrDefault();
        
        return attr?.QueueName ?? t.FullName ?? t.Name;
    }
}