var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Urls.Add($"http://0.0.0.0:{port}");

app.MapGet("/", () =>
{
    var serverId = Environment.GetEnvironmentVariable("SERVER_ID") ?? "unknown";
    return $"Server: {serverId}, Port: {port}";
});

app.Run();