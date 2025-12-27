using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadHopper.DependencyInjection;
using RadHopper.Transport;

namespace RadHopper.Consumers.Behaviors;

public class DefaultBehavior<TM> : IConsumerBehavior<TM>
    where TM : class
{
    private readonly ILogger<DefaultBehavior<TM>>? _logger;
    private readonly Type _consumerType;
    private readonly IServiceProvider _serviceProvider;
    private Func<HopMessage<TM>, Task>? _onCompletion;
    private Func<HopMessage<TM>, Task>? _onError;

    private readonly int _maxSize;
    private readonly ConcurrentQueue<Task<(HopMessage<TM>, bool)>> _tasks;
    private Task _flushTask;

    public DefaultBehavior(IServiceProvider serviceProvider, Type consumerType, TransportConfig config)
    {
        _consumerType = consumerType;
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetService<ILogger<DefaultBehavior<TM>>>();
        _onCompletion = null;
        _onError = null;

        _tasks = new ConcurrentQueue<Task<(HopMessage<TM>, bool)>>();
        _maxSize = config.GetBatchSize(consumerType);
        _flushTask = Task.CompletedTask;
        PrefetchHint = _maxSize * 2;
    }

    public async Task Consume(HopMessage<TM> message)
    {
        try
        {
            while (!IsReady())
            {
                lock (_tasks)
                {
                    if (_flushTask.IsCompleted)
                    {
                        _flushTask = Flush();
                    }
                }
                
                await Task.Delay(1);
            }

            lock (_tasks)
            {
                var task = ConsumeTask(message);
                _tasks.Enqueue(task);
                
                // Edge case... If the consumer returns a completed task, then ConsumerTask(message) returns completed.
                // When this happens the flush task will not be started... So we have to do it here too.
                if (_flushTask.IsCompleted)
                {
                    _flushTask = Flush();
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while queueing message!");
            if (_onError != null)
                await _onError(message);
        }
    }

    private async Task<(HopMessage<TM>, bool)> ConsumeTask(HopMessage<TM> message)
    {
        using var serviceScope = _serviceProvider.CreateScope();

        IConsumer<TM> messageTarget;
        try
        {
            messageTarget = TypeHelper.ConstructType<IConsumer<TM>>(_consumerType, serviceScope.ServiceProvider);
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Failed to construct consumer! Messages will be kept in the queue until this is fixed!");
            /*
             * This is a configuration issue so we don't want messages to get lost in this case... The user should fix this problem ASAP.
             * To prevent messages from getting lost we will return a message without a delivery tag... This will cause another failure down the line, but that is more acceptable than rejecting the message.
             */
            return (new HopMessage<TM>(), false);
        }

        var success = true;
        try
        {
            // Force the task to run async even if messageTarget.Consume is a synchronous call.
            await Task.Yield();
            
            await messageTarget.Consume(message).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Message consume failed!");
            success = false;
        }
        
        lock (_tasks)
        {
            if (_flushTask.IsCompleted)
            {
                _flushTask = Flush();
            }
        }

        return (message, success);
    }

    private bool IsReady()
    {
        lock (_tasks)
            return _tasks.Count < _maxSize;
    }

    private async Task Flush()
    {
        int itemsToGo = _maxSize * 2;

        while (!_tasks.IsEmpty && itemsToGo > 0)
            await WaitForCurrentCompletion();
    }

    private async Task WaitForCurrentCompletion()
    {
        try
        {
            if (_tasks.TryDequeue(out var task))
            {
                var message = await task;

                if (message.Item2)
                {
                    if (_onCompletion != null)
                        await _onCompletion(message.Item1);
                }
                else
                {
                    if (_onError != null)
                        await _onError(message.Item1);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Message ack failed!");
        }
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