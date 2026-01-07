using RabbitMQ.Client;
using RadHopper.Abstractions;
using RadHopper.Attributes;
using RadHopper.Consumers;
using RadHopper.RabbitMQ.Abstractions;
using System;

namespace RadHopper.RabbitMQ.Configuration;

internal sealed class RabbitMqTransportConfigurator : IRabbitMqTransportConfigurator
{
    public readonly ITransportConfigurator Configurator;
    public readonly List<Func<IServiceProvider, IConsumerDescriptor>> ConsumerDescriptorFactories = [];

    public string? Host { get; set; }

    public int Port { get; set; } = 5672;

    public RabbitMqTransportConfigurator(ITransportConfigurator configurator)
    {
        Configurator = configurator;
    }

    public ConnectionFactory CreateConnectionFaactory()
    {
        ArgumentException.ThrowIfNullOrEmpty(Host, nameof(Host));

        return new ConnectionFactory
        {
            HostName = Host,
            Port = Port
        };
    }

    public IRabbitMqTransportConfigurator AddReceiveEndpoint<C, TM>(string? queueName = null) where TM : class
        where C : IConsumerRoot<TM>
    {
        ConsumerDescriptorFactories.Add(provider =>
        {
            var name = GetMessageQueueName<TM>(queueName);

            var behavior = Configurator.BehaviorFactory.Create<C, TM>(provider, Configurator);

            return new ConsumerDescriptor<TM>(name, behavior);
        });

        return this;
    }

    static string GetMessageQueueName<T>(string? queueName)
    {
        return string.IsNullOrEmpty(queueName) ?
            OnQueueAttribute.GetQueueName(typeof(T)) :
            queueName;
    }

    public IRabbitMqTransportConfigurator WithHost(string host)
    {
        Host = host;

        return this;
    }

    public IRabbitMqTransportConfigurator WithPort(int port)
    {
        Port = port;

        return this;
    }
}
