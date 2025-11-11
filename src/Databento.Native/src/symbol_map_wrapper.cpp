#include "databento_native.h"
#include <databento/symbol_map.hpp>
#include <databento/dbn.hpp>
#include <databento/record.hpp>
#include <date/date.h>
#include <memory>
#include <string>
#include <cstring>

namespace db = databento;

// ============================================================================
// Internal Wrapper Structures
// ============================================================================

struct TsSymbolMapWrapper {
    std::unique_ptr<db::TsSymbolMap> map;

    explicit TsSymbolMapWrapper(std::unique_ptr<db::TsSymbolMap>&& m)
        : map(std::move(m)) {}
};

struct PitSymbolMapWrapper {
    std::unique_ptr<db::PitSymbolMap> map;

    explicit PitSymbolMapWrapper(std::unique_ptr<db::PitSymbolMap>&& m)
        : map(std::move(m)) {}
};

struct MetadataWrapper {
    db::Metadata metadata;

    explicit MetadataWrapper(db::Metadata&& meta)
        : metadata(std::move(meta)) {}
};

// ============================================================================
// Helper Functions
// ============================================================================

static void SafeStrCopy(char* dest, size_t dest_size, const char* src) {
    if (dest && dest_size > 0 && src) {
        strncpy(dest, src, dest_size - 1);
        dest[dest_size - 1] = '\0';
    }
}

// ============================================================================
// TsSymbolMap API Implementation
// ============================================================================

DATABENTO_API DbentoTsSymbolMapHandle dbento_metadata_create_symbol_map(
    DbentoMetadataHandle metadata_handle,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* metadata_wrapper = reinterpret_cast<MetadataWrapper*>(metadata_handle);
        if (!metadata_wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid metadata handle");
            return nullptr;
        }

        auto symbol_map = std::make_unique<db::TsSymbolMap>(metadata_wrapper->metadata);
        auto* wrapper = new TsSymbolMapWrapper(std::move(symbol_map));
        return reinterpret_cast<DbentoTsSymbolMapHandle>(wrapper);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API int dbento_ts_symbol_map_is_empty(DbentoTsSymbolMapHandle handle)
{
    try {
        auto* wrapper = reinterpret_cast<TsSymbolMapWrapper*>(handle);
        if (!wrapper || !wrapper->map) {
            return -1;
        }
        return wrapper->map->IsEmpty() ? 1 : 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API size_t dbento_ts_symbol_map_size(DbentoTsSymbolMapHandle handle)
{
    try {
        auto* wrapper = reinterpret_cast<TsSymbolMapWrapper*>(handle);
        if (!wrapper || !wrapper->map) {
            return 0;
        }
        return wrapper->map->Size();
    }
    catch (...) {
        return 0;
    }
}

DATABENTO_API int dbento_ts_symbol_map_find(
    DbentoTsSymbolMapHandle handle,
    int year,
    unsigned int month,
    unsigned int day,
    uint32_t instrument_id,
    char* symbol_buffer,
    size_t symbol_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<TsSymbolMapWrapper*>(handle);
        if (!wrapper || !wrapper->map) {
            return -1;
        }

        // Convert year/month/day to date::year_month_day
        date::year_month_day ymd{
            date::year{year} / date::month{month} / date::day{day}
        };

        // Find in map
        auto it = wrapper->map->Find(ymd, instrument_id);
        if (it == wrapper->map->Map().end()) {
            return -2; // Not found
        }

        // Copy symbol to buffer
        SafeStrCopy(symbol_buffer, symbol_buffer_size, it->second->c_str());
        return 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API void dbento_ts_symbol_map_destroy(DbentoTsSymbolMapHandle handle)
{
    try {
        auto* wrapper = reinterpret_cast<TsSymbolMapWrapper*>(handle);
        delete wrapper;
    }
    catch (...) {
        // Swallow exceptions in cleanup
    }
}

// ============================================================================
// PitSymbolMap API Implementation
// ============================================================================

DATABENTO_API DbentoPitSymbolMapHandle dbento_metadata_create_symbol_map_for_date(
    DbentoMetadataHandle metadata_handle,
    int year,
    unsigned int month,
    unsigned int day,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        auto* metadata_wrapper = reinterpret_cast<MetadataWrapper*>(metadata_handle);
        if (!metadata_wrapper) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid metadata handle");
            return nullptr;
        }

        // Convert year/month/day to date::year_month_day
        date::year_month_day ymd{
            date::year{year} / date::month{month} / date::day{day}
        };

        auto symbol_map = std::make_unique<db::PitSymbolMap>(metadata_wrapper->metadata, ymd);
        auto* wrapper = new PitSymbolMapWrapper(std::move(symbol_map));
        return reinterpret_cast<DbentoPitSymbolMapHandle>(wrapper);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API int dbento_pit_symbol_map_is_empty(DbentoPitSymbolMapHandle handle)
{
    try {
        auto* wrapper = reinterpret_cast<PitSymbolMapWrapper*>(handle);
        if (!wrapper || !wrapper->map) {
            return -1;
        }
        return wrapper->map->IsEmpty() ? 1 : 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API size_t dbento_pit_symbol_map_size(DbentoPitSymbolMapHandle handle)
{
    try {
        auto* wrapper = reinterpret_cast<PitSymbolMapWrapper*>(handle);
        if (!wrapper || !wrapper->map) {
            return 0;
        }
        return wrapper->map->Size();
    }
    catch (...) {
        return 0;
    }
}

DATABENTO_API int dbento_pit_symbol_map_find(
    DbentoPitSymbolMapHandle handle,
    uint32_t instrument_id,
    char* symbol_buffer,
    size_t symbol_buffer_size)
{
    try {
        auto* wrapper = reinterpret_cast<PitSymbolMapWrapper*>(handle);
        if (!wrapper || !wrapper->map) {
            return -1;
        }

        // Find in map
        auto it = wrapper->map->Find(instrument_id);
        if (it == wrapper->map->Map().end()) {
            return -2; // Not found
        }

        // Copy symbol to buffer
        SafeStrCopy(symbol_buffer, symbol_buffer_size, it->second.c_str());
        return 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API int dbento_pit_symbol_map_on_record(
    DbentoPitSymbolMapHandle handle,
    const uint8_t* record_bytes,
    size_t record_length)
{
    try {
        auto* wrapper = reinterpret_cast<PitSymbolMapWrapper*>(handle);
        if (!wrapper || !wrapper->map || !record_bytes) {
            return -1;
        }

        // Create Record from bytes (Record constructor takes non-const pointer)
        // Cast away const since we're just reading the data
        db::Record record(const_cast<db::RecordHeader*>(
            reinterpret_cast<const db::RecordHeader*>(record_bytes)));
        wrapper->map->OnRecord(record);
        return 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API void dbento_pit_symbol_map_destroy(DbentoPitSymbolMapHandle handle)
{
    try {
        auto* wrapper = reinterpret_cast<PitSymbolMapWrapper*>(handle);
        delete wrapper;
    }
    catch (...) {
        // Swallow exceptions in cleanup
    }
}
