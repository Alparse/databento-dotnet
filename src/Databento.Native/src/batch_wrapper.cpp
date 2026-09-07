#include "databento_native.h"
#include "common_helpers.hpp"
#include "handle_validation.hpp"
#include <databento/historical.hpp>
#include <databento/batch.hpp>
#include <databento/enums.hpp>
#include <databento/datetime.hpp>
#include <nlohmann/json.hpp>
#include <memory>
#include <stdexcept>
#include <string>
#include <vector>
#include <cstring>
#include <filesystem>

namespace db = databento;
using json = nlohmann::json;
using databento_native::SafeStrCopy;
using databento_native::ParseSchema;
using databento_native::NsToUnixNanos;
using databento_native::ValidateNonEmptyString;
using databento_native::ValidateSymbolArray;
using databento_native::ValidateTimeRange;

// ============================================================================
// External Wrapper Structure (from historical_client_wrapper.cpp)
// ============================================================================

struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key) {
        client = std::make_unique<db::Historical>(
            nullptr,
            key,
            db::HistoricalGateway::Bo1
        );
    }
};

// ============================================================================
// Helper Functions (now in common_helpers.hpp)
// ============================================================================

// Wire contract for split_duration across the C ABI (Issue #37).
// These values are what Databento.Client.Models.SplitDuration uses and are
// documented on dbento_batch_submit_job_ex in databento_native.h. They are
// mapped explicitly rather than static_cast because databento-cpp's
// SplitDuration enum has a different order (Day, Week, Month, Year, None)
// and has been re-ordered between releases.
namespace {

constexpr int32_t kSplitDurationNone = 0;
constexpr int32_t kSplitDurationDay = 1;
constexpr int32_t kSplitDurationWeek = 2;
constexpr int32_t kSplitDurationMonth = 3;
constexpr int32_t kSplitDurationYear = 4;

db::SplitDuration SplitDurationFromWire(int32_t value) {
    switch (value) {
        case kSplitDurationNone: return db::SplitDuration::None;
        case kSplitDurationDay: return db::SplitDuration::Day;
        case kSplitDurationWeek: return db::SplitDuration::Week;
        case kSplitDurationMonth: return db::SplitDuration::Month;
        case kSplitDurationYear: return db::SplitDuration::Year;
        default:
            throw std::invalid_argument(
                "Invalid split_duration value " + std::to_string(value) +
                " (expected 0=None, 1=Day, 2=Week, 3=Month, 4=Year)");
    }
}

int32_t SplitDurationToWire(db::SplitDuration value) {
    switch (value) {
        case db::SplitDuration::None: return kSplitDurationNone;
        case db::SplitDuration::Day: return kSplitDurationDay;
        case db::SplitDuration::Week: return kSplitDurationWeek;
        case db::SplitDuration::Month: return kSplitDurationMonth;
        case db::SplitDuration::Year: return kSplitDurationYear;
        default: return kSplitDurationNone;
    }
}

// databento-cpp only models the values the batch API accepts. Anything else
// would be serialized as "Unknown" and fail server-side with an unhelpful
// message, so reject it here with a clear one.
db::Encoding EncodingFromWire(int32_t value) {
    switch (value) {
        case 0: return db::Encoding::Dbn;
        case 1: return db::Encoding::Csv;
        case 2: return db::Encoding::Json;
        default:
            throw std::invalid_argument(
                "Invalid encoding value " + std::to_string(value) +
                " (expected 0=Dbn, 1=Csv, 2=Json)");
    }
}

db::Compression CompressionFromWire(int32_t value) {
    switch (value) {
        case 0: return db::Compression::None;
        case 1: return db::Compression::Zstd;
        default:
            throw std::invalid_argument(
                "Unsupported compression value " + std::to_string(value) +
                " for batch jobs (expected 0=None, 1=Zstd)");
    }
}

db::Delivery DeliveryFromWire(int32_t value) {
    switch (value) {
        case 0: return db::Delivery::Download;
        default:
            throw std::invalid_argument(
                "Unsupported delivery value " + std::to_string(value) +
                " for batch jobs (expected 0=Download)");
    }
}

}  // namespace

// Convert BatchJob to JSON
static json BatchJobToJson(const db::BatchJob& job) {
    json j;
    j["id"] = job.id;
    j["user_id"] = job.user_id;
    j["cost_usd"] = job.cost_usd;
    j["dataset"] = job.dataset;
    j["symbols"] = job.symbols;
    j["stype_in"] = static_cast<int>(job.stype_in);
    j["stype_out"] = static_cast<int>(job.stype_out);
    j["schema"] = static_cast<int>(job.schema);
    j["start"] = job.start;
    j["end"] = job.end;
    j["limit"] = job.limit;
    j["encoding"] = static_cast<int>(job.encoding);
    j["compression"] = static_cast<int>(job.compression);
    j["pretty_px"] = job.pretty_px;
    j["pretty_ts"] = job.pretty_ts;
    j["map_symbols"] = job.map_symbols;
    j["split_duration"] = SplitDurationToWire(job.split_duration);
    j["split_size"] = job.split_size;
    j["split_symbols"] = job.split_symbols;
    j["delivery"] = static_cast<int>(job.delivery);
    j["record_count"] = job.record_count;
    j["billed_size"] = job.billed_size;
    j["actual_size"] = job.actual_size;
    j["package_size"] = job.package_size;
    j["state"] = static_cast<int>(job.state);
    j["ts_received"] = job.ts_received;
    j["ts_queued"] = job.ts_queued;
    j["ts_process_start"] = job.ts_process_start;
    j["ts_process_done"] = job.ts_process_done;
    j["ts_expiration"] = job.ts_expiration;
    return j;
}

// Convert BatchFileDesc to JSON
static json BatchFileDescToJson(const db::BatchFileDesc& file) {
    json j;
    j["filename"] = file.filename;
    j["size"] = file.size;
    j["hash"] = file.hash;
    j["https_url"] = file.https_url;
    j["ftp_url"] = file.ftp_url;
    return j;
}

// Allocate a string that can be freed with dbento_free_string
static char* AllocateString(const std::string& str) {
    // Validate size to prevent overflow
    if (str.size() > SIZE_MAX - 1) {
        return nullptr;  // String too large
    }

    char* result = new char[str.size() + 1];

    // Use memcpy instead of strcpy for safety
    std::memcpy(result, str.c_str(), str.size());
    result[str.size()] = '\0';

    return result;
}

// ============================================================================
// Batch API Implementation
// ============================================================================

DATABENTO_API const char* dbento_batch_submit_job(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    int64_t end_time_ns,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return nullptr;
        }

        // Comprehensive input validation
        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);
        ValidateSymbolArray(symbols, symbol_count);
        ValidateTimeRange(start_time_ns, end_time_ns);

        // Convert symbols to vector
        std::vector<std::string> symbol_vec;
        if (symbols && symbol_count > 0) {
            for (size_t i = 0; i < symbol_count; ++i) {
                if (symbols[i]) {
                    symbol_vec.emplace_back(symbols[i]);
                }
            }
        }

        // Parse schema (throws on invalid schema)
        db::Schema schema_enum = ParseSchema(schema);

        // Convert timestamps (throws on invalid range)
        auto start_unix = NsToUnixNanos(start_time_ns);
        auto end_unix = NsToUnixNanos(end_time_ns);
        db::DateTimeRange<db::UnixNanos> datetime_range{start_unix, end_unix};

        // Submit batch job with defaults
        db::BatchJob job = wrapper->client->BatchSubmitJob(
            dataset,
            symbol_vec,
            schema_enum,
            datetime_range);

        // Convert to JSON and return
        json j = BatchJobToJson(job);
        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_batch_submit_job_ex(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    const char** symbols,
    size_t symbol_count,
    int64_t start_time_ns,
    int64_t end_time_ns,
    int32_t encoding,
    int32_t compression,
    bool pretty_px,
    bool pretty_ts,
    bool map_symbols,
    bool split_symbols,
    int32_t split_duration,
    uint64_t split_size,
    int32_t delivery,
    int32_t stype_in,
    int32_t stype_out,
    uint64_t limit,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return nullptr;
        }

        // Comprehensive input validation
        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);
        ValidateSymbolArray(symbols, symbol_count);
        ValidateTimeRange(start_time_ns, end_time_ns);

        // Convert symbols to vector
        std::vector<std::string> symbol_vec;
        if (symbols && symbol_count > 0) {
            for (size_t i = 0; i < symbol_count; ++i) {
                if (symbols[i]) {
                    symbol_vec.emplace_back(symbols[i]);
                }
            }
        }

        // Parse schema (throws on invalid schema)
        db::Schema schema_enum = ParseSchema(schema);

        // Convert timestamps (throws on invalid range)
        auto start_unix = NsToUnixNanos(start_time_ns);
        auto end_unix = NsToUnixNanos(end_time_ns);
        db::DateTimeRange<db::UnixNanos> datetime_range{start_unix, end_unix};

        // Convert enums (explicit mapping, throws on values databento-cpp cannot represent)
        db::Encoding encoding_enum = EncodingFromWire(encoding);
        db::Compression compression_enum = CompressionFromWire(compression);
        db::SplitDuration split_duration_enum = SplitDurationFromWire(split_duration);
        db::Delivery delivery_enum = DeliveryFromWire(delivery);
        db::SType stype_in_enum = static_cast<db::SType>(stype_in);
        db::SType stype_out_enum = static_cast<db::SType>(stype_out);

        // Submit batch job with all parameters
        db::BatchJob job = wrapper->client->BatchSubmitJob(
            dataset,
            symbol_vec,
            schema_enum,
            datetime_range,
            encoding_enum,
            compression_enum,
            pretty_px,
            pretty_ts,
            map_symbols,
            split_symbols,
            split_duration_enum,
            split_size,
            delivery_enum,
            stype_in_enum,
            stype_out_enum,
            limit);

        // Convert to JSON and return
        json j = BatchJobToJson(job);
        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_batch_list_jobs(
    DbentoHistoricalClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return nullptr;
        }

        // List all batch jobs
        std::vector<db::BatchJob> jobs = wrapper->client->BatchListJobs();

        // Convert to JSON array
        json j = json::array();
        for (const auto& job : jobs) {
            j.push_back(BatchJobToJson(job));
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_batch_list_files(
    DbentoHistoricalClientHandle handle,
    const char* job_id,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return nullptr;
        }

        // Validate job ID
        ValidateNonEmptyString("job_id", job_id);

        // List files for job
        std::vector<db::BatchFileDesc> files = wrapper->client->BatchListFiles(job_id);

        // Convert to JSON array
        json j = json::array();
        for (const auto& file : files) {
            j.push_back(BatchFileDescToJson(file));
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_batch_download_all(
    DbentoHistoricalClientHandle handle,
    const char* output_dir,
    const char* job_id,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return nullptr;
        }

        // Validate parameters
        ValidateNonEmptyString("output_dir", output_dir);
        ValidateNonEmptyString("job_id", job_id);

        // Download all files
        std::vector<std::filesystem::path> downloaded_paths =
            wrapper->client->BatchDownload(std::filesystem::path{output_dir}, job_id);

        // Convert to JSON array of strings
        json j = json::array();
        for (const auto& path : downloaded_paths) {
            j.push_back(path.string());
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_batch_download_file(
    DbentoHistoricalClientHandle handle,
    const char* output_dir,
    const char* job_id,
    const char* filename,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return nullptr;
        }

        // Validate parameters
        ValidateNonEmptyString("output_dir", output_dir);
        ValidateNonEmptyString("job_id", job_id);
        ValidateNonEmptyString("filename", filename);

        // Download specific file
        std::filesystem::path downloaded_path =
            wrapper->client->BatchDownload(std::filesystem::path{output_dir}, job_id, filename);

        std::string path_str = downloaded_path.string();
        return AllocateString(path_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_batch_download_all_keep_zip(
    DbentoHistoricalClientHandle handle,
    const char* output_dir,
    const char* job_id,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return nullptr;
        }

        // Validate parameters
        ValidateNonEmptyString("output_dir", output_dir);
        ValidateNonEmptyString("job_id", job_id);

        // Get list of files for this job
        std::vector<db::BatchFileDesc> files = wrapper->client->BatchListFiles(job_id);

        // Find the .zip file
        std::string zip_filename;
        for (const auto& file : files) {
            if (file.filename.size() >= 4 &&
                file.filename.substr(file.filename.size() - 4) == ".zip") {
                zip_filename = file.filename;
                break;
            }
        }

        if (zip_filename.empty()) {
            SafeStrCopy(error_buffer, error_buffer_size,
                "No zip file found in batch job files. The job may not have been created with zip packaging.");
            return nullptr;
        }

        // Download just the zip file
        std::filesystem::path downloaded_path =
            wrapper->client->BatchDownload(std::filesystem::path{output_dir}, job_id, zip_filename);

        std::string path_str = downloaded_path.string();
        return AllocateString(path_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}
