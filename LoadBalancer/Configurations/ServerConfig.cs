namespace LoadBalancer.Configurations;

public record ServerConfig
{
    public string Url { get; init; } = String.Empty;
}