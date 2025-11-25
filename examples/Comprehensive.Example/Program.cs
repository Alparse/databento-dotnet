using Databento.Client.Builders;
using Databento.Client.Models;
using System.Collections.Concurrent;

var apiKey = Environment.GetEnvironmentVariable("DATABENTO_API_KEY")
    ?? throw new InvalidOperationException("DATABENTO_API_KEY not set");

// Symbol map: InstrumentId → Ticker Symbol
var symbolMap = new ConcurrentDictionary<uint, string>();

// Create live client
await using var client = new LiveClientBuilder()
    .WithApiKey(apiKey)
    .WithDataset("EQUS.MINI")
    .Build();

// Handle incoming records
client.DataReceived += (sender, e) =>
{
    // Step 1: Capture symbol mappings (arrive first)
    if (e.Record is SymbolMappingMessage mapping)
    {
        // ⚠️ Use STypeOutSymbol for the actual ticker symbol!
        symbolMap[mapping.InstrumentId] = mapping.STypeOutSymbol;
        return;
    }

    // Step 2: Resolve symbols for data records
    if (e.Record is TradeMessage trade)
    {
        var symbol = symbolMap.GetValueOrDefault(
            trade.InstrumentId,
            trade.InstrumentId.ToString());  // Fallback if not found

        Console.WriteLine($"{symbol}: ${trade.PriceDecimal:F2} x {trade.Size}");
    }
};

// Calculate most recent market open (9:30 AM ET) for replay mode
var now = DateTimeOffset.UtcNow;
var et = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
var etNow = TimeZoneInfo.ConvertTime(now, et);
var replayDate = etNow.Date;

// Go back to most recent weekday
while (replayDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
    replayDate = replayDate.AddDays(-1);

if (etNow.TimeOfDay < TimeSpan.FromHours(9.5))
{
    replayDate = replayDate.AddDays(-1);
    while (replayDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        replayDate = replayDate.AddDays(-1);
}

var marketOpen = new DateTimeOffset(
    replayDate.Year, replayDate.Month, replayDate.Day,
    9, 30, 0, et.GetUtcOffset(replayDate));

// Subscribe with replay mode (works anytime, no market hours required)
await client.SubscribeAsync(
    dataset: "EQUS.MINI",
    schema: Schema.Trades,
    symbols: new[] { "NVDA", "AAPL" },
    startTime: marketOpen  // Omit this parameter for live mode
);

await client.StartAsync();

// CRITICAL: Must use StreamAsync() to pump records through the pipeline
var timeout = Task.Delay(TimeSpan.FromSeconds(30));
var streamTask = Task.Run(async () =>
{
    await foreach (var record in client.StreamAsync())
    {
        // Records are handled by DataReceived event
    }
});

await Task.WhenAny(streamTask, timeout);
await client.StopAsync();
