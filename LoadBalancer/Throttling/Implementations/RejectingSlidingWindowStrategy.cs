using ILogger = Serilog.ILogger;

namespace LoadBalancer.Throttling.Implementations;

public class RejectingSlidingWindowStrategy : IThrottlingStrategy
{
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly object _lock = new();
    private readonly TimeSpan _windowSize;
    private readonly int _requestLimit;
    private readonly ILogger _logger;

    public RejectingSlidingWindowStrategy(
        TimeSpan windowSize,
        int requestLimit,
        ILogger logger)
    {
        _windowSize = windowSize;
        _requestLimit = requestLimit;
        _logger = logger;
        
        _logger.Debug("Initialized with WindowSize: {WindowSize}, RequestLimit: {RequestLimit}", 
            windowSize, requestLimit);
    }

    public bool TryProcessRequest()
    {
        var now = DateTime.UtcNow;
        var windowStart = now - _windowSize;

        lock (_lock)
        {
            int removedCount = 0;
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
            {
                _requestTimestamps.Dequeue();
                removedCount++;
            }
            
            if (removedCount > 0)
                _logger.Debug("Removed {RemovedCount} old request from window", removedCount);
            
            var currentCount = _requestTimestamps.Count;
            if (currentCount >= _requestLimit)
            {
                _logger.Warning("Request rejected (Current: {CurrentCount}, Limit: {RequestLimit})", 
                    currentCount, _requestLimit);
                return false;
            }
            
            _requestTimestamps.Enqueue(now);
            _logger.Debug("Request accepted (Current: {CurrentCount}, Limit: {RequestLimit})", 
                currentCount + 1, _requestLimit);
            return true;
        }
    }
}