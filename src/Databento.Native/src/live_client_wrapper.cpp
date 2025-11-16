#include "databento_native.h"
#include "common_helpers.hpp"
#include "handle_validation.hpp"
#include <databento/live_threaded.hpp>
#include <databento/live.hpp>
#include <databento/record.hpp>
#include <databento/enums.hpp>
#include <memory>
#include <string>
#include <sstream>
#include <vector>
#include <cstring>
#include <exception>
#include <mutex>
#include <atomic>
#include <thread>
#include <chrono>

namespace db = databento;
using databento_native::SafeStrCopy;
using databento_native::ParseSchema;
using databento_native::ValidateNonEmptyString;
using databento_native::ValidateSymbolArray;

// ============================================================================
// Internal Wrapper Class
// ============================================================================
struct LiveClientWrapper {
    std::unique_ptr<db::LiveThreaded> client;
    RecordCallback record_callback = nullptr;
    MetadataCallback metadata_callback = nullptr;
    ErrorCallback error_callback = nullptr;
    void* user_data = nullptr;
    std::atomic<bool> is_running{false};  // Atomic for thread-safe access
    std::mutex callback_mutex;  // Protect callback invocations
    std::once_flag client_init_flag;  // Ensure single client initialization
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

    // Thread-safe client initialization using std::call_once
    void EnsureClientCreated() {
        std::call_once(client_init_flag, [this]() {
            auto builder = db::LiveThreaded::Builder()
                .SetKey(api_key)
                .SetDataset(dataset)
                .SetSendTsOut(send_ts_out)
                .SetUpgradePolicy(upgrade_policy);

            if (heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(
                    std::chrono::seconds(heartbeat_interval_secs));
            }

            client = std::make_unique<db::LiveThreaded>(builder.BuildThreaded());
        });
    }

    // Called by databento-cpp when a record is received
    db::KeepGoing OnRecord(const db::Record& record) {
        // Lock for thread-safe callback access
        std::lock_guard<std::mutex> lock(callback_mutex);

        // Check if still running
        if (!is_running.load(std::memory_order_acquire)) {
            return db::KeepGoing::Stop;
        }

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
            is_running.store(false, std::memory_order_release);
            return db::KeepGoing::Stop;
        }
        catch (...) {
            // Catch all exceptions including C# ones
            if (error_callback) {
                error_callback("Unknown exception in record callback", -998, user_data);
            }
            // Stop processing on exception
            is_running.store(false, std::memory_order_release);
            return db::KeepGoing::Stop;
        }

        return is_running.load(std::memory_order_acquire) ? db::KeepGoing::Continue : db::KeepGoing::Stop;
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
        return reinterpret_cast<DbentoLiveClientHandle>(
            databento_native::CreateValidatedHandle(databento_native::HandleType::LiveClient, wrapper));
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
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, &validation_error);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size,
                databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        // Validate parameters
        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);
        ValidateSymbolArray(symbols, symbol_count);

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

        // Parse schema from string to enum (centralized function, throws on error)
        db::Schema schema_enum = ParseSchema(schema);

        // Ensure client is created (thread-safe)
        wrapper->EnsureClientCreated();

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
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        // HIGH FIX: Validate callback function pointers
        // While we cannot fully validate function pointer integrity beyond null checking,
        // we ensure defensive programming practices:
        // 1. Null pointer check (prevents immediate crash)
        // 2. All callback invocations wrapped in try-catch (see OnRecord method)
        // 3. Document requirements for C# layer to maintain callback lifetime
        if (!on_record) {
            SafeStrCopy(error_buffer, error_buffer_size, "Record callback cannot be null");
            return -2;
        }

        // Store callbacks and user data
        // IMPORTANT: C# layer must ensure these function pointers remain valid
        // for the entire lifetime of the live client (no GC, no delegate disposal)
        wrapper->record_callback = on_record;
        wrapper->error_callback = on_error;  // May be null (optional)
        wrapper->user_data = user_data;
        wrapper->is_running.store(true, std::memory_order_release);

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
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, nullptr);
        if (wrapper) {
            // Atomic store for thread-safe stop
            wrapper->is_running.store(false, std::memory_order_release);
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
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, nullptr);
        if (wrapper) {
            // HIGH FIX: Phase 1 - Signal shutdown
            wrapper->is_running.store(false, std::memory_order_release);

            // HIGH FIX: Phase 2 - Brief delay to allow in-flight callbacks to observe stop signal
            // This prevents race where callbacks check is_running after we've deleted the wrapper
            std::this_thread::sleep_for(std::chrono::milliseconds(50));

            // HIGH FIX: Phase 3 - Acquire lock to ensure no callbacks are executing
            {
                std::lock_guard<std::mutex> lock(wrapper->callback_mutex);
                // Any callbacks that were in-flight are now complete
                // Any new callback attempts will be blocked here
            }

            // HIGH FIX: Phase 4 - Safe to delete wrapper now (no callbacks can access it)
            delete wrapper;

            // Destroy the validated handle
            databento_native::DestroyValidatedHandle(handle);
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

        // Create client immediately if we have a dataset (thread-safe)
        if (!ds.empty()) {
            wrapper->EnsureClientCreated();
        }

        return reinterpret_cast<DbentoLiveClientHandle>(
            databento_native::CreateValidatedHandle(databento_native::HandleType::LiveClient, wrapper));
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
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, &validation_error);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size,
                databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        if (!wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Client not initialized");
            return -2;
        }

        // Use databento-cpp's Reconnect method
        wrapper->is_running.store(false, std::memory_order_release);  // Stop current session
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
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, &validation_error);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size,
                databento_native::GetValidationErrorMessage(validation_error));
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
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
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
        wrapper->is_running.store(true, std::memory_order_release);

        // Start the client with metadata and record callbacks
        if (on_metadata) {
            wrapper->client->Start(
                [wrapper](db::Metadata&& metadata) {
                    try {
                        if (wrapper->metadata_callback) {
                            // Manually serialize metadata to JSON string for C# consumption
                            std::stringstream json;
                            json << "{";
                            json << "\"version\":" << static_cast<int>(metadata.version) << ",";
                            json << "\"dataset\":\"" << metadata.dataset << "\",";

                            // schema (nullable)
                            if (metadata.schema.has_value()) {
                                json << "\"schema\":" << static_cast<int>(metadata.schema.value()) << ",";
                            } else {
                                json << "\"schema\":null,";
                            }

                            json << "\"start\":" << metadata.start.time_since_epoch().count() << ",";
                            json << "\"end\":" << metadata.end.time_since_epoch().count() << ",";
                            json << "\"limit\":" << metadata.limit << ",";

                            // stype_in (nullable)
                            if (metadata.stype_in.has_value()) {
                                json << "\"stype_in\":" << static_cast<int>(metadata.stype_in.value()) << ",";
                            } else {
                                json << "\"stype_in\":null,";
                            }

                            json << "\"stype_out\":" << static_cast<int>(metadata.stype_out) << ",";
                            json << "\"ts_out\":" << (metadata.ts_out ? "true" : "false") << ",";
                            json << "\"symbol_cstr_len\":" << metadata.symbol_cstr_len << ",";

                            // symbols array
                            json << "\"symbols\":[";
                            for (size_t i = 0; i < metadata.symbols.size(); ++i) {
                                if (i > 0) json << ",";
                                json << "\"" << metadata.symbols[i] << "\"";
                            }
                            json << "],";

                            // partial array
                            json << "\"partial\":[";
                            for (size_t i = 0; i < metadata.partial.size(); ++i) {
                                if (i > 0) json << ",";
                                json << "\"" << metadata.partial[i] << "\"";
                            }
                            json << "],";

                            // not_found array
                            json << "\"not_found\":[";
                            for (size_t i = 0; i < metadata.not_found.size(); ++i) {
                                if (i > 0) json << ",";
                                json << "\"" << metadata.not_found[i] << "\"";
                            }
                            json << "],";

                            // mappings array (empty for now as it's complex)
                            json << "\"mappings\":[]";

                            json << "}";

                            std::string json_str = json.str();
                            wrapper->metadata_callback(json_str.c_str(), json_str.size(), wrapper->user_data);
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
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, &validation_error);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size,
                databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        // Validate parameters
        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);
        ValidateSymbolArray(symbols, symbol_count);

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

        // Parse schema from string to enum (centralized function, throws on error)
        db::Schema schema_enum = ParseSchema(schema);

        // Ensure client is created (thread-safe)
        wrapper->EnsureClientCreated();

        // Subscribe with snapshot
        wrapper->client->SubscribeWithSnapshot(symbol_vec, schema_enum, db::SType::RawSymbol);

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_subscribe_with_replay(
    DbentoLiveClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, &validation_error);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size,
                databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        // Validate parameters
        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);
        ValidateSymbolArray(symbols, symbol_count);

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

        // Parse schema from string to enum (centralized function, throws on error)
        db::Schema schema_enum = ParseSchema(schema);

        // Ensure client is created (thread-safe)
        wrapper->EnsureClientCreated();

        // Subscribe with intraday replay from start time
        // Convert int64_t nanoseconds to UnixNanos
        // UnixNanos is a time_point<system_clock, duration<uint64_t, nano>>
        db::UnixNanos start_time{std::chrono::nanoseconds{start_time_ns}};
        wrapper->client->Subscribe(symbol_vec, schema_enum, db::SType::RawSymbol, start_time);

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
        auto* wrapper = databento_native::ValidateAndCast<LiveClientWrapper>(
            handle, databento_native::HandleType::LiveClient, nullptr);
        if (!wrapper) {
            return 0;  // Disconnected
        }

        if (!wrapper->client) {
            return 0;  // Disconnected
        }

        // Check if running (atomic load)
        if (wrapper->is_running.load(std::memory_order_acquire)) {
            return 3;  // Streaming
        }

        // Client exists but not running
        return 2;  // Connected but not streaming
    }
    catch (...) {
        return 0;  // Disconnected on error
    }
}
