using LoadBalancer.Configurations;

namespace LoadBalancer.Strategies;

public class RoundRobinStrategy : ILoadBalancerStrategy
{
    private readonly ServerConfig[] _servers;
    private int _currentIndex;
    private readonly object _lock = new();

    public RoundRobinStrategy(ServerConfig[] servers)
    {
        _servers = servers;
    }

    public ServerConfig GetNextServer()
    {
        lock (_lock)
        {
            var server = _servers[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _servers.Length;
            return server;
        }
    }
}