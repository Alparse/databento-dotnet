#include "databento_native.h"
#include "common_helpers.hpp"
#include <databento/historical.hpp>
#include <databento/batch.hpp>
#include <databento/enums.hpp>
#include <databento/datetime.hpp>
#include <nlohmann/json.hpp>
#include <memory>
#include <string>
#include <vector>
#include <cstring>
#include <filesystem>

namespace db = databento;
using json = nlohmann::json;
using databento_native::SafeStrCopy;

// ============================================================================
// External Wrapper Structure (from historical_client_wrapper.cpp)
// ============================================================================

struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key) {
        client = std::make_unique<db::Historical>(nullptr, key, db::HistoricalGateway::Bo1);
    }
};

// ============================================================================
// Helper Functions
// ============================================================================

// Convert nanoseconds since epoch to UnixNanos
static db::UnixNanos NsToUnixNanos(int64_t ns) {
    return db::UnixNanos{std::chrono::duration<uint64_t, std::nano>{static_cast<uint64_t>(ns)}};
}

// Parse schema string to enum
static db::Schema ParseSchema(const std::string& schema_str) {
    if (schema_str == "mbo") return db::Schema::Mbo;
    else if (schema_str == "mbp-1") return db::Schema::Mbp1;
    else if (schema_str == "mbp-10") return db::Schema::Mbp10;
    else if (schema_str == "trades") return db::Schema::Trades;
    else if (schema_str == "ohlcv-1s") return db::Schema::Ohlcv1S;
    else if (schema_str == "ohlcv-1m") return db::Schema::Ohlcv1M;
    else if (schema_str == "ohlcv-1h") return db::Schema::Ohlcv1H;
    else if (schema_str == "ohlcv-1d") return db::Schema::Ohlcv1D;
    else if (schema_str == "ohlcv-eod") return db::Schema::OhlcvEod;
    else if (schema_str == "definition") return db::Schema::Definition;
    else if (schema_str == "statistics") return db::Schema::Statistics;
    else if (schema_str == "status") return db::Schema::Status;
    else if (schema_str == "imbalance") return db::Schema::Imbalance;
    else throw std::runtime_error("Unknown schema: " + schema_str);
}

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
    j["split_duration"] = static_cast<int>(job.split_duration);
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
    char* result = new char[str.size() + 1];
    std::strcpy(result, str.c_str());
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
        auto* wrapper = reinterpret_cast<HistoricalClientWrapper*>(handle);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return nullptr;
        }

        if (!dataset || !schema) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid parameters");
            return nullptr;
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

        // Parse schema
        db::Schema schema_enum = ParseSchema(schema);

        // Convert timestamps
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

DATABENTO_API const char* dbento_batch_list_jobs(
    DbentoHistoricalClientHandle handle,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<HistoricalClientWrapper*>(handle);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
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
        auto* wrapper = reinterpret_cast<HistoricalClientWrapper*>(handle);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return nullptr;
        }

        if (!job_id) {
            SafeStrCopy(error_buffer, error_buffer_size, "Job ID cannot be null");
            return nullptr;
        }

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
        auto* wrapper = reinterpret_cast<HistoricalClientWrapper*>(handle);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return nullptr;
        }

        if (!output_dir || !job_id) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid parameters");
            return nullptr;
        }

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
        auto* wrapper = reinterpret_cast<HistoricalClientWrapper*>(handle);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid client handle");
            return nullptr;
        }

        if (!output_dir || !job_id || !filename) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid parameters");
            return nullptr;
        }

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
