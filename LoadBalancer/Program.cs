using System.Net.Quic;
using LoadBalancer.Configurations;
using LoadBalancer.Strategies;
using LoadBalancer.Strategies.Implementations;
using LoadBalancer.Throttling;
using LoadBalancer.Throttling.Implementations;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);
builder.Services.Configure<LoadBalancerConfig>(
    builder.Configuration.GetSection("LoadBalancer"));
builder.Services.Configure<ThrottlingConfig>(
    builder.Configuration.GetSection("Throttling"));

builder.Services.AddSingleton<IThrottlingStrategy>(provider =>
{
    var config = provider.GetRequiredService<IOptions<ThrottlingConfig>>().Value;

    return config.Strategy switch
    {
        "RejectingSlidingWindow" => new RejectingSlidingWindowStrategy(
            config.WindowSize,
            config.RequestLimit),
        _ => new RejectingSlidingWindowStrategy(
            config.WindowSize,
            config.RequestLimit),
    };
});

builder.Services.AddSingleton<ILoadBalancerStrategy>(provider =>
{
    var config = provider.GetRequiredService<IOptions<LoadBalancerConfig>>().Value;
    var httpAccessor = provider.GetRequiredService<IHttpContextAccessor>();
    return config.Strategy switch
    {
        "RoundRobin" => new RoundRobinStrategy(config.Servers),
        "WeightedRoundRobin" => new WeightedRoundRobinStrategy(config.Servers),
        "StickyRoundRobin" => new StickyRoundRobinStrategy(
            config.Servers,
            httpAccessor,
            config.Duration),
        "IpHash" => new HashBasedStrategy(
            config.Servers,
            httpAccessor),
        "UrlHash" => new HashBasedStrategy(
            config.Servers,
            httpAccessor,
            HashBasedStrategy.HashMode.Url),
        "LeastConnections" => new LeastConnectionsStrategy(config.Servers),
        _ => new RoundRobinStrategy(config.Servers)
    };
});

var app = builder.Build();

async Task<IResult> HandleRequest(
    HttpContext context,
    ILoadBalancerStrategy strategy,
    HttpClient httpClient)
{
    if (strategy is not LeastConnectionsStrategy lcStrategy)
    {
        var server = strategy.GetNextServer();
        try
        {
            var response = await httpClient.GetAsync(server.Url);
            response.EnsureSuccessStatusCode();
            return Results.Text(await response.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem(
                detail: $"Ошибка соединения с сервером {server.Url}: {ex.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
    else
    {
        var server = lcStrategy.GetNextServer();
        try
        {
            var response = await httpClient.GetAsync(server.Url);
            response.EnsureSuccessStatusCode();
            return Results.Text(await response.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException ex)
        {
            return Results.Problem(
                detail: $"Ошибка соединения с сервером {server.Url}: {ex.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
        finally
        {
            lcStrategy.ReleaseConnection(server);
        }
    }
}

app.Use(async (context, next) =>
{
    var throttlingStrategy = context.RequestServices.GetRequiredService<IThrottlingStrategy>();

    if (!throttlingStrategy.TryProcessRequest())
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.Response.WriteAsync("Сервер перегружен, попробуйте еще раз позже");
        return;
    }

    await next();
});

app.MapGet("/", HandleRequest);

app.Run("http://0.0.0.0:8080");