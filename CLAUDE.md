- 1 output: === Symbol Resolution Example (Replay Mode) ===

This example demonstrates how to resolve InstrumentId → Ticker Symbol
during market data streaming using REPLAY mode.

Benefits of Replay:
  √ Works anytime (doesn't require market to be open)
  √ Guaranteed to have data
  √ Perfect for testing and development

INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763732978
√ Created live client
Subscribing to NVDA, AAPL trades in REPLAY mode...
  Replay Start: 2025-11-17 09:30:00 -05:00 (Market Open)

√ Subscribed

Starting stream...
INFO: [LiveBlocking::Start] Starting session
√ Stream started

Metadata:
  Dataset:  EQUS.MINI
  Symbols:
  Schema:

Receiving data (will run for 30 seconds of replay data)...

  First record arrived after 190ms

  Mapping #1:
    InstrumentId:   11667
    STypeInSymbol:  NVDA
    STypeOutSymbol: NVDA
    → Stored: 11667 → NVDA

  Mapping #2:
    InstrumentId:   38
    STypeInSymbol:  AAPL
    STypeOutSymbol: AAPL
    → Stored: 38 → AAPL

  Trade #1: NVDA   @ $  185.97 x    52 [14:30:00.008]
  Trade #2: AAPL   @ $  268.82 x   100 [14:30:00.051]
  Trade #3: NVDA   @ $  185.97 x    30 [14:30:00.076]
  Trade #4: AAPL   @ $  268.82 x    15 [14:30:00.076]
  Trade #5: NVDA   @ $  185.97 x    36 [14:30:00.077]
  Trade #6: AAPL   @ $  268.85 x    50 [14:30:00.089]
  Trade #7: AAPL   @ $  268.85 x    50 [14:30:00.089]
  Trade #8: NVDA   @ $  185.97 x   134 [14:30:00.097]
  Trade #9: AAPL   @ $  268.77 x    10 [14:30:00.108]
  Trade #10: NVDA   @ $  185.97 x   100 [14:30:00.113]
  Trade #11: NVDA   @ $  185.97 x    90 [14:30:00.114]
  Trade #12: AAPL   @ $  268.77 x     5 [14:30:00.114]
  Trade #13: AAPL   @ $  268.77 x   295 [14:30:00.118]
  Trade #14: AAPL   @ $  268.77 x   505 [14:30:00.118]
  Trade #15: NVDA   @ $  185.97 x    10 [14:30:00.125]
  ... (suppressing further output, continuing to collect data) Databento Live Client Authentication Example
=============================================

√ API key found in environment variable
  Key: db-YQ9y6... (masked)

INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763733037
√ Successfully created LiveClient
  Default dataset: EQUS.MINI

Verifying authentication...
Subscribing to NVDA trades on EQUS.MINI dataset
√ Subscribed to NVDA trades

Starting live stream...
(Receiving first 5 records to verify authentication)

INFO: [LiveBlocking::Start] Starting session
[23:25:21.980] Record #1: SystemMessage
[23:25:21.988] Record #2: SymbolMappingMessage Databento LiveBlocking Client Example
=====================================

Method 1: API Key as Argument (NOT recommended for production)
---------------------------------------------------------------
In production, use Method 2 (environment variable) instead.

(Skipped - not recommended for production use)

Method 2: API Key from Environment Variable (RECOMMENDED)
----------------------------------------------------------
Reads from DATABENTO_API_KEY environment variable

INFO: [LiveBlocking::Authenticate] Successfully authenticated with session_id 1763733089
√ Created LiveClient from environment variable
  Dataset: EQUS.MINI

Verifying client connectivity...
Subscribing to NVDA trades for 5 seconds

√ Subscribed to NVDA trades

INFO: [LiveBlocking::Start] Starting session
√ Started streaming
  (Receiving data for 5 seconds...)

[23:25:50.783] System: [Unset] Subscription request 1 for trades data succeeded [2025-11-18T05:25:50.8850000+00:00]

√ Client verification complete!

  Total records received: 2
  Trade messages:         0
  Other messages:         2

Summary:
--------
√ LiveClient successfully created using environment variable
√ Dataset configured: EQUS.MINI
√ Connectivity verified with NVDA subscription

Builder Pattern Mapping:
  C++:  LiveBlocking::Builder().SetKeyFromEnv().SetDataset(...).BuildBlocking()
  C#:   new LiveClientBuilder().WithApiKey(apiKey).WithDataset(...).Build()

Additional Builder Options:
---------------------------
 .WithSendTimestampOut(bool)      - Include gateway send timestamps
 .WithUpgradePolicy(policy)       - Set version upgrade policy
 .WithHeartbeatInterval(timespan) - Configure heartbeat interval
 .WithLogger(logger)              - Add diagnostic logging

C:\Users\serha\source\repos\databento-dotnet\examples\LiveBlocking.Example\bin\Debug\net8.0\LiveBlocking.Example.exe (process 23996) exited with code 0 (0x0).
To automatically close the console when debugging stops, enable Tools->Options->Debugging->Automatically close the console when debugging stops.
Press any key to close this window . . .