using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RadHopper.Transport;

namespace RadHopper;

public class RadHopperHostedService : IHostedService
{
    private readonly ILogger<RadHopperHostedService> _logger;
    
    private readonly IServiceProvider _serviceProvider;
    
    private static readonly IList<ITransportLayer> Layers = new List<ITransportLayer>();
    
    public RadHopperHostedService(IServiceProvider serviceProvider, ILogger<RadHopperHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var layer in Layers)
        {
            _logger.LogInformation($"Configuring transport: {layer.GetType().FullName}");
            await layer.SetupConnection(_serviceProvider);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping RadHopper");
        return Task.CompletedTask;
    }

    internal static void AddTransport(ITransportLayer layer)
    {
        Layers.Add(layer);
    }
}