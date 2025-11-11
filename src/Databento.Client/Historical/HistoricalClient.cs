using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Databento.Client.Metadata;
using Databento.Client.Models;
using Databento.Interop;
using Databento.Interop.Handles;
using Databento.Interop.Native;

namespace Databento.Client.Historical;

/// <summary>
/// Historical data client implementation
/// </summary>
public sealed class HistoricalClient : IHistoricalClient
{
    private readonly HistoricalClientHandle _handle;
    private bool _disposed;

    internal HistoricalClient(string apiKey)
    {
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        byte[] errorBuffer = new byte[512];
        var handlePtr = NativeMethods.dbento_historical_create(apiKey, errorBuffer, (nuint)errorBuffer.Length);

        if (handlePtr == IntPtr.Zero)
        {
            var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
            throw new DbentoException($"Failed to create historical client: {error}");
        }

        _handle = new HistoricalClientHandle(handlePtr);
    }

    /// <summary>
    /// Query historical data for a time range
    /// </summary>
    public async IAsyncEnumerable<Record> GetRangeAsync(
        string dataset,
        Schema schema,
        IEnumerable<string> symbols,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var channel = Channel.CreateUnbounded<Record>();
        var symbolArray = symbols.ToArray();

        // Convert times to nanoseconds since epoch
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        // Create callback delegates
        RecordCallbackDelegate recordCallback;
        unsafe
        {
            recordCallback = (recordBytes, recordLength, recordType, userData) =>
            {
            try
            {
                var bytes = new byte[recordLength];
                unsafe
                {
                    Marshal.Copy((IntPtr)recordBytes, bytes, 0, (int)recordLength);
                }

                var record = Record.FromBytes(bytes, recordType);
                channel.Writer.TryWrite(record);
            }
            catch (Exception)
            {
                // Log or handle error
            }
            };
        }

        // Start query on background thread
        var queryTask = Task.Run(() =>
        {
            try
            {
                byte[] errorBuffer = new byte[512];

                var result = NativeMethods.dbento_historical_get_range(
                    _handle,
                    dataset,
                    schema.ToSchemaString(),
                    symbolArray,
                    (nuint)symbolArray.Length,
                    startTimeNs,
                    endTimeNs,
                    recordCallback,
                    IntPtr.Zero,
                    errorBuffer,
                    (nuint)errorBuffer.Length);

                if (result != 0)
                {
                    var error = System.Text.Encoding.UTF8.GetString(errorBuffer).TrimEnd('\0');
                    throw new DbentoException($"Historical query failed: {error}", result);
                }
            }
            finally
            {
                channel.Writer.Complete();
            }
        }, cancellationToken);

        // Stream results
        await foreach (var record in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return record;
        }

        await queryTask;
    }

    /// <summary>
    /// Get metadata for a historical query
    /// Note: This feature is currently not fully implemented in the native layer
    /// </summary>
    public IMetadata? GetMetadata(
        string dataset,
        Schema schema,
        DateTimeOffset startTime,
        DateTimeOffset endTime)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Convert times to nanoseconds since epoch
        long startTimeNs = startTime.ToUnixTimeMilliseconds() * 1_000_000;
        long endTimeNs = endTime.ToUnixTimeMilliseconds() * 1_000_000;

        byte[] errorBuffer = new byte[512];

        var metadataHandle = NativeMethods.dbento_historical_get_metadata(
            _handle,
            dataset,
            schema.ToSchemaString(),
            startTimeNs,
            endTimeNs,
            errorBuffer,
            (nuint)errorBuffer.Length);

        if (metadataHandle == IntPtr.Zero)
        {
            // Native layer doesn't support metadata-only queries yet
            return null;
        }

        return new Metadata.Metadata(new MetadataHandle(metadataHandle));
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;

        _disposed = true;
        _handle?.Dispose();

        return ValueTask.CompletedTask;
    }
}
