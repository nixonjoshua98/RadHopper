using RabbitMQ.Client;
using RadHopper.Attributes;
using RadHopper.Consumers;
using RadHopper.Transport;
using RadHopper.Transport.Exceptions;

namespace RadHopper.RabbitMQ;

public class RabbitMqTransport : ITransportLayer
{
    
    private readonly TransportConfig _config;
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    
    private readonly List<IConsumerDescriptor> _consumers;
    private readonly List<Action<IServiceProvider>> _actions;

    public RabbitMqTransport(TransportConfig config, ConnectionFactory factory)
    {
        _config = config;
        _factory = factory;
        _connection = null;
        _consumers = new List<IConsumerDescriptor>();
        _actions = new List<Action<IServiceProvider>>();
    }

    public void AddReceiveEndpoint<C, TM>(string? queueName = null) where TM : class
        where C : IConsumerRoot<TM>
    {
        string queue;
        if (queueName == null)
            queue = OnQueueAttribute.GetQueueName(typeof(TM));
        else
            queue = queueName;
        
        _actions.Add((sp) =>
        {
            var behavior = _config.BehaviorFactory.Create<C, TM>(sp, _config);
            var consumer = new ConsumerDescriptor<TM>(queue, behavior);
            
            _consumers.Add(consumer);
        });
    }

    async Task ITransportLayer.SetupConnection(IServiceProvider sp)
    {
        if (_connection != null)
            throw new ConnectorException("Already initialized");
        _connection = await _factory.CreateConnectionAsync();
        
        foreach (var action in _actions)
            action(sp);
        _actions.Clear();

        foreach (var consumer in _consumers)
        {
            await consumer.SetupConnection(_connection, sp, _config);
        }
    }

    public IPublisher GetPublisher()
    {
        return new RabbitMqPublisher(_factory);
    }
}