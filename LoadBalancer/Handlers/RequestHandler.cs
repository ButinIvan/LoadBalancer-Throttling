using System.Diagnostics;
using LoadBalancer.Strategies;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Handlers;

public static class RequestHandler
{
    public static async Task<IResult> HandleRequest(
        HttpContext context,
        ILoadBalancerStrategy strategy,
        HttpClient httpClient,
        ILogger logger)
    {
        using var activity = Activity.Current?.Source.StartActivity("LoadBalancer.HandleRequest");
        
        logger.Information("Start processing request {Path}", context.Request.Path);
        
        var server = strategy.GetNextServer();
        
        try
        {
            var response = await httpClient.GetAsync(server.Url);
            response.EnsureSuccessStatusCode();
            
            logger.Information("Successful response from the server {ServerUrl}", server.Url);
            
            return Results.Text(await response.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException e)
        {
            logger.Error(e, "Error connecting to server {ServerUrl}", server.Url);
            
            return Results.Problem(
                detail: $"Error connecting to server {server.Url}: {e.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}