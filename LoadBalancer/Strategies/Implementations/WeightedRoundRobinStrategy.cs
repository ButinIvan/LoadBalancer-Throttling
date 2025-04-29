using LoadBalancer.Configurations;

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

    public WeightedRoundRobinStrategy(ServerConfig[] servers)
    {
        if (servers == null || servers.Length == 0)
            throw new ArgumentException("Servers list cannot be null or empty");
        
        _servers = servers;
        _weights = servers.Select(s => s.Weight > 0 ? s.Weight : 1).ToArray();
        _maxWeight = _weights.Max();
        _gcd = CalculateGCD(_weights);
    }

    public ServerConfig GetNextServer()
    {
        lock (_lock)
        {
            while (true)
            {
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
                    return _servers[_currentIndex];
                }
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