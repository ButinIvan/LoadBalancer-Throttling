using System.Collections.Concurrent;
using LoadBalancer.Configurations;

namespace LoadBalancer.Strategies.Implementations;

public class LeastConnectionsStrategy :ILoadBalancerStrategy
{
    private readonly ServerConfig[] _servers;
    private readonly ConcurrentDictionary<string, int> _connectionCounts = new();
    private readonly object _lock = new();

    public LeastConnectionsStrategy(ServerConfig[] servers)
    {
        _servers = servers;
        foreach (var server in servers)
        {
            _connectionCounts[server.Url] = 0;
        }
    }
    
    public ServerConfig GetNextServer()
    {
        lock (_lock)
        {
            var targetServer = _servers
                .OrderBy(s => _connectionCounts[s.Url])
                .First();

            _connectionCounts.AddOrUpdate(
                targetServer.Url,
                1,
                (_, count) => count + 1);

            return targetServer;
        }
    }

    public void ReleaseConnection(ServerConfig server)
    {
        _connectionCounts.AddOrUpdate(
            server.Url,
            0,
            (_, count) => Math.Max(0, count - 1));
    }
}