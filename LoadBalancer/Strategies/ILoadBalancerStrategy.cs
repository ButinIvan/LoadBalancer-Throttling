using LoadBalancer.Configurations;

namespace LoadBalancer.Strategies;

public interface ILoadBalancerStrategy
{
    ServerConfig GetNextServer();
}