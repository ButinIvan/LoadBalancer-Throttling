using LoadBalancer.Configurations;
using LoadBalancer.Extensions;
using LoadBalancer.Handlers;
using LoadBalancer.Middleware;
using Serilog;
using ILogger = Serilog.ILogger;


Log.Logger = LoggerConfig.CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    
    LoggerConfig.ConfigureSerilog(builder);
    
    builder.Services.AddSingleton<ILogger>(_ => Log.Logger);
    builder.Services.AddHttpClient();
    builder.Services.AddHttpContextAccessor();
    
    builder.Configuration.AddJsonFile("config.json", optional: false, reloadOnChange: true);
    
    builder.Services.AddLoadBalancerStrategy(builder.Configuration);
    builder.Services.AddThrottlingStrategy(builder.Configuration);
    
    var app = builder.Build();

    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = ((context, httpContext) =>
        {
            context.Set("ClientIp", httpContext.Connection.RemoteIpAddress?.ToString());
            context.Set("UserAgent", httpContext.Request.Headers.UserAgent);
        });
    });
    
    app.UseMiddleware<ThrottlingMiddleware>();
    
    app.MapGet("/", RequestHandler.HandleRequest);
    
    app.Run("http://0.0.0.0:8080");
}
catch (Exception e)
{
    Log.Fatal(e, "The application has terminated");
}
finally
{
    Log.CloseAndFlush();
}