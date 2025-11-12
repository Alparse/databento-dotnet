#include "databento_native.h"
#include "common_helpers.hpp"
#include <databento/dbn_file_store.hpp>
#include <databento/enums.hpp>
#include <databento/datetime.hpp>
#include <nlohmann/json.hpp>
#include <memory>
#include <string>
#include <cstring>
#include <filesystem>

namespace db = databento;
using json = nlohmann::json;
using databento_native::SafeStrCopy;

// ============================================================================
// DBN File Reader Wrapper Structure
// ============================================================================

struct DbnFileReaderWrapper {
    std::unique_ptr<db::DbnFileStore> file_store;
    std::filesystem::path file_path;

    explicit DbnFileReaderWrapper(const std::filesystem::path& path)
        : file_path(path) {
        file_store = std::make_unique<db::DbnFileStore>(path);
    }
};

// ============================================================================
// Helper Functions
// ============================================================================

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

// Convert Metadata to JSON
static json MetadataToJson(const db::Metadata& metadata) {
    json j;
    j["version"] = metadata.version;
    j["dataset"] = metadata.dataset;

    if (metadata.schema.has_value()) {
        j["schema"] = static_cast<int>(metadata.schema.value());
    } else {
        j["schema"] = nullptr;
    }

    // Convert UnixNanos to int64 nanoseconds
    j["start"] = static_cast<int64_t>(metadata.start.time_since_epoch().count());
    j["end"] = static_cast<int64_t>(metadata.end.time_since_epoch().count());
    j["limit"] = metadata.limit;

    if (metadata.stype_in.has_value()) {
        j["stype_in"] = static_cast<int>(metadata.stype_in.value());
    } else {
        j["stype_in"] = nullptr;
    }

    j["stype_out"] = static_cast<int>(metadata.stype_out);
    j["ts_out"] = metadata.ts_out;
    j["symbol_cstr_len"] = metadata.symbol_cstr_len;
    j["symbols"] = metadata.symbols;
    j["partial"] = metadata.partial;
    j["not_found"] = metadata.not_found;

    // Convert mappings
    json mappings_array = json::array();
    for (const auto& mapping : metadata.mappings) {
        json mapping_obj;
        mapping_obj["raw_symbol"] = mapping.raw_symbol;

        json intervals_array = json::array();
        for (const auto& interval : mapping.intervals) {
            json interval_obj;
            // Convert date::year_month_day to string
            std::ostringstream oss_start, oss_end;
            oss_start << interval.start_date;
            oss_end << interval.end_date;
            interval_obj["start_date"] = oss_start.str();
            interval_obj["end_date"] = oss_end.str();
            interval_obj["symbol"] = interval.symbol;
            intervals_array.push_back(interval_obj);
        }

        mapping_obj["intervals"] = intervals_array;
        mappings_array.push_back(mapping_obj);
    }

    j["mappings"] = mappings_array;
    return j;
}

// ============================================================================
// DBN File Reader API Implementation
// ============================================================================

DATABENTO_API DbnFileReaderHandle dbento_dbn_file_open(
    const char* file_path,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        if (!file_path) {
            SafeStrCopy(error_buffer, error_buffer_size, "File path cannot be null");
            return nullptr;
        }

        // Check if file exists
        std::filesystem::path path{file_path};
        if (!std::filesystem::exists(path)) {
            SafeStrCopy(error_buffer, error_buffer_size, "File does not exist");
            return nullptr;
        }

        auto* wrapper = new DbnFileReaderWrapper(path);
        return reinterpret_cast<DbnFileReaderHandle>(wrapper);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_dbn_file_get_metadata(
    DbnFileReaderHandle handle,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<DbnFileReaderWrapper*>(handle);
        if (!wrapper || !wrapper->file_store) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid file reader handle");
            return nullptr;
        }

        const db::Metadata& metadata = wrapper->file_store->GetMetadata();
        json j = MetadataToJson(metadata);
        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API int dbento_dbn_file_next_record(
    DbnFileReaderHandle handle,
    uint8_t* record_buffer,
    size_t record_buffer_size,
    size_t* record_length,
    uint8_t* record_type,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<DbnFileReaderWrapper*>(handle);
        if (!wrapper || !wrapper->file_store) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid file reader handle");
            return -1;
        }

        const db::Record* record = wrapper->file_store->NextRecord();

        // nullptr indicates end of file
        if (!record) {
            *record_length = 0;
            return 1; // Return 1 to indicate EOF (not an error)
        }

        // Get record size and type
        size_t rec_size = record->Size();
        uint8_t rec_type = static_cast<uint8_t>(record->RType());

        if (rec_size > record_buffer_size) {
            SafeStrCopy(error_buffer, error_buffer_size, "Record buffer too small");
            return -1;
        }

        // Copy record data
        std::memcpy(record_buffer, record, rec_size);
        *record_length = rec_size;
        *record_type = rec_type;

        return 0; // Success
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API void dbento_dbn_file_close(DbnFileReaderHandle handle)
{
    if (handle) {
        auto* wrapper = reinterpret_cast<DbnFileReaderWrapper*>(handle);
        delete wrapper;
    }
}
