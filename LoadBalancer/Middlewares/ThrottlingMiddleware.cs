using LoadBalancer.Throttling;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Middleware;

public class ThrottlingMiddleware
{
    private readonly RequestDelegate _next;

    public ThrottlingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var throttlingStrategy = context.RequestServices.GetRequiredService<IThrottlingStrategy>();
        var logger = context.RequestServices.GetRequiredService<ILogger>();

        if (!throttlingStrategy.TryProcessRequest())
        {
            logger.Warning("Request throttled - too many requests from {ClientIp}", 
                context.Connection.RemoteIpAddress?.ToString());
            
            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            await context.Response.WriteAsync("The server is busy. Please, try again later");
            return;
        }

        logger.Debug("Request allowed by throttling strategy");
        await _next(context);
    }
}