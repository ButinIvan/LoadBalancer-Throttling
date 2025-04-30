using System.Collections.Concurrent;
using LoadBalancer.Configurations;

namespace LoadBalancer.Strategies.Implementations;

public class StickyRoundRobinStrategy : ILoadBalancerStrategy
{
    
    private readonly ServerConfig[] _servers;
    private readonly ConcurrentDictionary<string, (ServerConfig server, DateTime expiry)> _sessions;
    private readonly TimeSpan _duration;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private RoundRobinStrategy _roundRobin;

    public StickyRoundRobinStrategy(
        ServerConfig[] servers,
        IHttpContextAccessor httpContextAccessor,
        TimeSpan duration)
    {
        _servers = servers;
        _httpContextAccessor = httpContextAccessor;
        _duration = duration;
        _sessions = new ConcurrentDictionary<string, (ServerConfig, DateTime)>();
        _roundRobin = new RoundRobinStrategy(_servers);
    }

    public ServerConfig GetNextServer()
    {
        var clientId = GetClientIdentifier();

        if (_sessions.TryGetValue(clientId, out var session) && session.expiry > DateTime.UtcNow)
        {
            return session.server;
        }
        
        var server = _roundRobin.GetNextServer();
        
        _sessions.AddOrUpdate(
            clientId,
            (server, DateTime.UtcNow.Add(_duration)),
            (_, _) => (server, DateTime.UtcNow.Add(_duration)));

        return server;
    }

    private string GetClientIdentifier()
    {
        var context = _httpContextAccessor.HttpContext;
        
        var stickyCookie = context?.Request.Cookies["sticky"];
        if (!string.IsNullOrEmpty(stickyCookie))
            return stickyCookie;

        var ipAddress = context?.Connection.RemoteIpAddress?.ToString();
        if (!string.IsNullOrEmpty(ipAddress))
            return ipAddress;

        var newId = Guid.NewGuid().ToString();
        context?.Response.Cookies.Append("sticky", newId, new CookieOptions()
        {
            Expires = DateTime.UtcNow.Add(_duration),
            HttpOnly = true,
            SameSite = SameSiteMode.Lax
        });

        return newId;
    }
}