namespace LoadBalancer.Configurations;

public record LoadBalancerConfig
{
    public string Strategy { get; init; } = "RoundRobin";
    public ServerConfig[] Servers { get; init; } = Array.Empty<ServerConfig>();
}