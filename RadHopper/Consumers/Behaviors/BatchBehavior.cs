using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadHopper.DependencyInjection;
using RadHopper.Transport;

namespace RadHopper.Consumers.Behaviors;

public class BatchBehavior<TM> : IConsumerBehavior<TM>
    where TM : class
{
    private readonly ILogger<BatchBehavior<TM>>? _logger;
    private readonly Type _consumerType;
    private readonly IServiceProvider _serviceProvider;
    private Func<HopMessage<TM>, Task>? _onCompletion;
    private Func<HopMessage<TM>, Task>? _onError;
    
    private readonly ConcurrentQueue<HopMessage<TM>> _messages;
    private readonly int _maxSize;
    private readonly int _waitTimeMs;
    private Task _flushTask;
    private Task _timedTask;

    public BatchBehavior(IServiceProvider serviceProvider, Type consumerType, TransportConfig config)
    {
        _consumerType = consumerType;
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetService<ILogger<BatchBehavior<TM>>>();
        _onCompletion = null;
        _onError = null;
        _maxSize = config.GetBatchSize(consumerType);
        _waitTimeMs = config.GetWaitTimeMs(consumerType);
        _flushTask = Task.CompletedTask;
        _timedTask = Task.CompletedTask;
        PrefetchHint = _maxSize * 2;
        _messages = new ConcurrentQueue<HopMessage<TM>>();
    }

    public async Task Consume(HopMessage<TM> message)
    {
        try
        {
            while (!IsReady())
            {
                lock (_messages)
                {
                    if (_flushTask.IsCompleted)
                    {
                        _flushTask = Flush();
                    }
                }

                await Task.Delay(1);
            }
            
            lock (_messages)
                _messages.Enqueue(message);

            lock (_messages)
            {
                if (_timedTask.IsCompleted)
                    _timedTask = TimedTask();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while queueing message!");
            if (_onError != null)
                await _onError(message);
        }
    }

    private async Task TimedTask()
    {
        bool running = true;
        while (running)
        {
            await Task.Delay(_waitTimeMs);
            lock (_messages)
            {
                if (_flushTask.IsCompleted)
                {
                    _flushTask = Flush();
                    running = false;
                }
            }
        }
    }
    
    private async Task Flush()
    {
        lock (_messages)
        {
            if (_messages.IsEmpty)
                return;
        }
        
        using var serviceScope = _serviceProvider.CreateScope();

        IBatchConsumer<TM> messageTarget;
        try
        {
            messageTarget = TypeHelper.ConstructType<IBatchConsumer<TM>>(_consumerType, serviceScope.ServiceProvider);
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Failed to construct consumer! Messages will be kept in the queue until this is fixed!");
            /*
             * This will cause the messages to not be acknowledged and will cause the queue to get stuck. Until the program ends.
             * But since this is 100% a configuration error that is better than throwing away the message.
             */
            return;
        }
        
        var toProcess = new List<HopMessage<TM>>();
        lock (_messages)
        {
            while (_messages.TryDequeue(out var message))
            {
                toProcess.Add(message);
            }
        }

        // Constructed the consumer for nothing...
        if (toProcess.Count == 0)
            return;

        try
        {
            await messageTarget.Consume(toProcess.ToArray());
            
            if (_onCompletion != null)
            {
                foreach (var message in toProcess)
                {
                    try
                    {
                        await _onCompletion(message);
                    }
                    catch (Exception ex1)
                    {
                        _logger?.LogError(ex1, "Message ack failed!");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Message consume failed!");
            if (_onError != null)
            {
                foreach (var message in toProcess)
                {
                    try
                    {
                        await _onError(message);
                    }
                    catch (Exception ex1)
                    {
                        _logger?.LogError(ex1, "Message rejection failed!");
                    }
                }
            }
        }
    }
    
    private bool IsReady()
    {
        lock (_messages)
            return _messages.Count < _maxSize;
    }

    public void RegisterCompletionCallback(Func<HopMessage<TM>, Task> callback)
    {
        _onCompletion = callback;
    }

    public void RegisterErrorCallback(Func<HopMessage<TM>, Task> callback)
    {
        _onError = callback;
    }

    public int PrefetchHint { get; private set; }
}