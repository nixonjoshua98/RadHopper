using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RadHopper.Consumers;
using RadHopper.DependencyInjection;
using RadHopper.Transport;
using RadHopper.Transport.Exceptions;

namespace RadHopper.RabbitMQ;

internal interface IConsumerDescriptor
{
    Task SetupConnection(IConnection connection, IServiceProvider serviceProvider, TransportConfig config);
}

internal class ConsumerDescriptor<TM> : IConsumerDescriptor
where TM : class
{
    private ILogger<ConsumerDescriptor<TM>>? _logger;
    private readonly string _queueName;
    private IChannel? _channel;
    
    private readonly IConsumerBehavior<TM> _behavior;
    
    private CancellationTokenSource? _cancellationTokenSource;

    public ConsumerDescriptor(string queue, IConsumerBehavior<TM> behavior)
    {
        _queueName = queue;
        _behavior = behavior;
        _channel = null;
    }

    public async Task SetupConnection(IConnection connection, IServiceProvider serviceProvider, TransportConfig config)
    {
        if (_channel != null)
            return;
        
        _cancellationTokenSource = new CancellationTokenSource();
        _channel = await connection.CreateChannelAsync();
        
        _logger =  serviceProvider.GetService<ILogger<ConsumerDescriptor<TM>>>();
        
        _behavior.RegisterCompletionCallback(async (message) =>
        {
            // This would be a problem... But it should never happen.
            // For default behavior this error will cause message rejection.
            if (message is not RabbitMqMessage<TM> rabbitMessage)
                throw new ConnectorException("Failed to find delivery tag to ack!");

            await _channel.BasicAckAsync(rabbitMessage.DeliveryTag, false);
        });
        
        _behavior.RegisterErrorCallback(async (message) =>
        {
            // This would be a problem... But it should never happen.
            // For default behavior this error will cause message rejection.
            if (message is not RabbitMqMessage<TM> rabbitMessage)
                throw new ConnectorException("Failed to find delivery tag to ack!");
            
            if (rabbitMessage.Redelivered)
                _logger?.LogError("Failed to consume message after redelivery! Discarding {tag}!", rabbitMessage.DeliveryTag);

            var requeue = !rabbitMessage.Redelivered && config.RequeueOnError;

            if (config.NeverDiscard && !requeue)
            {
                _logger?.LogCritical($"Message acknowledgement faiiled because NeverDiscard is enabled but requeue is not an option! Messages will be left unacknowledged!");
                return;
            }
            
            await _channel.BasicNackAsync(rabbitMessage.DeliveryTag, false, requeue);
        });
        
        // Written with subtraction so that we don't have to cast to int (no real benefit from doing one way or the other, it just looks nicer to me)
        if (_behavior.PrefetchHint - ushort.MaxValue <= 0)
        {
            ushort hint = (ushort)_behavior.PrefetchHint;
            await _channel.BasicQosAsync(0, hint, false);
        }
        
        await _channel.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false,
            arguments: null);
        
        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                var body = ea.Body.ToArray();
                
                var messageString = Encoding.UTF8.GetString(body);
                
                var message = JsonSerializer.Deserialize<TM>(messageString);

                if (message == null)
                    throw new ConnectorException("Failed to deserialize message");
                
                var wrappedMessage = new RabbitMqMessage<TM>()
                {
                    DeliveryTag = ea.DeliveryTag,
                    Message = message,
                    Headers = new Dictionary<string, string>(), // TODO
                    CancellationToken = _cancellationTokenSource.Token,
                    Redelivered = ea.Redelivered,
                };

                await _behavior.Consume(wrappedMessage);
            }
            catch (Exception ex)
            {
                // Behavior shouldn't throw... So this means deserialize likely failed.
                // Reject the message in this case.
                await _channel.BasicRejectAsync(deliveryTag: ea.DeliveryTag, false);
                _logger?.LogError(ex, $"Failure while receiving message! Rejecting!");
            }
        };
        
        await _channel.BasicConsumeAsync(_queueName, autoAck: false, consumer: consumer);
    }
    
}