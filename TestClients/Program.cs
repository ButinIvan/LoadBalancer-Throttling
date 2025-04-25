using System.Collections.Concurrent;

var baseUrl = "http://localhost:8080";
var clientsCount = 5;
var requestsPerClient = 20;
var minDelayMs = 100;
var maxDelayMs = 500;

var results = new ConcurrentDictionary<string, int>();
var successCount = 0;
var errorCount = 0;
var random = new Random();

var tasks = new Task[clientsCount];
for (var i = 0; i < clientsCount; i++)
{
    var clientId = i + 1;
    tasks[i] = Task.Run(async () =>
    {
        using var client = new HttpClient();

        for (var j = 0; j < requestsPerClient; j++)
        {
            try
            {
                var response = await client.GetStringAsync(baseUrl);
                var serverId = ParseServerId(response);

                results.AddOrUpdate(serverId, 1, (_, count) => count + 1);
                Interlocked.Increment(ref successCount);

                Console.WriteLine($"Client {clientId}: {response}");
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errorCount);
                Console.WriteLine($"Client {clientId} error: {ex.Message}");
            }

            await Task.Delay(random.Next(minDelayMs, maxDelayMs));
        }
    });
}

await Task.WhenAll(tasks);

Console.WriteLine();
Console.WriteLine("Результаты");
Console.WriteLine($"Всего запросов: {successCount + errorCount}");
Console.WriteLine($"Успешных: {successCount}, Ошибок: {errorCount}");

foreach (var (server, count) in results)
{
    Console.WriteLine($"Сервер {server}: {count} запросов");
}

static string ParseServerId(string response)
{
    return response.Split("Server:")[1].Split(',')[0].Trim();
}