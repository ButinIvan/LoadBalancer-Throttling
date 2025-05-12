namespace LoadBalancer.Throttling.Implementations;

public class RejectingSlidingWindowStrategy : IThrottlingStrategy
{
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly object _lock = new();
    private readonly TimeSpan _windowSize;
    private readonly int _requestLimit;

    public RejectingSlidingWindowStrategy(TimeSpan windowSize, int requestLimit)
    {
        _windowSize = windowSize;
        _requestLimit = requestLimit;
    }

    public bool TryProcessRequest()
    {
        var now = DateTime.UtcNow;
        var windowStart = now - _windowSize;

        lock (_lock)
        {
            while (_requestTimestamps.Count > 0 && _requestTimestamps.Peek() < windowStart)
            {
                _requestTimestamps.Dequeue();
            }
            
            if (_requestTimestamps.Count >= _requestLimit)
            {
                return false;
            }
            
            _requestTimestamps.Enqueue(now);
            return true;
        }
    }
}