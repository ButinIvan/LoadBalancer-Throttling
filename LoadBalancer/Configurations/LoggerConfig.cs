using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace LoadBalancer.Configurations;

public static class LoggerConfig
{
    public static ILogger CreateBootstrapLogger()
    {
        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithSpan()
            .WriteTo.Console(outputTemplate: 
                "{Timestamp:HH:mm:ss} [{Level}] TraceId: {TraceId} | {Message} {NewLine} {Exception}")
            .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://seq:5343")
            .CreateBootstrapLogger();
    }

    public static void ConfigureSerilog(WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) =>
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithSpan()
                .WriteTo.Console()
                .WriteTo.Seq(Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://0.0.0.0:5343"));
    }
}