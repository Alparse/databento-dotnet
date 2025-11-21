#define NOMINMAX  // Prevent Windows min/max macros from interfering with std::numeric_limits
#include "databento_native.h"
#include "common_helpers.hpp"
#include "handle_validation.hpp"
#include <databento/live_blocking.hpp>
#include <databento/live.hpp>
#include <databento/record.hpp>
#include <databento/enums.hpp>
#include <memory>
#include <string>
#include <sstream>
#include <vector>
#include <cstring>
#include <exception>

namespace db = databento;
using databento_native::SafeStrCopy;
using databento_native::ParseSchema;
using databento_native::ValidateNonEmptyString;

// ============================================================================
// Internal Wrapper Class for LiveBlocking
// ============================================================================
struct LiveBlockingWrapper {
    std::unique_ptr<db::LiveBlocking> client;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;
    std::string dataset;
    std::string api_key;
    bool send_ts_out = false;
    db::VersionUpgradePolicy upgrade_policy = db::VersionUpgradePolicy::UpgradeToV3;
    int heartbeat_interval_secs = 30;

    explicit LiveBlockingWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()) {}

    explicit LiveBlockingWrapper(
        const std::string& key,
        const std::string& ds,
        bool ts_out,
        db::VersionUpgradePolicy policy,
        int heartbeat_secs)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()),
          dataset(ds),
          send_ts_out(ts_out),
          upgrade_policy(policy),
          heartbeat_interval_secs(heartbeat_secs)
    {}

    void EnsureClientCreated() {
        if (!client) {
            auto builder = db::LiveBlocking::Builder()
                .SetKey(api_key)
                .SetDataset(dataset)
                .SetSendTsOut(send_ts_out)
                .SetUpgradePolicy(upgrade_policy)
                .SetLogReceiver(log_receiver.get());

            if (heartbeat_interval_secs > 0) {
                builder.SetHeartbeatInterval(std::chrono::seconds(heartbeat_interval_secs));
            }

            client = std::make_unique<db::LiveBlocking>(builder.BuildBlocking());
        }
    }

    ~LiveBlockingWrapper() {
        if (client) {
            try {
                client->Stop();
            } catch (...) {
                // Ignore exceptions during cleanup
            }
        }
    }
};

// ============================================================================
// LiveBlocking API Functions
// ============================================================================

DATABENTO_API DbentoLiveClientHandle dbento_live_blocking_create_ex(
    const char* api_key,
    const char* dataset,
    int send_ts_out,
    int upgrade_policy,
    int heartbeat_interval_secs,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        ValidateNonEmptyString("api_key", api_key);
        ValidateNonEmptyString("dataset", dataset);

        auto* wrapper = new LiveBlockingWrapper(
            api_key,
            dataset,
            send_ts_out != 0,
            static_cast<db::VersionUpgradePolicy>(upgrade_policy),
            heartbeat_interval_secs);

        // Mark as LiveBlocking type
        return databento_native::CreateValidatedHandle(databento_native::HandleType::LiveBlocking, wrapper);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API int dbento_live_blocking_subscribe(
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
        auto* wrapper = databento_native::ValidateAndCast<LiveBlockingWrapper>(
            handle, databento_native::HandleType::LiveBlocking, &validation_error);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size,
                databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);

        if (!symbols || symbol_count == 0) {
            SafeStrCopy(error_buffer, error_buffer_size, "Symbols array cannot be null or empty");
            return -2;
        }

        // Ensure client is created before subscribing
        wrapper->EnsureClientCreated();

        // Convert symbol array
        std::vector<std::string> symbol_vec;
        symbol_vec.reserve(symbol_count);
        for (size_t i = 0; i < symbol_count; ++i) {
            if (!symbols[i]) {
                SafeStrCopy(error_buffer, error_buffer_size, "Symbol cannot be null");
                return -3;
            }
            symbol_vec.emplace_back(symbols[i]);
        }

        // Parse schema
        auto parsed_schema = ParseSchema(schema);

        // Subscribe - default to RawSymbol stype
        wrapper->client->Subscribe(symbol_vec, parsed_schema, db::SType::RawSymbol);

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_blocking_subscribe_with_replay(
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
        auto* wrapper = databento_native::ValidateAndCast<LiveBlockingWrapper>(
            handle, databento_native::HandleType::LiveBlocking, &validation_error);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size,
                databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);

        if (!symbols || symbol_count == 0) {
            SafeStrCopy(error_buffer, error_buffer_size, "Symbols array cannot be null or empty");
            return -2;
        }

        wrapper->EnsureClientCreated();

        std::vector<std::string> symbol_vec;
        symbol_vec.reserve(symbol_count);
        for (size_t i = 0; i < symbol_count; ++i) {
            if (!symbols[i]) {
                SafeStrCopy(error_buffer, error_buffer_size, "Symbol cannot be null");
                return -3;
            }
            symbol_vec.emplace_back(symbols[i]);
        }

        auto parsed_schema = ParseSchema(schema);

        // Subscribe with replay start time
        db::UnixNanos start_time{std::chrono::nanoseconds(start_time_ns)};
        wrapper->client->Subscribe(symbol_vec, parsed_schema, db::SType::RawSymbol, start_time);

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_blocking_subscribe_with_snapshot(
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
        auto* wrapper = databento_native::ValidateAndCast<LiveBlockingWrapper>(
            handle, databento_native::HandleType::LiveBlocking, &validation_error);
        if (!wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size,
                databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);

        if (!symbols || symbol_count == 0) {
            SafeStrCopy(error_buffer, error_buffer_size, "Symbols array cannot be null or empty");
            return -2;
        }

        wrapper->EnsureClientCreated();

        std::vector<std::string> symbol_vec;
        symbol_vec.reserve(symbol_count);
        for (size_t i = 0; i < symbol_count; ++i) {
            if (!symbols[i]) {
                SafeStrCopy(error_buffer, error_buffer_size, "Symbol cannot be null");
                return -3;
            }
            symbol_vec.emplace_back(symbols[i]);
        }

        auto parsed_schema = ParseSchema(schema);

        wrapper->client->SubscribeWithSnapshot(symbol_vec, parsed_schema, db::SType::RawSymbol);

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

// Helper function to serialize Metadata to JSON
std::string SerializeMetadataToJson(const db::Metadata& metadata) {
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

    return json.str();
}

DATABENTO_API int dbento_live_blocking_start(
    DbentoLiveClientHandle handle,
    char* metadata_buffer,
    size_t metadata_buffer_size,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveBlockingWrapper>(
            handle, databento_native::HandleType::LiveBlocking, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        if (!metadata_buffer) {
            SafeStrCopy(error_buffer, error_buffer_size, "Metadata buffer cannot be null");
            return -2;
        }

        // This blocks until metadata is received!
        db::Metadata metadata = wrapper->client->Start();

        // Serialize metadata to JSON
        std::string json_metadata = SerializeMetadataToJson(metadata);

        if (json_metadata.size() >= metadata_buffer_size) {
            SafeStrCopy(error_buffer, error_buffer_size, "Metadata buffer too small");
            return -3;
        }

        SafeStrCopy(metadata_buffer, metadata_buffer_size, json_metadata.c_str());

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_blocking_next_record(
    DbentoLiveClientHandle handle,
    uint8_t* record_buffer,
    size_t record_buffer_size,
    size_t* out_record_length,
    uint8_t* out_record_type,
    int timeout_ms,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveBlockingWrapper>(
            handle, databento_native::HandleType::LiveBlocking, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        if (!record_buffer || !out_record_length || !out_record_type) {
            SafeStrCopy(error_buffer, error_buffer_size, "Output parameters cannot be null");
            return -2;
        }

        const db::Record* record;

        if (timeout_ms < 0) {
            // No timeout - blocking call
            record = &wrapper->client->NextRecord();
        } else {
            // With timeout
            record = wrapper->client->NextRecord(std::chrono::milliseconds(timeout_ms));
            if (!record) {
                // Timeout reached
                return 1;  // Special return code for timeout
            }
        }

        // Get record header and size
        const auto& header = record->Header();
        size_t record_size = record->Size();

        if (record_size > record_buffer_size) {
            SafeStrCopy(error_buffer, error_buffer_size, "Record buffer too small");
            return -3;
        }

        // Copy record data
        std::memcpy(record_buffer, &header, record_size);

        *out_record_length = record_size;
        *out_record_type = static_cast<uint8_t>(record->RType());

        return 0;  // Success
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_blocking_reconnect(
    DbentoLiveClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveBlockingWrapper>(
            handle, databento_native::HandleType::LiveBlocking, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        wrapper->client->Reconnect();
        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API int dbento_live_blocking_resubscribe(
    DbentoLiveClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveBlockingWrapper>(
            handle, databento_native::HandleType::LiveBlocking, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        wrapper->client->Resubscribe();
        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API void dbento_live_blocking_stop(DbentoLiveClientHandle handle)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveBlockingWrapper>(
            handle, databento_native::HandleType::LiveBlocking, &validation_error);
        if (wrapper && wrapper->client) {
            wrapper->client->Stop();
        }
    }
    catch (...) {
        // Ignore exceptions in Stop
    }
}

DATABENTO_API void dbento_live_blocking_destroy(DbentoLiveClientHandle handle)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<LiveBlockingWrapper>(
            handle, databento_native::HandleType::LiveBlocking, &validation_error);
        if (wrapper) {
            delete wrapper;
        }
    }
    catch (...) {
        // Ignore exceptions during cleanup
    }
}
