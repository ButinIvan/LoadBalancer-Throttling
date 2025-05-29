using LoadBalancer.Strategies;
using LoadBalancer.Strategies.Implementations;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Middlewares;

public class RequestForwardingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpClientFactory _httpClientFactory;

    public RequestForwardingMiddleware(RequestDelegate next, IHttpClientFactory httpClientFactory)
    {
        _next = next;
        _httpClientFactory = httpClientFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger>();
        var strategy = context.RequestServices.GetRequiredService<ILoadBalancerStrategy>();

        logger.Information("Start processing {Method} {Path}",
            context.Request.Method, context.Request.Path);
        
        if (strategy is not LeastConnectionsStrategy lcStrategy)
        {
            var server = strategy.GetNextServer();

            var client = _httpClientFactory.CreateClient();
            var requestMessage = new HttpRequestMessage();

            try
            {
                requestMessage.Method = new HttpMethod(context.Request.Method);

                foreach (var header in context.Request.Headers)
                {
                    if (requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                        requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }

                if (context.Request.ContentLength > 0)
                {
                    requestMessage.Content = new StreamContent(context.Request.Body);
                }

                var targetUri = new Uri(new Uri(server.Url), context.Request.Path + context.Request.QueryString);
                requestMessage.RequestUri = targetUri;

                logger.Information("Forwarding request to: {TargetUri}", targetUri);

                var responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead,
                    context.RequestAborted);

                logger.Information("Response from {Server}: {StatusCode}",
                    server.Url, (int)responseMessage.StatusCode);

                context.Response.StatusCode = (int)responseMessage.StatusCode;

                if (responseMessage.Headers != null)
                    foreach (var header in responseMessage.Headers)
                        context.Response.Headers[header.Key] = header.Value.ToArray();

                if (responseMessage.Content?.Headers != null)
                    foreach (var header in responseMessage.Content.Headers)
                        context.Response.Headers[header.Key] = header.Value.ToArray();

                await responseMessage.Content.CopyToAsync(context.Response.Body);
            }
            catch (Exception e)
            {
                logger.Error(e, "Error forwarding request to {Server}", server.Url);
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                await context.Response.WriteAsync($"Error forwarding request: {e.Message}");
            }
        }
        else
        {
            var server = lcStrategy.GetNextServer();
            var client = _httpClientFactory.CreateClient();
            var requestMessage = new HttpRequestMessage();

            try
            {
                requestMessage.Method = new HttpMethod(context.Request.Method);

                foreach (var header in context.Request.Headers)
                {
                    if (requestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
                        requestMessage.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
                }

                if (context.Request.ContentLength > 0)
                {
                    requestMessage.Content = new StreamContent(context.Request.Body);
                }

                var targetUri = new Uri(new Uri(server.Url), context.Request.Path + context.Request.QueryString);
                requestMessage.RequestUri = targetUri;

                logger.Information("Forwarding request to: {TargetUri}", targetUri);

                var responseMessage = await client.SendAsync(requestMessage, HttpCompletionOption.ResponseHeadersRead,
                    context.RequestAborted);

                logger.Information("Response from {Server}: {StatusCode}",
                    server.Url, (int)responseMessage.StatusCode);

                context.Response.StatusCode = (int)responseMessage.StatusCode;

                if (responseMessage.Headers != null)
                    foreach (var header in responseMessage.Headers)
                        context.Response.Headers[header.Key] = header.Value.ToArray();

                if (responseMessage.Content?.Headers != null)
                    foreach (var header in responseMessage.Content.Headers)
                        context.Response.Headers[header.Key] = header.Value.ToArray();

                await responseMessage.Content.CopyToAsync(context.Response.Body);
            }
            catch (Exception e)
            {
                logger.Error(e, "Error forwarding request to {Server}", server.Url);
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                await context.Response.WriteAsync($"Error forwarding request: {e.Message}");
            }
            finally
            {
                lcStrategy.ReleaseConnection(server);
            }
        }
    }
}