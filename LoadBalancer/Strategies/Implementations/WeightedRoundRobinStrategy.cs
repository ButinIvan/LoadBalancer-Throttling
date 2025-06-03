using System.Diagnostics;
using LoadBalancer.Configurations;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Strategies.Implementations;

public class WeightedRoundRobinStrategy : ILoadBalancerStrategy
{
    private readonly ServerConfig[] _servers;
    private readonly int[] _weights;
    private int _currentIndex = -1;
    private int _currentWeight;
    private readonly int _maxWeight;
    private readonly int _gcd;
    private readonly object _lock = new();
    private readonly ILogger _logger;

    public WeightedRoundRobinStrategy(ServerConfig[] servers, ILogger logger)
    {
        if (servers == null || servers.Length == 0)
            throw new ArgumentException("Servers list cannot be null or empty");
        
        _servers = servers;
        _weights = servers.Select(s => s.Weight > 0 ? s.Weight : 1).ToArray();
        _maxWeight = _weights.Max();
        _gcd = CalculateGCD(_weights);
        _logger = logger;
        
        _logger.Debug("Initialized with {ServerCount} servers, MaxWeight: {MaxWeight}, GCD: {GCD}",
            servers.Length, _maxWeight, _gcd);
    }

    public ServerConfig GetNextServer()
    {
        lock (_lock)
        {
            try
            {
                var iterations = 0;
                var maxIterations = _servers.Length * (_maxWeight / _gcd + 1);
                while (true)
                {
                    if (iterations++ > maxIterations)
                    {
                        _logger.Error("Infinite loop detected in WeightedRoundRobinStrategy");
                        throw new InvalidOperationException("Possible infinite loop detected in server selection");
                    }
                    _currentIndex = (_currentIndex + 1) % _servers.Length;
                    if (_currentIndex == 0)
                    {
                        _currentWeight -= _gcd;
                        if (_currentWeight <= 0)
                        {
                            _currentWeight = _maxWeight;
                        }
                    }

                    if (_weights[_currentIndex] >= _currentWeight)
                    {
                        var selectedServer = _servers[_currentIndex];
                        
                        _logger.Information(
                            "Selected server {ServerUrl} (Index: {Index}, Weight: {Weight}, CurrentWeight: {CurrentWeight})",
                            selectedServer.Url,
                            _currentIndex,
                            _weights[_currentIndex],
                            _currentWeight);
                        
                        return selectedServer;
                    }
                }
            }
            catch (Exception e)
            {
                _logger.Error(e, "Error in WeightedRoundRobinStrategy.GetNextServer");
                throw;
            }
        }
    }

    private static int CalculateGCD(int[] numbers)
    {
        return numbers.Aggregate(numbers[0], (current, number) => GCD(current, number));
    }

    private static int GCD(int a, int b)
    {
        return b == 0 ? a : GCD(b, a % b);
    }
}