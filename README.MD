# RadHopper

RadHopper is a lightweight .NET package for registering and running message consumers.  
It currently supports **RabbitMQ**, with additional transports planned.

The focus is on simple consumer registration and clear, explicit usage.

# Basic Usage

Register RadHopper and configure the RabbitMQ transport during startup:
```C#
services.AddRadHopper(config =>
{
    // Batch size for batch consumers, otherwise used as a concurrency limit
    config.DefaultBatchSize = 100; // Default = ProcessorCount

    // Maximum wait time (in ms) before a batch is consumed
    config.DefaultWaitTimeMs = 500; // Default = 1 second

    // Whether messages should be requeued when an error occurs
    config.RequeueOnError = true; // Default = true

    // When false, messages may be discarded after being requeued once
    // When true, messages are left unacknowledged instead
    config.NeverDiscard = false; // Default = false.

    var factory = new ConnectionFactory // RabbitMQ.Client.ConnectionFactory
    {
        HostName = "localhost"
    };

    var transport = new RabbitMqTransport(config, factory);

    transport.AddReceiveEndpoint<SampleConsumer, SampleMessage>();

    return transport;
});
```

# Consumers
`ConsumerConfigurationAttribute` is optional.
Any values provided override the defaults from the RadHopper configuration.

## Single Message Consumer
```C#
[ConsumerConfiguration(batchSize: 10)]
public class SampleConsumer : IConsumer<SampleMessage>
{
    public Task Consume(HopMessage<SampleMessage> message)
    {
        // Handle message
        return Task.CompletedTask;
    }
}
```

## Batch consumers
```C#
[ConsumerConfiguration(batchSize: 100, waitTimeMs: 500)]
public class SampleConsumer : IBatchConsumer<SampleMessage>
{
    public Task Consume(HopMessage<SampleMessage>[] context)
    {
        Console.WriteLine($"Messages received: {context.Length}");
        return Task.CompletedTask;
    }
}
```

# Messages & queues

## Example:
```C#
[OnQueue("sample-queue")]
public record SampleMessage
{
    public Guid Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Payload { get; init; } = string.Empty;
}
```

## Queue naming
The onQueue attribute is used to set the queue name. This attribute is optional and by default the FullName of the type will be used.

You also have the option to specify the queue name explicitly when calling AddReceiveEndpoint or when publishing a message.

```C#
transport.AddReceiveEndpoint<SampleConsumer, SampleMessage>(
    "optional-queue-name"
);
```

# Publishing Messages
After calling AddRadHopper an `IPublisher` will be registered as a singleton.
```C#
public interface IPublisher
{
    Task Publish<TO>(TO data, CancellationToken? cancellationToken = null);
    Task Publish<TO>(string queue, TO data, CancellationToken? cancellationToken = null);
    Task PublishRaw(string queue, string data, CancellationToken? cancellationToken = null);
}
```

Usage:
```C#
await publisher.Publish(new SampleMessage
{
    Id = Guid.NewGuid(),
    CreatedAt = DateTime.UtcNow,
    Payload = "Hello world"
});

// Or specify the queue name explicitly.
await publisher.Publish("optional-queue-name", message);
```

# Transport Support
* RabbitMQ ✅

# ⚠️ **MVP / Early Preview**
> RadHopper is currently in **MVP (Minimum Viable Product)** stage.
> 
> The public API may change, features may be added or adjusted, and not all edge cases are fully handled yet.  
> Feedback and experimentation are encouraged, but it is not recommended for critical production workloads at this stage.
