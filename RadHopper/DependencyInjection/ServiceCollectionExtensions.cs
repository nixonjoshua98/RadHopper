using Microsoft.Extensions.DependencyInjection;
using RadHopper.Transport;

namespace RadHopper.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRadHopper(this IServiceCollection collection, Func<TransportConfig, ITransportLayer> configure)
    {
        var config = new TransportConfig(new Consumers.BehaviorFactory.BehaviorFactory());
        var transport = configure(config);
        
        RadHopperHostedService.AddTransport(transport);
        
        collection.AddHostedService<RadHopperHostedService>();

        collection.AddSingleton<IPublisher>((p) => transport.GetPublisher());

        return collection;
    }
}