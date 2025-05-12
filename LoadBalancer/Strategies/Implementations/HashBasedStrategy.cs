using System.Security.Cryptography;
using System.Text;
using LoadBalancer.Configurations;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Strategies.Implementations;

public class HashBasedStrategy : ILoadBalancerStrategy
{
    public enum HashMode
    {
        Ip,
        Url
    }

    private readonly ServerConfig[] _servers;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HashMode _mode;
    private readonly ILogger _logger;

    public HashBasedStrategy(ServerConfig[] servers,
        IHttpContextAccessor httpContextAccessor,
        ILogger logger,
        HashMode mode = HashMode.Ip)
    {
        _servers = servers;
        _httpContextAccessor = httpContextAccessor;
        _mode = mode;
        _logger = logger;
    }

    public ServerConfig GetNextServer()
    {
        try
        {
            var hashKey = _mode switch
            {
                HashMode.Ip => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                HashMode.Url => _httpContextAccessor.HttpContext?.Request.Path.ToString(),
                _ => throw new InvalidOperationException("Unknown hash mode")
            } ?? "";

            _logger.Debug("Calculating hash for {HashKey} in {Mode} mode", hashKey, _mode);

            var hash = CalculateHash(hashKey);
            var selectedServer = _servers[hash % _servers.Length];

            _logger.Information("Selected server {ServerUrl} for {HashKey} (Hash: {Hash})",
                selectedServer.Url, hashKey, hash);

            return selectedServer;
        }
        catch (Exception e)
        {
            _logger.Error(e, "Error in HashBasedStrategy.GetNextServer");
            throw;
        }
    }

    private static uint CalculateHash(string input)
    {
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToUInt32(hashBytes, 0);
    }
}