namespace LoadBalancer.Throttling;

public interface IThrottlingStrategy
{
    bool TryProcessRequest();
}