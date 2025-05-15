using LoadBalancer.Configurations;
using LoadBalancer.Strategies;
using LoadBalancer.Strategies.Implementations;
using LoadBalancer.Throttling;
using LoadBalancer.Throttling.Implementations;
using Microsoft.Extensions.Options;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Extensions;

public static class ServiceRegistration
{
    public static void AddLoadBalancerStrategy(this IServiceCollection services, IConfiguration configuration) 
    {
        services.Configure<LoadBalancerConfig>(configuration.GetSection("LoadBalancer"));

        services.AddSingleton<ILoadBalancerStrategy>(provider =>
        {
            var config = provider.GetRequiredService<IOptions<LoadBalancerConfig>>().Value;
            var httpAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            var logger = provider.GetRequiredService<ILogger>();

            if (config.Servers.Length == 0)
            {
                logger.Fatal("The server list cannot be empty. Check the configuration in config.json. ");
                throw new ArgumentException("Invalid config.json: server list is empty.");
            }
            
            return config.Strategy switch
            {
                "RoundRobin" => new RoundRobinStrategy(
                    config.Servers,
                    logger),
                "WeightedRoundRobin" => new WeightedRoundRobinStrategy(
                    config.Servers,
                    logger),
                "StickyRoundRobin" => new StickyRoundRobinStrategy(
                    config.Servers,
                    httpAccessor,
                    config.Duration,
                    logger),
                "IpHash" => new HashBasedStrategy(
                    config.Servers,
                    httpAccessor,
                    logger),
                "UrlHash" => new HashBasedStrategy(
                    config.Servers,
                    httpAccessor,
                    logger,
                    HashBasedStrategy.HashMode.Url),
                "LeastConnections" => new LeastConnectionsStrategy(
                    config.Servers,
                    logger),
                _ => new RoundRobinStrategy(
                    config.Servers,
                    logger)
            };
        });
    }

    public static void AddThrottlingStrategy(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ThrottlingConfig>(
            configuration.GetSection("Throttling"));

        services.AddSingleton<IThrottlingStrategy>(provider =>
        {
            var config = provider.GetRequiredService<IOptions<ThrottlingConfig>>().Value;
            var logger = provider.GetRequiredService<ILogger>();

            return config.Strategy switch
            { 
                "RejectingSlidingWindow" => new RejectingSlidingWindowStrategy(
                    config.WindowSize,
                    config.RequestLimit,
                    logger),
                _ => new RejectingSlidingWindowStrategy(
                    config.WindowSize,
                    config.RequestLimit,
                    logger),
            };
        });
    }
}