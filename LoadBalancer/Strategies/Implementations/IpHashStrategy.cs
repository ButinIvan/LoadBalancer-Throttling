using System.Security.Cryptography;
using System.Text;
using LoadBalancer.Configurations;

namespace LoadBalancer.Strategies.Implementations;

public class IpHashStrategy : ILoadBalancerStrategy
{
    private readonly ServerConfig[] _servers;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IpHashStrategy(ServerConfig[] servers, IHttpContextAccessor httpContextAccessor)
    {
        _servers = servers;
        _httpContextAccessor = httpContextAccessor;
    }
    
    public ServerConfig GetNextServer()
    {
        var ip = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "";
        var hash = CalculateHash(ip);
        return _servers[hash % _servers.Length];
    }

    private static uint CalculateHash(string input)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToUInt32(hashBytes, 0);
    }
}