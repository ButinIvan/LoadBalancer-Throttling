using System.Collections.Concurrent;
using LoadBalancer.Configurations;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Strategies.Implementations;

public class StickyRoundRobinStrategy : ILoadBalancerStrategy
{
    
    private readonly ServerConfig[] _servers;
    private readonly ConcurrentDictionary<string, (ServerConfig server, DateTime expiry)> _sessions;
    private readonly TimeSpan _duration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private RoundRobinStrategy _roundRobin;
    private readonly ILogger _logger;

    public StickyRoundRobinStrategy(
        ServerConfig[] servers,
        IHttpContextAccessor httpContextAccessor,
        TimeSpan duration,
        ILogger logger)
    {
        _logger = logger;
        _servers = servers;
        _httpContextAccessor = httpContextAccessor;
        _duration = duration;
        _sessions = new ConcurrentDictionary<string, (ServerConfig, DateTime)>();
        _roundRobin = new RoundRobinStrategy(_servers, _logger);
        
        _logger.Debug("Initialized with {ServerCount} servers, session duration {Duration}", servers.Length, duration);
    }

    public ServerConfig GetNextServer()
    {
        try
        {
            var clientId = GetClientIdentifier();
            _logger.Debug("Client id: {ClientId}", clientId);

            if (_sessions.TryGetValue(clientId, out var session) && session.expiry > DateTime.UtcNow)
            {
                _logger.Information("Using existing session for {ClientId} with server {ServerUrl}",
                    clientId, session.server.Url);
                return session.server;
            }

            var server = _roundRobin.GetNextServer();

            _sessions.AddOrUpdate(
                clientId,
                (server, DateTime.UtcNow.Add(_duration)),
                (_, _) => (server, DateTime.UtcNow.Add(_duration)));

            _logger.Information("Created new session for {ClientId} with server {ServerUrl} (Expires: {Expiry})",
                clientId, server.Url, DateTime.UtcNow.Add(_duration));

            return server;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error in StickyRoundRobinStrategy.GetNextServer");
            throw;
        }
    }

    private string GetClientIdentifier()
    {
        var context = _httpContextAccessor.HttpContext;
        
        var stickyCookie = context?.Request.Cookies["sticky"];
        if (!string.IsNullOrEmpty(stickyCookie))
        {
            _logger.Debug("Found sticky cookie: {StickyCookie}", stickyCookie);
            return stickyCookie;
        }

        var ipAddress = _httpContextAccessor.HttpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault() ??
                        context?.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(ipAddress))
        {
            _logger.Debug("Using IP address as id: {IpAddress}", ipAddress);
            return ipAddress;
        }

        var newId = Guid.NewGuid().ToString();
        context?.Response.Cookies.Append("sticky", newId, new CookieOptions()
        {
            Expires = DateTime.UtcNow.Add(_duration),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });

        _logger.Debug("Generated new client id: {NewId}", newId);
        return newId;
    }
}