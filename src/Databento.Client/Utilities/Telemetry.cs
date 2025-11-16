using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Databento.Client.Utilities;

/// <summary>
/// MEDIUM FIX: Centralized telemetry for distributed tracing and metrics
/// Provides OpenTelemetry-compatible instrumentation without requiring OpenTelemetry packages
/// Users can opt-in to OpenTelemetry by configuring ActivitySource and Meter listeners
/// </summary>
internal static class Telemetry
{
    /// <summary>
    /// ActivitySource for distributed tracing (OpenTelemetry-compatible)
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(
        "Databento.Client",
        typeof(Telemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0");

    /// <summary>
    /// Meter for metrics collection (OpenTelemetry-compatible)
    /// </summary>
    public static readonly Meter Meter = new(
        "Databento.Client",
        typeof(Telemetry).Assembly.GetName().Version?.ToString() ?? "1.0.0");

    // ========== Counters (for rate/count metrics) ==========

    /// <summary>
    /// Total number of records received across all clients
    /// </summary>
    public static readonly Counter<long> RecordsReceived = Meter.CreateCounter<long>(
        "databento.records.received",
        description: "Total number of records received from Databento");

    /// <summary>
    /// Total number of API requests (Historical/Reference)
    /// </summary>
    public static readonly Counter<long> ApiRequests = Meter.CreateCounter<long>(
        "databento.api.requests",
        description: "Total number of API requests to Databento");

    /// <summary>
    /// Total number of API request failures
    /// </summary>
    public static readonly Counter<long> ApiRequestFailures = Meter.CreateCounter<long>(
        "databento.api.failures",
        description: "Total number of failed API requests");

    /// <summary>
    /// Total number of retry attempts
    /// </summary>
    public static readonly Counter<long> RetryAttempts = Meter.CreateCounter<long>(
        "databento.api.retries",
        description: "Total number of API retry attempts");

    /// <summary>
    /// Total number of subscriptions created
    /// </summary>
    public static readonly Counter<long> SubscriptionsCreated = Meter.CreateCounter<long>(
        "databento.subscriptions.created",
        description: "Total number of subscriptions created");

    /// <summary>
    /// Total number of errors from live streams
    /// </summary>
    public static readonly Counter<long> StreamErrors = Meter.CreateCounter<long>(
        "databento.stream.errors",
        description: "Total number of errors from live streams");

    // ========== Histograms (for distribution metrics) ==========

    /// <summary>
    /// API request duration histogram
    /// </summary>
    public static readonly Histogram<double> ApiRequestDuration = Meter.CreateHistogram<double>(
        "databento.api.duration",
        unit: "ms",
        description: "API request duration in milliseconds");

    /// <summary>
    /// Record processing latency (time from ts_recv to processing)
    /// </summary>
    public static readonly Histogram<double> RecordLatency = Meter.CreateHistogram<double>(
        "databento.records.latency",
        unit: "ms",
        description: "Record processing latency in milliseconds");

    // ========== Observable Gauges (for current state) ==========

    private static long _activeConnections = 0;
    private static long _activeSubscriptions = 0;

    /// <summary>
    /// Current number of active Live client connections
    /// </summary>
    public static readonly ObservableGauge<long> ActiveConnections = Meter.CreateObservableGauge(
        "databento.connections.active",
        () => Interlocked.Read(ref _activeConnections),
        description: "Number of active Live client connections");

    /// <summary>
    /// Current number of active subscriptions
    /// </summary>
    public static readonly ObservableGauge<long> ActiveSubscriptions = Meter.CreateObservableGauge(
        "databento.subscriptions.active",
        () => Interlocked.Read(ref _activeSubscriptions),
        description: "Number of active subscriptions");

    // ========== Helper methods for managing observable gauge state ==========

    public static void IncrementActiveConnections() => Interlocked.Increment(ref _activeConnections);
    public static void DecrementActiveConnections() => Interlocked.Decrement(ref _activeConnections);
    public static void IncrementActiveSubscriptions() => Interlocked.Increment(ref _activeSubscriptions);
    public static void DecrementActiveSubscriptions() => Interlocked.Decrement(ref _activeSubscriptions);
}
