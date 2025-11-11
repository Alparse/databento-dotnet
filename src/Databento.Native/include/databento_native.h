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

#ifdef __cplusplus
}
#endif

#endif // DATABENTO_NATIVE_H
