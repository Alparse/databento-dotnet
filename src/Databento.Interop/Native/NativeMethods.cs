using System.Runtime.InteropServices;
using Databento.Interop.Handles;

namespace Databento.Interop.Native;

/// <summary>
/// P/Invoke declarations for databento_native library
/// </summary>
public static partial class NativeMethods
{
    private const string LibName = "databento_native";

    // ========================================================================
    // Live Client API
    // ========================================================================

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_live_create(
        string apiKey,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int dbento_live_subscribe(
        LiveClientHandle handle,
        string dataset,
        string schema,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)]
        string[] symbols,
        nuint symbolCount,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName)]
    public static partial int dbento_live_start(
        LiveClientHandle handle,
        RecordCallbackDelegate onRecord,
        ErrorCallbackDelegate onError,
        IntPtr userData,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName)]
    public static partial void dbento_live_stop(LiveClientHandle handle);

    [LibraryImport(LibName)]
    public static partial void dbento_live_destroy(IntPtr handle);

    // ========================================================================
    // Historical Client API
    // ========================================================================

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_historical_create(
        string apiKey,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int dbento_historical_get_range(
        HistoricalClientHandle handle,
        string dataset,
        string schema,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)]
        string[] symbols,
        nuint symbolCount,
        long startTimeNs,
        long endTimeNs,
        RecordCallbackDelegate onRecord,
        IntPtr userData,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial int dbento_historical_get_range_to_file(
        HistoricalClientHandle handle,
        string filePath,
        string dataset,
        string schema,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)]
        string[] symbols,
        nuint symbolCount,
        long startTimeNs,
        long endTimeNs,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_historical_get_metadata(
        HistoricalClientHandle handle,
        string dataset,
        string schema,
        long startTimeNs,
        long endTimeNs,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName)]
    public static partial void dbento_historical_destroy(IntPtr handle);

    // ========================================================================
    // Metadata API
    // ========================================================================

    [LibraryImport(LibName)]
    public static partial int dbento_metadata_get_symbol_mapping(
        MetadataHandle handle,
        uint instrumentId,
        byte[] symbolBuffer,
        nuint symbolBufferSize);

    [LibraryImport(LibName)]
    public static partial void dbento_metadata_destroy(IntPtr handle);

    // ========================================================================
    // Metadata Query API
    // ========================================================================

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_list_publishers(
        HistoricalClientHandle handle,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_list_datasets(
        HistoricalClientHandle handle,
        string? venue,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_list_schemas(
        HistoricalClientHandle handle,
        string dataset,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_list_fields(
        HistoricalClientHandle handle,
        string encoding,
        string schema,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_get_dataset_condition(
        HistoricalClientHandle handle,
        string dataset,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_get_dataset_range(
        HistoricalClientHandle handle,
        string dataset,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial ulong dbento_metadata_get_record_count(
        HistoricalClientHandle handle,
        string dataset,
        string schema,
        long startTimeNs,
        long endTimeNs,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)]
        string[] symbols,
        nuint symbolCount,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial ulong dbento_metadata_get_billable_size(
        HistoricalClientHandle handle,
        string dataset,
        string schema,
        long startTimeNs,
        long endTimeNs,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)]
        string[] symbols,
        nuint symbolCount,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_get_cost(
        HistoricalClientHandle handle,
        string dataset,
        string schema,
        long startTimeNs,
        long endTimeNs,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)]
        string[] symbols,
        nuint symbolCount,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_get_billing_info(
        HistoricalClientHandle handle,
        string dataset,
        string schema,
        long startTimeNs,
        long endTimeNs,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)]
        string[] symbols,
        nuint symbolCount,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName)]
    public static partial void dbento_free_string(IntPtr strPtr);

    // ========================================================================
    // Symbol Map API
    // ========================================================================

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_create_symbol_map(
        MetadataHandle metadataHandle,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_metadata_create_symbol_map_for_date(
        MetadataHandle metadataHandle,
        int year,
        uint month,
        uint day,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName)]
    public static partial int dbento_ts_symbol_map_is_empty(TsSymbolMapHandle handle);

    [LibraryImport(LibName)]
    public static partial nuint dbento_ts_symbol_map_size(TsSymbolMapHandle handle);

    [LibraryImport(LibName)]
    public static partial int dbento_ts_symbol_map_find(
        TsSymbolMapHandle handle,
        int year,
        uint month,
        uint day,
        uint instrumentId,
        byte[] symbolBuffer,
        nuint symbolBufferSize);

    [LibraryImport(LibName)]
    public static partial void dbento_ts_symbol_map_destroy(IntPtr handle);

    [LibraryImport(LibName)]
    public static partial int dbento_pit_symbol_map_is_empty(PitSymbolMapHandle handle);

    [LibraryImport(LibName)]
    public static partial nuint dbento_pit_symbol_map_size(PitSymbolMapHandle handle);

    [LibraryImport(LibName)]
    public static partial int dbento_pit_symbol_map_find(
        PitSymbolMapHandle handle,
        uint instrumentId,
        byte[] symbolBuffer,
        nuint symbolBufferSize);

    [LibraryImport(LibName)]
    public static partial int dbento_pit_symbol_map_on_record(
        PitSymbolMapHandle handle,
        byte[] recordBytes,
        nuint recordLength);

    [LibraryImport(LibName)]
    public static partial void dbento_pit_symbol_map_destroy(IntPtr handle);

    // ========================================================================
    // Batch API
    // ========================================================================

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_batch_submit_job(
        HistoricalClientHandle handle,
        string dataset,
        string schema,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)]
        string[] symbols,
        nuint symbolCount,
        long startTimeNs,
        long endTimeNs,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_batch_list_jobs(
        HistoricalClientHandle handle,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_batch_list_files(
        HistoricalClientHandle handle,
        string jobId,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_batch_download_all(
        HistoricalClientHandle handle,
        string outputDir,
        string jobId,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_batch_download_file(
        HistoricalClientHandle handle,
        string outputDir,
        string jobId,
        string filename,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    // ========================================================================
    // DBN File Reader API
    // ========================================================================

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_dbn_file_open(
        string filePath,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_dbn_file_get_metadata(
        DbnFileReaderHandle handle,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName)]
    public static partial int dbento_dbn_file_next_record(
        DbnFileReaderHandle handle,
        byte[] recordBuffer,
        nuint recordBufferSize,
        out nuint recordLength,
        out byte recordType,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName)]
    public static partial void dbento_dbn_file_close(IntPtr handle);

    // ========================================================================
    // DBN File Writer API
    // ========================================================================

    [LibraryImport(LibName, StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr dbento_dbn_file_create(
        string filePath,
        string metadataJson,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName)]
    public static partial int dbento_dbn_file_write_record(
        DbnFileWriterHandle handle,
        byte[] recordBytes,
        nuint recordLength,
        byte[]? errorBuffer,
        nuint errorBufferSize);

    [LibraryImport(LibName)]
    public static partial void dbento_dbn_file_close_writer(IntPtr handle);
}
