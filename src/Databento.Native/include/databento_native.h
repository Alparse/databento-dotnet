#ifndef DATABENTO_NATIVE_H
#define DATABENTO_NATIVE_H

#include <stddef.h>
#include <stdint.h>

#ifdef _WIN32
    #ifdef DATABENTO_EXPORTS
        #define DATABENTO_API __declspec(dllexport)
    #else
        #define DATABENTO_API __declspec(dllimport)
    #endif
#else
    #define DATABENTO_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

// ============================================================================
// Opaque Handles
// ============================================================================
typedef void* DbentoLiveClientHandle;
typedef void* DbentoHistoricalClientHandle;
typedef void* DbentoMetadataHandle;
typedef void* DbentoTsSymbolMapHandle;
typedef void* DbentoPitSymbolMapHandle;
typedef void* DbnFileReaderHandle;
typedef void* DbnFileWriterHandle;
typedef void* DbentoSymbologyResolutionHandle;
typedef void* DbentoUnitPricesHandle;

// ============================================================================
// Callback Types
// ============================================================================

/**
 * Callback for received records
 * @param record_bytes Raw record data (DBN format)
 * @param record_length Length of record in bytes
 * @param record_type Record type identifier (schema type)
 * @param user_data User-provided context pointer
 */
typedef void (*RecordCallback)(
    const uint8_t* record_bytes,
    size_t record_length,
    uint8_t record_type,
    void* user_data
);

/**
 * Callback for errors
 * @param error_message Error description
 * @param error_code Error code (negative values indicate errors)
 * @param user_data User-provided context pointer
 */
typedef void (*ErrorCallback)(
    const char* error_message,
    int error_code,
    void* user_data
);

/**
 * Callback for session metadata (Phase 15)
 * @param metadata_json JSON string containing session metadata
 * @param metadata_length Length of metadata string in bytes
 * @param user_data User-provided context pointer
 */
typedef void (*MetadataCallback)(
    const char* metadata_json,
    size_t metadata_length,
    void* user_data
);

// ============================================================================
// Live Client API
// ============================================================================

/**
 * Create a live client (threaded mode)
 * @param api_key Databento API key (required)
 * @param error_buffer Buffer for error messages (can be NULL)
 * @param error_buffer_size Size of error buffer
 * @return Handle to live client, or NULL on failure
 */
DATABENTO_API DbentoLiveClientHandle dbento_live_create(
    const char* api_key,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Subscribe to data streams
 * @param handle Live client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (e.g., "trades", "mbp-1", "ohlcv-1s")
 * @param symbols Array of symbol strings (NULL-terminated)
 * @param symbol_count Number of symbols in array
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_subscribe(
    DbentoLiveClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Start receiving data (blocking call - runs until stopped)
 * @param handle Live client handle
 * @param on_record Callback invoked for each received record
 * @param on_error Callback invoked on errors
 * @param user_data User context passed to callbacks
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_start(
    DbentoLiveClientHandle handle,
    RecordCallback on_record,
    ErrorCallback on_error,
    void* user_data,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Stop receiving data
 * @param handle Live client handle
 */
DATABENTO_API void dbento_live_stop(DbentoLiveClientHandle handle);

/**
 * Destroy live client and free resources
 * @param handle Live client handle
 */
DATABENTO_API void dbento_live_destroy(DbentoLiveClientHandle handle);

/**
 * Create a live client with extended configuration (Phase 15)
 * @param api_key Databento API key (required)
 * @param dataset Default dataset for subscriptions (can be NULL)
 * @param send_ts_out Include gateway send timestamps (0=false, non-zero=true)
 * @param upgrade_policy Version upgrade policy (0=AsIs, 1=Upgrade)
 * @param heartbeat_interval_secs Heartbeat interval in seconds (0 or negative=default 30s)
 * @param error_buffer Buffer for error messages (can be NULL)
 * @param error_buffer_size Size of error buffer
 * @return Handle to live client, or NULL on failure
 */
DATABENTO_API DbentoLiveClientHandle dbento_live_create_ex(
    const char* api_key,
    const char* dataset,
    int send_ts_out,
    int upgrade_policy,
    int heartbeat_interval_secs,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Reconnect to the gateway after disconnection (Phase 15)
 * @param handle Live client handle
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_reconnect(
    DbentoLiveClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Resubscribe to all previously tracked subscriptions (Phase 15)
 * @param handle Live client handle
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_resubscribe(
    DbentoLiveClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Start receiving data with metadata callback support (Phase 15)
 * @param handle Live client handle
 * @param on_metadata Callback invoked once for session metadata (can be NULL)
 * @param on_record Callback invoked for each received record
 * @param on_error Callback invoked on errors
 * @param user_data User context passed to callbacks
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_start_ex(
    DbentoLiveClientHandle handle,
    MetadataCallback on_metadata,
    RecordCallback on_record,
    ErrorCallback on_error,
    void* user_data,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Subscribe to data streams with initial snapshot (Phase 15)
 * @param handle Live client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (e.g., "trades", "mbp-1", "ohlcv-1s")
 * @param symbols Array of symbol strings (NULL-terminated)
 * @param symbol_count Number of symbols in array
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_subscribe_with_snapshot(
    DbentoLiveClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Subscribe to data streams with intraday replay from a specific start time
 * @param handle Live client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (e.g., "trades", "mbp-1", "ohlcv-1s")
 * @param symbols Array of symbol strings (NULL-terminated)
 * @param symbol_count Number of symbols in array
 * @param start_time_ns Start time in nanoseconds since Unix epoch (0 for full replay)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_subscribe_with_replay(
    DbentoLiveClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Get current connection state (Phase 15)
 * @param handle Live client handle
 * @return Connection state: 0=Disconnected, 1=Connecting, 2=Connected, 3=Streaming
 */
DATABENTO_API int dbento_live_get_connection_state(DbentoLiveClientHandle handle);

// ============================================================================
// LiveBlocking Client API (Pull-based)
// ============================================================================

/**
 * Create a LiveBlocking client with extended configuration
 * @param api_key Databento API key (required)
 * @param dataset Default dataset for subscriptions (required)
 * @param send_ts_out Include gateway send timestamps (0=false, non-zero=true)
 * @param upgrade_policy Version upgrade policy (0=AsIs, 1=Upgrade)
 * @param heartbeat_interval_secs Heartbeat interval in seconds (0 or negative=default 30s)
 * @param error_buffer Buffer for error messages (can be NULL)
 * @param error_buffer_size Size of error buffer
 * @return Handle to LiveBlocking client, or NULL on failure
 */
DATABENTO_API DbentoLiveClientHandle dbento_live_blocking_create_ex(
    const char* api_key,
    const char* dataset,
    int send_ts_out,
    int upgrade_policy,
    int heartbeat_interval_secs,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Subscribe to data streams (LiveBlocking)
 * @param handle LiveBlocking client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (e.g., "trades", "mbp-1", "ohlcv-1s")
 * @param symbols Array of symbol strings
 * @param symbol_count Number of symbols in array
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_blocking_subscribe(
    DbentoLiveClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Subscribe to data streams with intraday replay (LiveBlocking)
 * @param handle LiveBlocking client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (e.g., "trades", "mbp-1", "ohlcv-1s")
 * @param symbols Array of symbol strings
 * @param symbol_count Number of symbols in array
 * @param start_time_ns Start time in nanoseconds since Unix epoch (0 for full replay)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_blocking_subscribe_with_replay(
    DbentoLiveClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Subscribe to data streams with initial snapshot (LiveBlocking, MBO only)
 * @param handle LiveBlocking client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (must be "mbo")
 * @param symbols Array of symbol strings
 * @param symbol_count Number of symbols in array
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_blocking_subscribe_with_snapshot(
    DbentoLiveClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Start the LiveBlocking session - blocks until metadata is decoded
 * Returns DBN metadata as JSON string in metadata_buffer
 * @param handle LiveBlocking client handle
 * @param metadata_buffer Buffer to receive JSON metadata
 * @param metadata_buffer_size Size of metadata buffer (recommend 64KB)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_blocking_start(
    DbentoLiveClientHandle handle,
    char* metadata_buffer,
    size_t metadata_buffer_size,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Get next record from LiveBlocking client - blocks until record received or timeout
 * @param handle LiveBlocking client handle
 * @param record_buffer Buffer to receive record data
 * @param record_buffer_size Size of record buffer (recommend 256KB)
 * @param out_record_length Receives actual record length
 * @param out_record_type Receives record type (RType)
 * @param timeout_ms Timeout in milliseconds (-1 for no timeout)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, 1 on timeout, negative on error
 */
DATABENTO_API int dbento_live_blocking_next_record(
    DbentoLiveClientHandle handle,
    uint8_t* record_buffer,
    size_t record_buffer_size,
    size_t* out_record_length,
    uint8_t* out_record_type,
    int timeout_ms,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Reconnect LiveBlocking client to the gateway
 * @param handle LiveBlocking client handle
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_blocking_reconnect(
    DbentoLiveClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Resubscribe to all stored subscriptions (LiveBlocking)
 * @param handle LiveBlocking client handle
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_live_blocking_resubscribe(
    DbentoLiveClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Stop the LiveBlocking session
 * @param handle LiveBlocking client handle
 */
DATABENTO_API void dbento_live_blocking_stop(DbentoLiveClientHandle handle);

/**
 * Destroy LiveBlocking client and free resources
 * @param handle LiveBlocking client handle
 */
DATABENTO_API void dbento_live_blocking_destroy(DbentoLiveClientHandle handle);

// ============================================================================
// Historical Client API
// ============================================================================

/**
 * Create a historical data client
 * @param api_key Databento API key (required)
 * @param error_buffer Buffer for error messages (can be NULL)
 * @param error_buffer_size Size of error buffer
 * @return Handle to historical client, or NULL on failure
 */
DATABENTO_API DbentoHistoricalClientHandle dbento_historical_create(
    const char* api_key,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Query historical time series data
 * @param handle Historical client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (e.g., "trades", "mbp-1")
 * @param symbols Array of symbol strings
 * @param symbol_count Number of symbols
 * @param start_time_ns Start time (nanoseconds since Unix epoch)
 * @param end_time_ns End time (nanoseconds since Unix epoch)
 * @param on_record Callback invoked for each historical record
 * @param user_data User context passed to callback
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_historical_get_range(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    int64_t end_time_ns,
    RecordCallback on_record,
    void* user_data,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Query historical time series data and save directly to a DBN file
 * @param handle Historical client handle
 * @param file_path Output file path for DBN file
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (e.g., "trades", "mbp-1")
 * @param symbols Array of symbol strings
 * @param symbol_count Number of symbols
 * @param start_time_ns Start time (nanoseconds since Unix epoch)
 * @param end_time_ns End time (nanoseconds since Unix epoch)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_historical_get_range_to_file(
    DbentoHistoricalClientHandle handle,
    const char* file_path,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    int64_t end_time_ns,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Query historical time series data with symbology type filtering
 * @param handle Historical client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (e.g., "trades", "mbp-1")
 * @param symbols Array of symbol strings
 * @param symbol_count Number of symbols
 * @param start_time_ns Start time (nanoseconds since Unix epoch)
 * @param end_time_ns End time (nanoseconds since Unix epoch)
 * @param stype_in Input symbology type (e.g., "parent", "raw_symbol", "instrument_id")
 * @param stype_out Output symbology type (e.g., "raw_symbol", "instrument_id")
 * @param limit Maximum number of records to return (0 = unlimited)
 * @param on_record Callback function for each record
 * @param user_data User context passed to callback
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_historical_get_range_with_symbology(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char* stype_in,
    const char* stype_out,
    uint64_t limit,
    RecordCallback on_record,
    void* user_data,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Query historical time series data with symbology type filtering and save to file
 * @param handle Historical client handle
 * @param file_path Output file path for DBN file
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param schema Schema name (e.g., "trades", "mbp-1")
 * @param symbols Array of symbol strings
 * @param symbol_count Number of symbols
 * @param start_time_ns Start time (nanoseconds since Unix epoch)
 * @param end_time_ns End time (nanoseconds since Unix epoch)
 * @param stype_in Input symbology type (e.g., "parent", "raw_symbol", "instrument_id")
 * @param stype_out Output symbology type (e.g., "raw_symbol", "instrument_id")
 * @param limit Maximum number of records to return (0 = unlimited)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_historical_get_range_to_file_with_symbology(
    DbentoHistoricalClientHandle handle,
    const char* file_path,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char* stype_in,
    const char* stype_out,
    uint64_t limit,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Get metadata for a historical query
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param schema Schema name
 * @param start_time_ns Start time (nanoseconds since Unix epoch)
 * @param end_time_ns End time (nanoseconds since Unix epoch)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Metadata handle, or NULL on failure
 */
DATABENTO_API DbentoMetadataHandle dbento_historical_get_metadata(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    int64_t start_time_ns,
    int64_t end_time_ns,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Destroy historical client and free resources
 * @param handle Historical client handle
 */
DATABENTO_API void dbento_historical_destroy(DbentoHistoricalClientHandle handle);

// ============================================================================
// Metadata API
// ============================================================================

/**
 * Get symbol mapping from metadata
 * @param handle Metadata handle
 * @param instrument_id Instrument ID to look up
 * @param symbol_buffer Buffer to receive symbol string
 * @param symbol_buffer_size Size of symbol buffer
 * @return 0 on success, negative error code if not found
 */
DATABENTO_API int dbento_metadata_get_symbol_mapping(
    DbentoMetadataHandle handle,
    uint32_t instrument_id,
    char* symbol_buffer,
    size_t symbol_buffer_size
);

/**
 * Destroy metadata handle and free resources
 * @param handle Metadata handle
 */
DATABENTO_API void dbento_metadata_destroy(DbentoMetadataHandle handle);

/**
 * List all available datasets, optionally filtered by venue
 * @param handle Historical client handle
 * @param venue Optional venue filter (can be NULL for all datasets)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON array of dataset names as string, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_metadata_list_datasets(
    DbentoHistoricalClientHandle handle,
    const char* venue,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * List all publishers
 * @param handle Historical client handle
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON array of publisher information, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_metadata_list_publishers(
    DbentoHistoricalClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * List all schemas available for a dataset
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON array of schema names, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_metadata_list_schemas(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * List all fields for an encoding and schema
 * @param handle Historical client handle
 * @param encoding Encoding type (e.g., "dbn", "csv", "json")
 * @param schema Schema name
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON array of field information, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_metadata_list_fields(
    DbentoHistoricalClientHandle handle,
    const char* encoding,
    const char* schema,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * Get dataset availability condition
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON object with condition info, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_metadata_get_dataset_condition(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * Get dataset condition for a specific date range
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param start_date Start date in ISO 8601 format (YYYY-MM-DD)
 * @param end_date End date in ISO 8601 format (YYYY-MM-DD), or NULL for open-ended range
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON array of condition details per date, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_metadata_get_dataset_condition_with_date_range(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* start_date,
    const char* end_date,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * Get dataset time range
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON object with dataset range, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_metadata_get_dataset_range(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * Get record count for a query
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param schema Schema name
 * @param start_time_ns Start time (nanoseconds since epoch)
 * @param end_time_ns End time (nanoseconds since epoch)
 * @param symbols Array of symbols
 * @param symbol_count Number of symbols
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Record count, or UINT64_MAX on failure
 */
DATABENTO_API uint64_t dbento_metadata_get_record_count(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * Get billable size for a query
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param schema Schema name
 * @param start_time_ns Start time (nanoseconds since epoch)
 * @param end_time_ns End time (nanoseconds since epoch)
 * @param symbols Array of symbols
 * @param symbol_count Number of symbols
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Billable size in bytes, or UINT64_MAX on failure
 */
DATABENTO_API uint64_t dbento_metadata_get_billable_size(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * Get cost estimate for a query
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param schema Schema name
 * @param start_time_ns Start time (nanoseconds since epoch)
 * @param end_time_ns End time (nanoseconds since epoch)
 * @param symbols Array of symbols
 * @param symbol_count Number of symbols
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Cost as JSON string, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_metadata_get_cost(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size);

/**
 * Get combined billing information for a query
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param schema Schema name
 * @param start_time_ns Start time (nanoseconds since epoch)
 * @param end_time_ns End time (nanoseconds since epoch)
 * @param symbols Array of symbols
 * @param symbol_count Number of symbols
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON object with billing info, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_metadata_get_billing_info(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size);

// ============================================================================
// Symbol Map API
// ============================================================================

/**
 * Create a timeseries symbol map from metadata
 * @param metadata_handle Metadata handle to create symbol map from
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Handle to timeseries symbol map, or NULL on failure
 */
DATABENTO_API DbentoTsSymbolMapHandle dbento_metadata_create_symbol_map(
    DbentoMetadataHandle metadata_handle,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Create a point-in-time symbol map for a specific date
 * @param metadata_handle Metadata handle to create symbol map from
 * @param year Year (e.g., 2024)
 * @param month Month (1-12)
 * @param day Day (1-31)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Handle to point-in-time symbol map, or NULL on failure
 */
DATABENTO_API DbentoPitSymbolMapHandle dbento_metadata_create_symbol_map_for_date(
    DbentoMetadataHandle metadata_handle,
    int year,
    unsigned int month,
    unsigned int day,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Check if timeseries symbol map is empty
 * @param handle TsSymbolMap handle
 * @return 1 if empty, 0 if not empty, -1 on error
 */
DATABENTO_API int dbento_ts_symbol_map_is_empty(DbentoTsSymbolMapHandle handle);

/**
 * Get size of timeseries symbol map
 * @param handle TsSymbolMap handle
 * @return Number of mappings, or 0 on error
 */
DATABENTO_API size_t dbento_ts_symbol_map_size(DbentoTsSymbolMapHandle handle);

/**
 * Find symbol in timeseries symbol map
 * @param handle TsSymbolMap handle
 * @param year Year (e.g., 2024)
 * @param month Month (1-12)
 * @param day Day (1-31)
 * @param instrument_id Instrument ID to look up
 * @param symbol_buffer Buffer to receive symbol string
 * @param symbol_buffer_size Size of symbol buffer
 * @return 0 on success, negative error code if not found
 */
DATABENTO_API int dbento_ts_symbol_map_find(
    DbentoTsSymbolMapHandle handle,
    int year,
    unsigned int month,
    unsigned int day,
    uint32_t instrument_id,
    char* symbol_buffer,
    size_t symbol_buffer_size
);

/**
 * Destroy timeseries symbol map and free resources
 * @param handle TsSymbolMap handle
 */
DATABENTO_API void dbento_ts_symbol_map_destroy(DbentoTsSymbolMapHandle handle);

/**
 * Check if point-in-time symbol map is empty
 * @param handle PitSymbolMap handle
 * @return 1 if empty, 0 if not empty, -1 on error
 */
DATABENTO_API int dbento_pit_symbol_map_is_empty(DbentoPitSymbolMapHandle handle);

/**
 * Get size of point-in-time symbol map
 * @param handle PitSymbolMap handle
 * @return Number of mappings, or 0 on error
 */
DATABENTO_API size_t dbento_pit_symbol_map_size(DbentoPitSymbolMapHandle handle);

/**
 * Find symbol in point-in-time symbol map
 * @param handle PitSymbolMap handle
 * @param instrument_id Instrument ID to look up
 * @param symbol_buffer Buffer to receive symbol string
 * @param symbol_buffer_size Size of symbol buffer
 * @return 0 on success, negative error code if not found
 */
DATABENTO_API int dbento_pit_symbol_map_find(
    DbentoPitSymbolMapHandle handle,
    uint32_t instrument_id,
    char* symbol_buffer,
    size_t symbol_buffer_size
);

/**
 * Update point-in-time symbol map from a record (for live data)
 * @param handle PitSymbolMap handle
 * @param record_bytes Raw record data (DBN format)
 * @param record_length Length of record in bytes
 * @return 0 on success, negative error code on failure
 */
DATABENTO_API int dbento_pit_symbol_map_on_record(
    DbentoPitSymbolMapHandle handle,
    const uint8_t* record_bytes,
    size_t record_length
);

/**
 * Destroy point-in-time symbol map and free resources
 * @param handle PitSymbolMap handle
 */
DATABENTO_API void dbento_pit_symbol_map_destroy(DbentoPitSymbolMapHandle handle);

// ============================================================================
// Batch API
// ============================================================================

/**
 * Submit a batch job (basic version with defaults)
 * WARNING: This will incur a cost
 * @param handle Historical client handle
 * @param dataset Dataset name
 * @param schema Schema type string
 * @param symbols Array of symbol strings
 * @param symbol_count Number of symbols
 * @param start_time_ns Start time (nanoseconds since epoch)
 * @param end_time_ns End time (nanoseconds since epoch)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON string describing the batch job, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_batch_submit_job(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    int64_t end_time_ns,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * List batch jobs
 * @param handle Historical client handle
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON array of batch jobs, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_batch_list_jobs(
    DbentoHistoricalClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * List files for a batch job
 * @param handle Historical client handle
 * @param job_id Job identifier
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON array of file descriptions, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_batch_list_files(
    DbentoHistoricalClientHandle handle,
    const char* job_id,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Download all files from a batch job
 * @param handle Historical client handle
 * @param output_dir Output directory path
 * @param job_id Job identifier
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON array of downloaded file paths, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_batch_download_all(
    DbentoHistoricalClientHandle handle,
    const char* output_dir,
    const char* job_id,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Download a specific file from a batch job
 * @param handle Historical client handle
 * @param output_dir Output directory path
 * @param job_id Job identifier
 * @param filename Specific filename to download
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return File path as string, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_batch_download_file(
    DbentoHistoricalClientHandle handle,
    const char* output_dir,
    const char* job_id,
    const char* filename,
    char* error_buffer,
    size_t error_buffer_size
);

// ============================================================================
// DBN File Reader API
// ============================================================================

/**
 * Open a DBN file for reading
 * @param file_path Path to the DBN file
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Handle to DBN file reader, or NULL on failure
 */
DATABENTO_API DbnFileReaderHandle dbento_dbn_file_open(
    const char* file_path,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Get metadata from a DBN file
 * @param handle DBN file reader handle
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return JSON string describing metadata, or NULL on failure (must be freed with dbento_free_string)
 */
DATABENTO_API const char* dbento_dbn_file_get_metadata(
    DbnFileReaderHandle handle,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Read the next record from a DBN file
 * @param handle DBN file reader handle
 * @param record_buffer Buffer to receive record data
 * @param record_buffer_size Size of record buffer
 * @param record_length Output: actual length of the record
 * @param record_type Output: record type identifier
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, 1 on EOF, negative on error
 */
DATABENTO_API int dbento_dbn_file_next_record(
    DbnFileReaderHandle handle,
    uint8_t* record_buffer,
    size_t record_buffer_size,
    size_t* record_length,
    uint8_t* record_type,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Close a DBN file and free resources
 * @param handle DBN file reader handle
 */
DATABENTO_API void dbento_dbn_file_close(DbnFileReaderHandle handle);

// ============================================================================
// DBN File Writer API
// ============================================================================

/**
 * Create a DBN file writer for writing records
 * @param file_path Path where the DBN file will be created
 * @param metadata_json JSON string containing DBN metadata
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Handle to DBN file writer, or NULL on failure
 */
DATABENTO_API DbnFileWriterHandle dbento_dbn_file_create(
    const char* file_path,
    const char* metadata_json,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Write a record to a DBN file
 * @param handle DBN file writer handle
 * @param record_bytes Raw record data (DBN format)
 * @param record_length Length of record in bytes
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return 0 on success, negative on error
 */
DATABENTO_API int dbento_dbn_file_write_record(
    DbnFileWriterHandle handle,
    const uint8_t* record_bytes,
    size_t record_length,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Close and finalize a DBN file writer
 * @param handle DBN file writer handle
 */
DATABENTO_API void dbento_dbn_file_close_writer(DbnFileWriterHandle handle);

// ============================================================================
// Symbology Resolution API
// ============================================================================

/**
 * Resolve symbols from one symbology type to another over a date range
 * @param handle Historical client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param symbols Array of symbol strings to resolve
 * @param symbol_count Number of symbols in array
 * @param stype_in Input symbology type (e.g., "raw_symbol", "instrument_id")
 * @param stype_out Output symbology type (e.g., "raw_symbol", "continuous")
 * @param start_date Start date in YYYY-MM-DD format (inclusive)
 * @param end_date End date in YYYY-MM-DD format (exclusive)
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Handle to symbology resolution result, or NULL on failure
 */
DATABENTO_API DbentoSymbologyResolutionHandle dbento_historical_symbology_resolve(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char** symbols,
    size_t symbol_count,
    const char* stype_in,
    const char* stype_out,
    const char* start_date,
    const char* end_date,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Get the number of mappings in the resolution result
 * @param handle Symbology resolution handle
 * @return Number of mappings
 */
DATABENTO_API size_t dbento_symbology_resolution_mappings_count(
    DbentoSymbologyResolutionHandle handle
);

/**
 * Get the symbol key for a mapping at a given index
 * @param handle Symbology resolution handle
 * @param index Index of the mapping
 * @param key_buffer Buffer to store the symbol key
 * @param key_buffer_size Size of the buffer
 * @return 0 on success, -1 on error, -2 if index out of bounds
 */
DATABENTO_API int dbento_symbology_resolution_get_mapping_key(
    DbentoSymbologyResolutionHandle handle,
    size_t index,
    char* key_buffer,
    size_t key_buffer_size
);

/**
 * Get the number of intervals for a given symbol mapping
 * @param handle Symbology resolution handle
 * @param symbol_key The symbol key to query
 * @return Number of intervals for this symbol
 */
DATABENTO_API size_t dbento_symbology_resolution_get_intervals_count(
    DbentoSymbologyResolutionHandle handle,
    const char* symbol_key
);

/**
 * Get a specific mapping interval
 * @param handle Symbology resolution handle
 * @param symbol_key The symbol key to query
 * @param interval_index Index of the interval
 * @param start_date_buffer Buffer for start date (YYYY-MM-DD format)
 * @param start_date_buffer_size Size of start date buffer
 * @param end_date_buffer Buffer for end date (YYYY-MM-DD format)
 * @param end_date_buffer_size Size of end date buffer
 * @param symbol_buffer Buffer for the resolved symbol
 * @param symbol_buffer_size Size of symbol buffer
 * @return 0 on success, -1 on error, -2 if key not found, -3 if index out of bounds
 */
DATABENTO_API int dbento_symbology_resolution_get_interval(
    DbentoSymbologyResolutionHandle handle,
    const char* symbol_key,
    size_t interval_index,
    char* start_date_buffer,
    size_t start_date_buffer_size,
    char* end_date_buffer,
    size_t end_date_buffer_size,
    char* symbol_buffer,
    size_t symbol_buffer_size
);

/**
 * Get the number of partial symbols (resolved for some but not all dates)
 * @param handle Symbology resolution handle
 * @return Number of partial symbols
 */
DATABENTO_API size_t dbento_symbology_resolution_partial_count(
    DbentoSymbologyResolutionHandle handle
);

/**
 * Get a partial symbol by index
 * @param handle Symbology resolution handle
 * @param index Index of the partial symbol
 * @param symbol_buffer Buffer to store the symbol
 * @param symbol_buffer_size Size of the buffer
 * @return 0 on success, -1 on error, -2 if index out of bounds
 */
DATABENTO_API int dbento_symbology_resolution_get_partial(
    DbentoSymbologyResolutionHandle handle,
    size_t index,
    char* symbol_buffer,
    size_t symbol_buffer_size
);

/**
 * Get the number of not found symbols
 * @param handle Symbology resolution handle
 * @return Number of not found symbols
 */
DATABENTO_API size_t dbento_symbology_resolution_not_found_count(
    DbentoSymbologyResolutionHandle handle
);

/**
 * Get a not found symbol by index
 * @param handle Symbology resolution handle
 * @param index Index of the not found symbol
 * @param symbol_buffer Buffer to store the symbol
 * @param symbol_buffer_size Size of the buffer
 * @return 0 on success, -1 on error, -2 if index out of bounds
 */
DATABENTO_API int dbento_symbology_resolution_get_not_found(
    DbentoSymbologyResolutionHandle handle,
    size_t index,
    char* symbol_buffer,
    size_t symbol_buffer_size
);

/**
 * Get the input symbology type (stype_in)
 * @param handle Symbology resolution handle
 * @return SType enum value as int, or -1 on error
 */
DATABENTO_API int dbento_symbology_resolution_get_stype_in(
    DbentoSymbologyResolutionHandle handle
);

/**
 * Get the output symbology type (stype_out)
 * @param handle Symbology resolution handle
 * @return SType enum value as int, or -1 on error
 */
DATABENTO_API int dbento_symbology_resolution_get_stype_out(
    DbentoSymbologyResolutionHandle handle
);

/**
 * Destroy a symbology resolution handle and free resources
 * @param handle Symbology resolution handle
 */
DATABENTO_API void dbento_symbology_resolution_destroy(
    DbentoSymbologyResolutionHandle handle
);

// ============================================================================
// Unit Prices API
// ============================================================================

/**
 * Get unit prices per schema for all feed modes
 * @param handle Historical client handle
 * @param dataset Dataset name (e.g., "GLBX.MDP3")
 * @param error_buffer Buffer for error messages
 * @param error_buffer_size Size of error buffer
 * @return Handle to unit prices result, or NULL on failure
 */
DATABENTO_API DbentoUnitPricesHandle dbento_historical_list_unit_prices(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    char* error_buffer,
    size_t error_buffer_size
);

/**
 * Get the number of feed modes in the unit prices result
 * @param handle Unit prices handle
 * @return Number of feed modes
 */
DATABENTO_API size_t dbento_unit_prices_get_modes_count(
    DbentoUnitPricesHandle handle
);

/**
 * Get the feed mode for a specific mode index
 * @param handle Unit prices handle
 * @param mode_index Index of the feed mode
 * @return Feed mode as int (0=Historical, 1=HistoricalStreaming, 2=Live), or -1 on error
 */
DATABENTO_API int dbento_unit_prices_get_mode(
    DbentoUnitPricesHandle handle,
    size_t mode_index
);

/**
 * Get the number of schemas with prices for a specific feed mode
 * @param handle Unit prices handle
 * @param mode_index Index of the feed mode
 * @return Number of schemas with prices
 */
DATABENTO_API size_t dbento_unit_prices_get_schema_count(
    DbentoUnitPricesHandle handle,
    size_t mode_index
);

/**
 * Get the schema and price for a specific index within a feed mode
 * @param handle Unit prices handle
 * @param mode_index Index of the feed mode
 * @param schema_index Index of the schema within that mode
 * @param out_schema Pointer to receive schema enum value
 * @param out_price Pointer to receive price (USD)
 * @return 0 on success, -1 on error, -2 if index out of bounds
 */
DATABENTO_API int dbento_unit_prices_get_schema_price(
    DbentoUnitPricesHandle handle,
    size_t mode_index,
    size_t schema_index,
    int* out_schema,
    double* out_price
);

/**
 * Destroy a unit prices handle and free resources
 * @param handle Unit prices handle
 */
DATABENTO_API void dbento_unit_prices_destroy(
    DbentoUnitPricesHandle handle
);

// ============================================================================
// Memory Management
// ============================================================================

/**
 * Free a string allocated by the native library
 * Must be called for any char* returned by batch API, symbology, etc.
 * @param str String pointer to free (can be NULL)
 */
DATABENTO_API void dbento_free_string(char* str);

#ifdef __cplusplus
}
#endif

#endif // DATABENTO_NATIVE_H
