#include "databento_native.h"
#include "common_helpers.hpp"
#include <databento/dbn_encoder.hpp>
#include <databento/file_stream.hpp>
#include <databento/dbn.hpp>
#include <databento/enums.hpp>
#include <databento/datetime.hpp>
#include <databento/record.hpp>
#include <nlohmann/json.hpp>
#include <memory>
#include <string>
#include <cstring>
#include <filesystem>
#include <sstream>
#include <date/date.h>

namespace db = databento;
using json = nlohmann::json;
using databento_native::SafeStrCopy;

// ============================================================================
// DBN File Writer Wrapper Structure
// ============================================================================

struct DbnFileWriterWrapper {
    std::unique_ptr<db::OutFileStream> file_stream;
    std::unique_ptr<db::DbnEncoder> encoder;
    std::filesystem::path file_path;

    DbnFileWriterWrapper(const std::filesystem::path& path,
                         std::unique_ptr<db::OutFileStream> stream,
                         std::unique_ptr<db::DbnEncoder> enc)
        : file_stream(std::move(stream))
        , encoder(std::move(enc))
        , file_path(path) {
    }
};

// ============================================================================
// Helper Functions
// ============================================================================

// Parse JSON metadata and construct db::Metadata
static db::Metadata ParseMetadataFromJson(const std::string& json_str) {
    json j = json::parse(json_str);

    db::Metadata metadata;
    metadata.version = j["version"].get<std::uint8_t>();
    metadata.dataset = j["dataset"].get<std::string>();

    // Parse optional schema
    if (!j["schema"].is_null()) {
        metadata.schema = static_cast<db::Schema>(j["schema"].get<int>());
    }

    // Parse start/end timestamps (nanoseconds)
    auto start_ns = j["start"].get<int64_t>();
    auto end_ns = j["end"].get<int64_t>();
    metadata.start = db::UnixNanos{std::chrono::duration<uint64_t, std::nano>{static_cast<uint64_t>(start_ns)}};
    metadata.end = db::UnixNanos{std::chrono::duration<uint64_t, std::nano>{static_cast<uint64_t>(end_ns)}};

    metadata.limit = j["limit"].get<std::uint64_t>();

    // Parse optional stype_in
    if (!j["stype_in"].is_null()) {
        metadata.stype_in = static_cast<db::SType>(j["stype_in"].get<int>());
    }

    metadata.stype_out = static_cast<db::SType>(j["stype_out"].get<int>());
    metadata.ts_out = j["ts_out"].get<bool>();
    metadata.symbol_cstr_len = j["symbol_cstr_len"].get<std::size_t>();

    // Parse symbol arrays
    metadata.symbols = j["symbols"].get<std::vector<std::string>>();
    metadata.partial = j["partial"].get<std::vector<std::string>>();
    metadata.not_found = j["not_found"].get<std::vector<std::string>>();

    // Parse mappings
    if (j.contains("mappings") && j["mappings"].is_array()) {
        for (const auto& mapping_json : j["mappings"]) {
            db::SymbolMapping mapping;
            mapping.raw_symbol = mapping_json["raw_symbol"].get<std::string>();

            for (const auto& interval_json : mapping_json["intervals"]) {
                db::MappingInterval interval;

                // Parse ISO 8601 date strings
                std::string start_date_str = interval_json["start_date"].get<std::string>();
                std::string end_date_str = interval_json["end_date"].get<std::string>();

                std::istringstream ss_start(start_date_str);
                std::istringstream ss_end(end_date_str);

                ss_start >> date::parse("%Y-%m-%d", interval.start_date);
                ss_end >> date::parse("%Y-%m-%d", interval.end_date);

                interval.symbol = interval_json["symbol"].get<std::string>();
                mapping.intervals.push_back(interval);
            }

            metadata.mappings.push_back(mapping);
        }
    }

    return metadata;
}

// ============================================================================
// DBN File Writer API Implementation
// ============================================================================

DATABENTO_API DbnFileWriterHandle dbento_dbn_file_create(
    const char* file_path,
    const char* metadata_json,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        if (!file_path || !metadata_json) {
            SafeStrCopy(error_buffer, error_buffer_size, "File path and metadata cannot be null");
            return nullptr;
        }

        // Parse metadata from JSON
        db::Metadata metadata = ParseMetadataFromJson(metadata_json);

        // Create file stream
        std::filesystem::path path{file_path};
        auto file_stream = std::make_unique<db::OutFileStream>(path);

        // Create encoder (it will write the metadata header automatically)
        auto encoder = std::make_unique<db::DbnEncoder>(metadata, file_stream.get());

        // Create wrapper
        auto* wrapper = new DbnFileWriterWrapper(path, std::move(file_stream), std::move(encoder));
        return reinterpret_cast<DbnFileWriterHandle>(wrapper);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API int dbento_dbn_file_write_record(
    DbnFileWriterHandle handle,
    const uint8_t* record_bytes,
    size_t record_length,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<DbnFileWriterWrapper*>(handle);
        if (!wrapper || !wrapper->encoder) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid file writer handle");
            return -1;
        }

        if (!record_bytes || record_length == 0) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid record data");
            return -1;
        }

        // Create Record from raw bytes (Record constructor requires mutable pointer)
        // Copy data to mutable buffer to avoid const_cast undefined behavior
        std::vector<uint8_t> mutable_copy(record_bytes, record_bytes + record_length);
        const db::Record record{reinterpret_cast<db::RecordHeader*>(mutable_copy.data())};

        // SAFETY: EncodeRecord processes the record synchronously and does not store
        // the pointer. The mutable_copy vector remains alive until after EncodeRecord
        // returns, ensuring no use-after-free. This is safe by design of databento-cpp.
        wrapper->encoder->EncodeRecord(record);

        return 0; // Success
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API void dbento_dbn_file_close_writer(DbnFileWriterHandle handle)
{
    if (handle) {
        auto* wrapper = reinterpret_cast<DbnFileWriterWrapper*>(handle);
        delete wrapper;
        // Destructor will automatically flush and close the file stream
    }
}
