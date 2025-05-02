using LoadBalancer.Configurations;
using LoadBalancer.Strategies;
using LoadBalancer.Strategies.Implementations;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);
builder.Services.Configure<LoadBalancerConfig>(
    builder.Configuration.GetSection("LoadBalancer"));

builder.Services.AddSingleton<ILoadBalancerStrategy>(provider =>
{
    var config = provider.GetRequiredService<IOptions<LoadBalancerConfig>>().Value;
    return config.Strategy switch
    {
        "RoundRobin" => new RoundRobinStrategy(config.Servers),
        "WeightedRoundRobin" => new WeightedRoundRobinStrategy(config.Servers),
        "StickyRoundRobin" => new StickyRoundRobinStrategy(
            config.Servers,
            provider.GetRequiredService<IHttpContextAccessor>(),
            config.Duration),
        _ => new RoundRobinStrategy(config.Servers)
    };
});

var app = builder.Build();

async Task<IResult> HandleRequest(
    HttpContext context,
    ILoadBalancerStrategy strategy,
    HttpClient httpClient)
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

app.MapGet("/", HandleRequest);

app.Run("http://0.0.0.0:8080");