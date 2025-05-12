using System.Diagnostics;
using LoadBalancer.Configurations;
using LoadBalancer.Strategies;
using LoadBalancer.Strategies.Implementations;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using ILogger = Serilog.ILogger;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithSpan()
    .WriteTo.Console(outputTemplate: 
        "{Timestamp:HH:mm:ss} [{Level}] TraceId: {TraceId} | {Message}{NewLine}{Exception}")
    .WriteTo.Seq("http://localhost:5341")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .WriteTo.Console()
            .WriteTo.Seq("http://localhost:5341"));

    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();
    builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);
    builder.Services.Configure<LoadBalancerConfig>(
        builder.Configuration.GetSection("LoadBalancer"));

    builder.Services.AddSingleton<ILogger>(provider => Log.Logger);
    
    builder.Services.AddSingleton<ILoadBalancerStrategy>(provider =>
    {
        var config = provider.GetRequiredService<IOptions<LoadBalancerConfig>>().Value;
        var httpAccessor = provider.GetRequiredService<IHttpContextAccessor>();
        var logger = provider.GetRequiredService<ILogger>();
        
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
            _ => new RoundRobinStrategy(
                config.Servers,
                logger)
        };
    });

    var app = builder.Build();

    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = ((context, httpContext) =>
        {
            context.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
            context.Set("UserAgent", httpContext.Request.Headers.UserAgent);
        });
    });

    async Task<IResult> HandleRequest(
        HttpContext context,
        ILoadBalancerStrategy strategy,
        HttpClient httpClient,
        ILogger<Program> logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("LoadBalancer.HandleRequest");
        
        logger.LogInformation("Начало обработки запроса {Path}", context.Request.Path);
        
        var server = strategy.GetNextServer();
        logger.LogInformation("Выбран сервер: {ServerUrl}", server.Url);
        
        try
        {
            var response = await httpClient.GetAsync(server.Url);
            response.EnsureSuccessStatusCode();
            logger.LogInformation("Успешный ответ от сервера {ServerUrl}", server.Url);
            return Results.Text(await response.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException e)
        {
            logger.LogError(e, "Ошибка соединения с сервером {server.Url}", server.Url);
            return Results.Problem(
                detail: $"Ошибка соединения с сервером {server.Url}: {e.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }

    app.MapGet("/", HandleRequest);

    app.Run("http://0.0.0.0:8080");
}
catch (Exception e)
{
    Log.Fatal(e, "Работа приложения завершилась");
}
finally
{
    Log.CloseAndFlush();
}