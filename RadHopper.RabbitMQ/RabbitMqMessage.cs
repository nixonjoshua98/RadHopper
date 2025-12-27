using RadHopper.Consumers;

namespace RadHopper.RabbitMQ;

internal class RabbitMqMessage<T> : HopMessage<T>
    where T : class
{
    internal ulong DeliveryTag;
    internal bool Redelivered;
}