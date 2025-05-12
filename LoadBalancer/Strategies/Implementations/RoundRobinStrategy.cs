using System.Diagnostics;
using LoadBalancer.Configurations;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Strategies.Implementations;

public class RoundRobinStrategy : ILoadBalancerStrategy
{
    private readonly ServerConfig[] _servers;
    private readonly ILogger _logger;
    private int _currentIndex;
    private readonly object _lock = new();

    public RoundRobinStrategy(ServerConfig[] servers, ILogger logger)
    {
        _servers = servers;
        _logger = logger;
        _logger.Debug("Initialized with {ServerCount} servers", servers.Length);
    }

    public ServerConfig GetNextServer()
    {
        lock (_lock)
        {
            try
            {
                var server = _servers[_currentIndex];
                _currentIndex = (_currentIndex + 1) % _servers.Length;
            
                _logger.Information("Selected server {ServerUrl} (Index: {Index}, TraceId: {TraceId})",
                    server.Url,
                    _currentIndex,
                    Activity.Current?.TraceId);
            
                return server;
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error in RoundRobinStrategy.GetNextServer");
                throw;
            }
            
        }
    }
}