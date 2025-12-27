using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RadHopper.Attributes;
using RadHopper.Transport;

namespace RadHopper.RabbitMQ;

public class RabbitMqPublisher : IPublisher, IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IChannel? _channel;
    
    public RabbitMqPublisher(ConnectionFactory factory)
    {
        _factory = factory;
    }
    
    public Task Publish<TO>(TO data, CancellationToken? cancellationToken = null)
    {
        var queueName = OnQueueAttribute.GetQueueName(typeof(TO));
        return Publish(queueName, data, cancellationToken);
    }
    
    public Task Publish<TO>(string queue, TO data, CancellationToken? cancellationToken = null)
    {
        var serialized = JsonSerializer.Serialize(data);
        return PublishRaw(queue, serialized, cancellationToken);
    }

    public async Task PublishRaw(string queue, string data, CancellationToken? cancellationToken = null)
    {
        try
        {
            await _semaphore.WaitAsync();

            await SetupConnection();
            
            var bytes = Encoding.UTF8.GetBytes(data);
            if (cancellationToken != null)
                await _channel!.BasicPublishAsync(exchange: string.Empty, routingKey: queue, body: bytes,
                    cancellationToken.Value);
            else
                await _channel!.BasicPublishAsync(exchange: string.Empty, routingKey: queue, body: bytes);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SetupConnection()
    {
        if (_connection != null) return;
        IConnection? connection = null;
        IChannel? channel = null;
        try
        {
            connection = await _factory.CreateConnectionAsync();
            try
            {
                channel = await connection.CreateChannelAsync();
            }
            catch
            {
                channel?.Dispose();
                throw;
            }
        }
        catch
        {
            connection?.Dispose();
            throw;
        }

        _connection = connection;
        _channel = channel;
    }

    public void Dispose()
    {
        _connection?.Dispose();
        _channel?.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection != null) await _connection.DisposeAsync();
        if (_channel != null) await _channel.DisposeAsync();
    }
}