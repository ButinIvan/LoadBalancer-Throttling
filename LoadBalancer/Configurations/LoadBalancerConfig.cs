namespace LoadBalancer.Configurations;

public record LoadBalancerConfig
{
    public string Strategy { get; init; } = "RoundRobin";

    public TimeSpan Duration { get; init; } = TimeSpan.FromSeconds(30);
    
    public ServerConfig[] Servers { get; init; } = Array.Empty<ServerConfig>();
}