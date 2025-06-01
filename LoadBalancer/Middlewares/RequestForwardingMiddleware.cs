using LoadBalancer.Configurations;
using LoadBalancer.Strategies;
using LoadBalancer.Strategies.Implementations;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Middlewares;

public class RequestForwardingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;

    public RequestForwardingMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory, ILogger logger)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var strategy = context.RequestServices.GetRequiredService<ILoadBalancerStrategy>();

        _logger.Information("Start processing {Method} {Path}",
            context.Request.Method, context.Request.Path);

        ServerConfig server;

        if (strategy is not LeastConnectionsStrategy lcStrategy)
        {
            server = strategy.GetNextServer();
            await ProcessRequest(context, server);
        }
        else
        {
            server = lcStrategy.GetNextServer();
            await ProcessRequestWithCleanup(context, server, lcStrategy);
        }
    }
    
    private void HandleError(HttpContext context, ServerConfig server, Exception e)
    {
        _logger.Error(e, "Error forwarding request to {Server}", server.Url);
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        context.Response.WriteAsync($"Error forwarding request: {e.Message}").Wait();
    }
    
    private async Task ProcessResponse(
        HttpContext context, 
        ServerConfig server,
        HttpResponseMessage responseMessage)
    {
        _logger.Information("Response from {Server}: {StatusCode}",
            server.Url, (int)responseMessage.StatusCode);

        context.Response.StatusCode = (int)responseMessage.StatusCode;

        foreach (var header in responseMessage.Headers)
        {
            context.Response.Headers[header.Key] = header.Value.ToArray();
        }

        if (responseMessage.Content != null)
        {
            foreach (var header in responseMessage.Content.Headers)
            {
                context.Response.Headers[header.Key] = header.Value.ToArray();
            }

            await responseMessage.Content.CopyToAsync(context.Response.Body);
        }
    }
    
    private HttpRequestMessage CreateRequestMessage(HttpContext context, ServerConfig server)
    {
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri(new Uri(server.Url), context.Request.Path + context.Request.QueryString)
        };

        foreach (var header in context.Request.Headers)
        {
            if (!requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            {
                requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        if (context.Request.ContentLength > 0)
        {
            requestMessage.Content = new StreamContent(context.Request.Body);
        }

        _logger.Information("Forwarding request to: {TargetUri}", requestMessage.RequestUri);
        return requestMessage;
    }
    
    private async Task ProcessRequest(HttpContext context, ServerConfig server)
    {
        using var client = _httpClientFactory.CreateClient();
        var requestMessage = CreateRequestMessage(context, server);

        try
        {
            var responseMessage = await client.SendAsync(
                requestMessage, 
                HttpCompletionOption.ResponseHeadersRead,
                context.RequestAborted);

            await ProcessResponse(context, server, responseMessage);
        }
        catch (Exception e)
        {
            HandleError(context, server, e);
        }
    }
    
    private async Task ProcessRequestWithCleanup(
        HttpContext context, 
        ServerConfig server,
        LeastConnectionsStrategy lcStrategy)
    {
        try
        {
            await ProcessRequest(context, server);
        }
        finally
        {
            lcStrategy.ReleaseConnection(server);
        }
    }
}