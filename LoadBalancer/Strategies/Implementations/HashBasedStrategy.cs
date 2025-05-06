using System.Security.Cryptography;
using System.Text;
using LoadBalancer.Configurations;

namespace LoadBalancer.Strategies.Implementations;

public class HashBasedStrategy : ILoadBalancerStrategy
{
    public enum HashMode { Ip, Url }
    
    private readonly ServerConfig[] _servers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HashMode _mode;

    public HashBasedStrategy(ServerConfig[] servers, IHttpContextAccessor httpContextAccessor, HashMode mode = HashMode.Ip)
    {
        _servers = servers;
        _httpContextAccessor = httpContextAccessor;
        _mode = mode;
    }
    
    public ServerConfig GetNextServer()
    {
        var hashKey = _mode switch
        {
            HashMode.Ip => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            HashMode.Url => _httpContextAccessor.HttpContext?.Request.Path.ToString(),
            _ => throw new InvalidOperationException("Unknown hash mode")
        } ?? "";
        
        var hash = CalculateHash(hashKey);
        return _servers[hash % _servers.Length];
    }

    private static uint CalculateHash(string input)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToUInt32(hashBytes, 0);
    }
}