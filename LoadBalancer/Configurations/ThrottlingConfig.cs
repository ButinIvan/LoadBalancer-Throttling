namespace LoadBalancer.Configurations;

public class ThrottlingConfig
{
    public string Strategy { get; set; } = "RejectingSlidingWindow";
    public int RequestLimit { get; set; } = 100;
    public TimeSpan WindowSize { get; set; } = TimeSpan.FromSeconds(1);
}