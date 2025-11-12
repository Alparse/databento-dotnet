#include "databento_native.h"
#include "common_helpers.hpp"
#include <databento/live_threaded.hpp>
#include <databento/live.hpp>
#include <databento/record.hpp>
#include <databento/enums.hpp>
#include <memory>
#include <string>
#include <vector>
#include <cstring>
#include <exception>

namespace db = databento;
using databento_native::SafeStrCopy;

// ============================================================================
// Internal Wrapper Class
// ============================================================================
struct LiveClientWrapper {
    std::unique_ptr<db::LiveThreaded> client;
    RecordCallback record_callback = nullptr;
    MetadataCallback metadata_callback = nullptr;
    ErrorCallback error_callback = nullptr;
    void* user_data = nullptr;
    bool is_running = false;
    std::string dataset;
    std::string api_key;
    bool send_ts_out = false;
    db::VersionUpgradePolicy upgrade_policy = db::VersionUpgradePolicy::UpgradeToV3;
    int heartbeat_interval_secs = 30;

    explicit LiveClientWrapper(const std::string& key)
        : api_key(key) {}

    explicit LiveClientWrapper(
        const std::string& key,
        const std::string& ds,
        bool ts_out,
        db::VersionUpgradePolicy policy,
        int heartbeat_secs)
        : api_key(key)
        , dataset(ds)
        , send_ts_out(ts_out)
        , upgrade_policy(policy)
        , heartbeat_interval_secs(heartbeat_secs)
    {}

    ~LiveClientWrapper() {
        // LiveThreaded destructor handles cleanup
    }

    // Called by databento-cpp when a record is received
    db::KeepGoing OnRecord(const db::Record& record) {
        try {
            if (record_callback) {
                // Get the actual RecordHeader pointer (not the Record wrapper)
                const auto& header = record.Header();
                const uint8_t* bytes = reinterpret_cast<const uint8_t*>(&header);

                // Get record size based on its type
                size_t length = record.Size();

                // Get record type
                uint8_t type = static_cast<uint8_t>(record.RType());

                // Invoke callback - protected from exceptions
                record_callback(bytes, length, type, user_data);
            }
        }
        catch (const std::exception& ex) {
            // Report error through error callback if available
            if (error_callback) {
                error_callback(ex.what(), -999, user_data);
            }
            // Stop processing on exception
            is_running = false;
            return db::KeepGoing::Stop;
        }
        catch (...) {
            // Catch all exceptions including C# ones
            if (error_callback) {
                error_callback("Unknown exception in record callback", -998, user_data);
            }
            // Stop processing on exception
            is_running = false;
            return db::KeepGoing::Stop;
        }

        return is_running ? db::KeepGoing::Continue : db::KeepGoing::Stop;
    }

    // Called when an error occurs
    void OnError(const std::exception& e) {
        if (error_callback) {
            error_callback(e.what(), -1, user_data);
        }
    }
};

// ============================================================================
// C API Implementation
// ============================================================================

DATABENTO_API DbentoLiveClientHandle dbento_live_create(
    const char* api_key,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        if (!api_key) {
            SafeStrCopy(error_buffer, error_buffer_size, "API key cannot be null");
            return nullptr;
        }

        auto* wrapper = new LiveClientWrapper(api_key);
        return reinterpret_cast<DbentoLiveClientHandle>(wrapper);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API int dbento_live_subscribe(
    DbentoLiveClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<LiveClientWrapper*>(handle);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return -1;
        }

        if (!dataset || !schema) {
            SafeStrCopy(error_buffer, error_buffer_size, "Dataset and schema cannot be null");
            return -2;
        }

        // Store dataset for client creation
        wrapper->dataset = dataset;

        // Convert symbols to vector
        std::vector<std::string> symbol_vec;
        if (symbols && symbol_count > 0) {
            for (size_t i = 0; i < symbol_count; ++i) {
                if (symbols[i]) {
                    symbol_vec.emplace_back(symbols[i]);
                }
            }
        }

        // Parse schema from string to enum
        db::Schema schema_enum;
        std::string schema_str = schema;
        if (schema_str == "mbo") schema_enum = db::Schema::Mbo;
        else if (schema_str == "mbp-1") schema_enum = db::Schema::Mbp1;
        else if (schema_str == "mbp-10") schema_enum = db::Schema::Mbp10;
        else if (schema_str == "trades") schema_enum = db::Schema::Trades;
        else if (schema_str == "ohlcv-1s") schema_enum = db::Schema::Ohlcv1S;
        else if (schema_str == "ohlcv-1m") schema_enum = db::Schema::Ohlcv1M;
        else if (schema_str == "ohlcv-1h") schema_enum = db::Schema::Ohlcv1H;
        else if (schema_str == "ohlcv-1d") schema_enum = db::Schema::Ohlcv1D;
        else {
            SafeStrCopy(error_buffer, error_buffer_size, "Unknown schema type");
            return -3;
        }

        // Create client now that we have the dataset
        if (!wrapper->client) {
            auto builder = db::LiveThreaded::Builder()
                .SetKey(wrapper->api_key)
                .SetDataset(wrapper->dataset)
                .SetSendTsOut(wrapper->send_ts_out)
                .SetUpgradePolicy(wrapper->upgrade_policy);

            if (wrapper->heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(std::chrono::seconds(wrapper->heartbeat_interval_secs));
            }

            wrapper->client = std::make_unique<db::LiveThreaded>(builder.BuildThreaded());
        }

        // Subscribe using databento-cpp API (symbols, schema, stype)
        wrapper->client->Subscribe(symbol_vec, schema_enum, db::SType::RawSymbol);

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_start(
    DbentoLiveClientHandle handle,
    RecordCallback on_record,
    ErrorCallback on_error,
    void* user_data,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<LiveClientWrapper*>(handle);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return -1;
        }

        if (!on_record) {
            SafeStrCopy(error_buffer, error_buffer_size, "Record callback cannot be null");
            return -2;
        }

        // Store callbacks and user data
        wrapper->record_callback = on_record;
        wrapper->error_callback = on_error;
        wrapper->user_data = user_data;
        wrapper->is_running = true;

        // Start the client with a lambda that bridges to our callback
        wrapper->client->Start([wrapper](const db::Record& record) {
            return wrapper->OnRecord(record);
        });

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API void dbento_live_stop(DbentoLiveClientHandle handle)
{
    try {
        auto* wrapper = reinterpret_cast<LiveClientWrapper*>(handle);
        if (wrapper && wrapper->is_running) {
            wrapper->is_running = false;
            // The callback will return KeepGoing::Stop on next iteration
        }
    }
    catch (...) {
        // Swallow exceptions in cleanup
    }
}

DATABENTO_API void dbento_live_destroy(DbentoLiveClientHandle handle)
{
    try {
        auto* wrapper = reinterpret_cast<LiveClientWrapper*>(handle);
        if (wrapper) {
            wrapper->is_running = false;
            delete wrapper;
        }
    }
    catch (...) {
        // Swallow exceptions in cleanup
    }
}

// ============================================================================
// Extended API Functions (Phase 15)
// ============================================================================

DATABENTO_API DbentoLiveClientHandle dbento_live_create_ex(
    const char* api_key,
    const char* dataset,
    int send_ts_out,
    int upgrade_policy,
    int heartbeat_interval_secs,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        if (!api_key) {
            SafeStrCopy(error_buffer, error_buffer_size, "API key cannot be null");
            return nullptr;
        }

        std::string ds = dataset ? dataset : "";

        // Map upgrade policy: 0 = AsIs, 1 = UpgradeToV3
        auto policy = (upgrade_policy == 0)
            ? db::VersionUpgradePolicy::AsIs
            : db::VersionUpgradePolicy::UpgradeToV3;

        auto* wrapper = new LiveClientWrapper(
            api_key,
            ds,
            send_ts_out != 0,
            policy,
            heartbeat_interval_secs > 0 ? heartbeat_interval_secs : 30
        );

        // Create client immediately if we have a dataset
        if (!ds.empty()) {
            auto builder = db::LiveThreaded::Builder()
                .SetKey(wrapper->api_key)
                .SetDataset(wrapper->dataset)
                .SetSendTsOut(wrapper->send_ts_out)
                .SetUpgradePolicy(wrapper->upgrade_policy);

            if (wrapper->heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(std::chrono::seconds(wrapper->heartbeat_interval_secs));
            }

            wrapper->client = std::make_unique<db::LiveThreaded>(builder.BuildThreaded());
        }

        return reinterpret_cast<DbentoLiveClientHandle>(wrapper);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API int dbento_live_reconnect(
    DbentoLiveClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<LiveClientWrapper*>(handle);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return -1;
        }

        if (!wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Client not initialized");
            return -2;
        }

        // Use databento-cpp's Reconnect method
        wrapper->is_running = false;  // Stop current session
        wrapper->client->Reconnect();

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_resubscribe(
    DbentoLiveClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<LiveClientWrapper*>(handle);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return -1;
        }

        if (!wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Client not initialized");
            return -2;
        }

        // Use databento-cpp's Resubscribe method (resubscribes all tracked subscriptions)
        wrapper->client->Resubscribe();

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_start_ex(
    DbentoLiveClientHandle handle,
    MetadataCallback on_metadata,
    RecordCallback on_record,
    ErrorCallback on_error,
    void* user_data,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<LiveClientWrapper*>(handle);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return -1;
        }

        if (!on_record) {
            SafeStrCopy(error_buffer, error_buffer_size, "Record callback cannot be null");
            return -2;
        }

        // Store callbacks and user data
        wrapper->record_callback = on_record;
        wrapper->metadata_callback = on_metadata;
        wrapper->error_callback = on_error;
        wrapper->user_data = user_data;
        wrapper->is_running = true;

        // Start the client with metadata and record callbacks
        if (on_metadata) {
            wrapper->client->Start(
                [wrapper](const db::Metadata& metadata) {
                    try {
                        if (wrapper->metadata_callback) {
                            // Serialize metadata to JSON string for C# consumption
                            // For now, pass empty string (metadata serialization TBD)
                            wrapper->metadata_callback("", 0, wrapper->user_data);
                        }
                    }
                    catch (const std::exception& ex) {
                        // Report error through error callback
                        if (wrapper->error_callback) {
                            wrapper->error_callback(ex.what(), -997, wrapper->user_data);
                        }
                    }
                    catch (...) {
                        // Catch all exceptions
                        if (wrapper->error_callback) {
                            wrapper->error_callback("Unknown exception in metadata callback", -996, wrapper->user_data);
                        }
                    }
                },
                [wrapper](const db::Record& record) {
                    return wrapper->OnRecord(record);
                }
            );
        } else {
            // Start without metadata callback
            wrapper->client->Start([wrapper](const db::Record& record) {
                return wrapper->OnRecord(record);
            });
        }

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_subscribe_with_snapshot(
    DbentoLiveClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<LiveClientWrapper*>(handle);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return -1;
        }

        if (!dataset || !schema) {
            SafeStrCopy(error_buffer, error_buffer_size, "Dataset and schema cannot be null");
            return -2;
        }

        // Store dataset if client not yet created
        if (wrapper->dataset.empty()) {
            wrapper->dataset = dataset;
        }

        // Convert symbols to vector
        std::vector<std::string> symbol_vec;
        if (symbols && symbol_count > 0) {
            for (size_t i = 0; i < symbol_count; ++i) {
                if (symbols[i]) {
                    symbol_vec.emplace_back(symbols[i]);
                }
            }
        }

        // Parse schema from string to enum
        db::Schema schema_enum;
        std::string schema_str = schema;
        if (schema_str == "mbo") schema_enum = db::Schema::Mbo;
        else if (schema_str == "mbp-1") schema_enum = db::Schema::Mbp1;
        else if (schema_str == "mbp-10") schema_enum = db::Schema::Mbp10;
        else if (schema_str == "trades") schema_enum = db::Schema::Trades;
        else if (schema_str == "ohlcv-1s") schema_enum = db::Schema::Ohlcv1S;
        else if (schema_str == "ohlcv-1m") schema_enum = db::Schema::Ohlcv1M;
        else if (schema_str == "ohlcv-1h") schema_enum = db::Schema::Ohlcv1H;
        else if (schema_str == "ohlcv-1d") schema_enum = db::Schema::Ohlcv1D;
        else {
            SafeStrCopy(error_buffer, error_buffer_size, "Unknown schema type");
            return -3;
        }

        // Create client if needed
        if (!wrapper->client) {
            auto builder = db::LiveThreaded::Builder()
                .SetKey(wrapper->api_key)
                .SetDataset(wrapper->dataset)
                .SetSendTsOut(wrapper->send_ts_out)
                .SetUpgradePolicy(wrapper->upgrade_policy);

            if (wrapper->heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(std::chrono::seconds(wrapper->heartbeat_interval_secs));
            }

            wrapper->client = std::make_unique<db::LiveThreaded>(builder.BuildThreaded());
        }

        // Subscribe with snapshot
        wrapper->client->SubscribeWithSnapshot(symbol_vec, schema_enum, db::SType::RawSymbol);

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_get_connection_state(DbentoLiveClientHandle handle)
{
    try {
        auto* wrapper = reinterpret_cast<LiveClientWrapper*>(handle);
        if (!wrapper) {
            return 0;  // Disconnected
        }

        if (!wrapper->client) {
            return 0;  // Disconnected
        }

        // Check if running
        if (wrapper->is_running) {
            return 3;  // Streaming
        }

        // Client exists but not running
        return 2;  // Connected but not streaming
    }
    catch (...) {
        return 0;  // Disconnected on error
    }
}
