using System.Security.Cryptography;
using System.Text;
using LoadBalancer.Configurations;

namespace LoadBalancer.Strategies.Implementations;

public class UrlHashStrategy : ILoadBalancerStrategy
{
    private readonly ServerConfig[] _servers;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UrlHashStrategy(ServerConfig[] servers, IHttpContextAccessor httpContextAccessor)
    {
        _servers = servers;
        _httpContextAccessor = httpContextAccessor;
    }

    public ServerConfig GetNextServer()
    {
        var url = _httpContextAccessor.HttpContext?.Request.Path.ToString() ?? "";
        var hash = CalculateHash(url);
        return _servers[hash % _servers.Length];
    }
    
    private static uint CalculateHash(string input)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToUInt32(hashBytes, 0);
    }
}