using Databento.Client.Builders;
using Databento.Client.Models;

// Get API key from environment variable (secure)
var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY environment variable not set");

// Create live client
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .Build();

// Subscribe to events
client.DataReceived += (sender, e) =>
{
    Console.WriteLine($"Received: {e.Record}");
};

// Subscribe to NVDA trades
await client.SubscribeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA" }
);

// Start streaming
await client.StartAsync();

// Stream records using IAsyncEnumerable
await foreach (var record in client.StreamAsync())
{
    // Process records
    if (record is TradeMessage trade)
    {
        Console.WriteLine($"Trade: {trade.InstrumentId} @ {trade.PriceDecimal}");
    }
}
