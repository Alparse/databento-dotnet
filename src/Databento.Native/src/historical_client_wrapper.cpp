#include "databento_native.h"
#include "common_helpers.hpp"
#include "handle_validation.hpp"
#include <databento/historical.hpp>
#include <databento/record.hpp>
#include <databento/enums.hpp>
#include <databento/timeseries.hpp>
#include <databento/datetime.hpp>
#include <databento/symbology.hpp>
#include <nlohmann/json.hpp>
#include <memory>
#include <string>
#include <vector>
#include <cstring>
#include <chrono>
#include <sstream>

namespace db = databento;
using json = nlohmann::json;
using databento_native::SafeStrCopy;
using databento_native::ParseSchema;
using databento_native::NsToUnixNanos;
using databento_native::ValidateNonEmptyString;
using databento_native::ValidateSymbolArray;
using databento_native::ValidateTimeRange;

// ============================================================================
// Internal Wrapper Class
// ============================================================================
struct HistoricalClientWrapper {
    std::unique_ptr<db::Historical> client;
    std::string api_key;
    std::unique_ptr<databento_native::StderrLogReceiver> log_receiver;

    explicit HistoricalClientWrapper(const std::string& key)
        : api_key(key),
          log_receiver(std::make_unique<databento_native::StderrLogReceiver>()) {
        client = std::make_unique<db::Historical>(
            log_receiver.get(),
            key,
            db::HistoricalGateway::Bo1
        );
    }
};

struct MetadataWrapper {
    db::Metadata metadata;

    explicit MetadataWrapper(db::Metadata&& meta)
        : metadata(std::move(meta)) {}
};

// ============================================================================
// Helper Functions (now in common_helpers.hpp)
// ============================================================================

// ============================================================================
// C API Implementation
// ============================================================================

DATABENTO_API DbentoHistoricalClientHandle dbento_historical_create(
    const char* api_key,
    char* error_buffer,
    size_t error_buffer_size)
{
    try {
        if (!api_key) {
            SafeStrCopy(error_buffer, error_buffer_size, "API key cannot be null");
            return nullptr;
        }

        auto* wrapper = new HistoricalClientWrapper(api_key);
        return reinterpret_cast<DbentoHistoricalClientHandle>(
            databento_native::CreateValidatedHandle(databento_native::HandleType::HistoricalClient, wrapper));
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

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
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        if (!dataset || !schema || !on_record) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid parameters");
            return -2;
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

        // MEDIUM FIX: Use centralized schema parsing (eliminates code duplication)
        // ParseSchema now handles all schema types consistently and throws on unknown schema
        db::Schema schema_enum = ParseSchema(schema);

        // Convert timestamps
        auto start_unix = NsToUnixNanos(start_time_ns);
        auto end_unix = NsToUnixNanos(end_time_ns);
        db::DateTimeRange<db::UnixNanos> datetime_range{start_unix, end_unix};

        // Call timeseries API
        wrapper->client->TimeseriesGetRange(
            dataset,
            datetime_range,
            symbol_vec,
            schema_enum,
            [on_record, user_data](const db::Record& record) {
                // Get the actual RecordHeader pointer (not the Record wrapper)
                const auto& header = record.Header();
                const uint8_t* bytes = reinterpret_cast<const uint8_t*>(&header);
                size_t length = record.Size();
                uint8_t type = static_cast<uint8_t>(record.RType());

                on_record(bytes, length, type, user_data);
                return db::KeepGoing::Continue;
            }
        );

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

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
    size_t error_buffer_size)
{
    try {
        databento_native::ValidationError validation_error;
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, &validation_error);
        if (!wrapper || !wrapper->client) {
            SafeStrCopy(error_buffer, error_buffer_size,
                wrapper ? "Client not initialized" : databento_native::GetValidationErrorMessage(validation_error));
            return -1;
        }

        if (!file_path || !dataset || !schema) {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid parameters");
            return -2;
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

        // MEDIUM FIX: Use centralized schema parsing (eliminates code duplication)
        db::Schema schema_enum = ParseSchema(schema);

        // Convert timestamps
        auto start_unix = NsToUnixNanos(start_time_ns);
        auto end_unix = NsToUnixNanos(end_time_ns);
        db::DateTimeRange<db::UnixNanos> datetime_range{start_unix, end_unix};

        // Call TimeseriesGetRangeToFile
        wrapper->client->TimeseriesGetRangeToFile(
            dataset,
            datetime_range,
            symbol_vec,
            schema_enum,
            std::filesystem::path{file_path}
        );

        return 0;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return -1;
    }
}

DATABENTO_API DbentoMetadataHandle dbento_historical_get_metadata(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
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

        if (!dataset || !schema) {
            SafeStrCopy(error_buffer, error_buffer_size, "Dataset and schema cannot be null");
            return nullptr;
        }

        // Mark unused parameters - this is a stub function not yet fully implemented
        (void)start_time_ns;
        (void)end_time_ns;

        // MEDIUM FIX: Use centralized schema parsing (eliminates code duplication)
        // Note: schema_enum would be used when metadata-only query is implemented
        db::Schema schema_enum = ParseSchema(schema);
        (void)schema_enum;  // Mark as intentionally unused until implementation complete

        // Note: Getting metadata without full query is not directly supported
        // For now, return nullptr - this feature would need a different API approach
        SafeStrCopy(error_buffer, error_buffer_size, "Metadata-only query not implemented");
        return nullptr;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API void dbento_historical_destroy(DbentoHistoricalClientHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<HistoricalClientWrapper>(
            handle, databento_native::HandleType::HistoricalClient, nullptr);
        if (wrapper) {
            delete wrapper;
            databento_native::DestroyValidatedHandle(handle);
        }
    }
    catch (...) {
        // Swallow exceptions in cleanup
    }
}

// ============================================================================
// Metadata API
// ============================================================================

DATABENTO_API int dbento_metadata_get_symbol_mapping(
    DbentoMetadataHandle handle,
    uint32_t instrument_id,
    char* symbol_buffer,
    size_t symbol_buffer_size)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<MetadataWrapper>(
            handle, databento_native::HandleType::Metadata, nullptr);
        if (!wrapper) {
            return -1;
        }

        // Mark unused parameters - this is a stub function not yet fully implemented
        (void)instrument_id;
        (void)symbol_buffer;
        (void)symbol_buffer_size;

        // Get symbol mapping from metadata
        auto symbol_map = wrapper->metadata.CreateSymbolMap();

        // Look up the instrument ID - API may have different method name
        // For now, return not implemented
        return -2; // Not found/not implemented
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API void dbento_metadata_destroy(DbentoMetadataHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<MetadataWrapper>(
            handle, databento_native::HandleType::Metadata, nullptr);
        if (wrapper) {
            delete wrapper;
            databento_native::DestroyValidatedHandle(handle);
        }
    }
    catch (...) {
        // Swallow exceptions in cleanup
    }
}

// ============================================================================
// Symbology Resolution API
// ============================================================================

struct SymbologyResolutionWrapper {
    db::SymbologyResolution resolution;

    explicit SymbologyResolutionWrapper(db::SymbologyResolution&& res)
        : resolution(std::move(res)) {}
};

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

        if (!dataset || !stype_in || !stype_out || !start_date || !end_date) {
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

        // Parse SType from strings
        auto parse_stype = [](const std::string& stype_str) -> db::SType {
            if (stype_str == "instrument_id") return db::SType::InstrumentId;
            if (stype_str == "raw_symbol") return db::SType::RawSymbol;
            if (stype_str == "smart") return db::SType::Smart;
            if (stype_str == "continuous") return db::SType::Continuous;
            if (stype_str == "parent") return db::SType::Parent;
            if (stype_str == "nasdaq_symbol") return db::SType::NasdaqSymbol;
            if (stype_str == "cms_symbol") return db::SType::CmsSymbol;
            if (stype_str == "isin") return db::SType::Isin;
            if (stype_str == "us_code") return db::SType::UsCode;
            if (stype_str == "bbg_comp_id") return db::SType::BbgCompId;
            if (stype_str == "bbg_comp_ticker") return db::SType::BbgCompTicker;
            if (stype_str == "figi") return db::SType::Figi;
            if (stype_str == "figi_ticker") return db::SType::FigiTicker;
            throw std::invalid_argument("Unknown SType: " + stype_str);
        };

        db::SType stype_in_enum = parse_stype(stype_in);
        db::SType stype_out_enum = parse_stype(stype_out);

        // DateRange uses strings in YYYY-MM-DD format
        db::DateRange date_range{start_date, end_date};

        // Call SymbologyResolve
        auto resolution = wrapper->client->SymbologyResolve(
            dataset,
            symbol_vec,
            stype_in_enum,
            stype_out_enum,
            date_range
        );

        auto* res_wrapper = new SymbologyResolutionWrapper(std::move(resolution));
        return reinterpret_cast<DbentoSymbologyResolutionHandle>(
            databento_native::CreateValidatedHandle(databento_native::HandleType::SymbologyResolution, res_wrapper));
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API size_t dbento_symbology_resolution_mappings_count(
    DbentoSymbologyResolutionHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        return wrapper ? wrapper->resolution.mappings.size() : 0;
    }
    catch (...) {
        return 0;
    }
}

DATABENTO_API int dbento_symbology_resolution_get_mapping_key(
    DbentoSymbologyResolutionHandle handle,
    size_t index,
    char* key_buffer,
    size_t key_buffer_size)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        if (!wrapper || !key_buffer || key_buffer_size == 0) {
            return -1;
        }

        if (index >= wrapper->resolution.mappings.size()) {
            return -2; // Index out of bounds
        }

        // Iterate to the index-th element
        auto it = wrapper->resolution.mappings.begin();
        std::advance(it, index);

        SafeStrCopy(key_buffer, key_buffer_size, it->first.c_str());
        return 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API size_t dbento_symbology_resolution_get_intervals_count(
    DbentoSymbologyResolutionHandle handle,
    const char* symbol_key)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        if (!wrapper || !symbol_key) {
            return 0;
        }

        auto it = wrapper->resolution.mappings.find(symbol_key);
        if (it == wrapper->resolution.mappings.end()) {
            return 0;
        }

        return it->second.size();
    }
    catch (...) {
        return 0;
    }
}

DATABENTO_API int dbento_symbology_resolution_get_interval(
    DbentoSymbologyResolutionHandle handle,
    const char* symbol_key,
    size_t interval_index,
    char* start_date_buffer,
    size_t start_date_buffer_size,
    char* end_date_buffer,
    size_t end_date_buffer_size,
    char* symbol_buffer,
    size_t symbol_buffer_size)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        if (!wrapper || !symbol_key) {
            return -1;
        }

        auto it = wrapper->resolution.mappings.find(symbol_key);
        if (it == wrapper->resolution.mappings.end()) {
            return -2; // Key not found
        }

        if (interval_index >= it->second.size()) {
            return -3; // Index out of bounds
        }

        const auto& interval = it->second[interval_index];

        // Format dates as YYYY-MM-DD
        std::ostringstream start_ss, end_ss;
        start_ss << date::format("%Y-%m-%d", interval.start_date);
        end_ss << date::format("%Y-%m-%d", interval.end_date);

        SafeStrCopy(start_date_buffer, start_date_buffer_size, start_ss.str().c_str());
        SafeStrCopy(end_date_buffer, end_date_buffer_size, end_ss.str().c_str());
        SafeStrCopy(symbol_buffer, symbol_buffer_size, interval.symbol.c_str());

        return 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API size_t dbento_symbology_resolution_partial_count(
    DbentoSymbologyResolutionHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        return wrapper ? wrapper->resolution.partial.size() : 0;
    }
    catch (...) {
        return 0;
    }
}

DATABENTO_API int dbento_symbology_resolution_get_partial(
    DbentoSymbologyResolutionHandle handle,
    size_t index,
    char* symbol_buffer,
    size_t symbol_buffer_size)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        if (!wrapper || !symbol_buffer || symbol_buffer_size == 0) {
            return -1;
        }

        if (index >= wrapper->resolution.partial.size()) {
            return -2; // Index out of bounds
        }

        SafeStrCopy(symbol_buffer, symbol_buffer_size, wrapper->resolution.partial[index].c_str());
        return 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API size_t dbento_symbology_resolution_not_found_count(
    DbentoSymbologyResolutionHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        return wrapper ? wrapper->resolution.not_found.size() : 0;
    }
    catch (...) {
        return 0;
    }
}

DATABENTO_API int dbento_symbology_resolution_get_not_found(
    DbentoSymbologyResolutionHandle handle,
    size_t index,
    char* symbol_buffer,
    size_t symbol_buffer_size)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        if (!wrapper || !symbol_buffer || symbol_buffer_size == 0) {
            return -1;
        }

        if (index >= wrapper->resolution.not_found.size()) {
            return -2; // Index out of bounds
        }

        SafeStrCopy(symbol_buffer, symbol_buffer_size, wrapper->resolution.not_found[index].c_str());
        return 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API int dbento_symbology_resolution_get_stype_in(
    DbentoSymbologyResolutionHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        if (!wrapper) {
            return -1;
        }
        return static_cast<int>(wrapper->resolution.stype_in);
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API int dbento_symbology_resolution_get_stype_out(
    DbentoSymbologyResolutionHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        if (!wrapper) {
            return -1;
        }
        return static_cast<int>(wrapper->resolution.stype_out);
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API void dbento_symbology_resolution_destroy(
    DbentoSymbologyResolutionHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<SymbologyResolutionWrapper>(
            handle, databento_native::HandleType::SymbologyResolution, nullptr);
        if (wrapper) {
            delete wrapper;
            databento_native::DestroyValidatedHandle(handle);
        }
    }
    catch (...) {
        // Swallow exceptions in cleanup
    }
}

// ============================================================================
// Unit Prices API
// ============================================================================

struct UnitPricesWrapper {
    std::vector<db::UnitPricesForMode> prices;

    explicit UnitPricesWrapper(std::vector<db::UnitPricesForMode>&& p)
        : prices(std::move(p)) {}
};

DATABENTO_API DbentoUnitPricesHandle dbento_historical_list_unit_prices(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
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

        if (!dataset) {
            SafeStrCopy(error_buffer, error_buffer_size, "Dataset cannot be null");
            return nullptr;
        }

        auto prices = wrapper->client->MetadataListUnitPrices(dataset);
        auto* prices_wrapper = new UnitPricesWrapper(std::move(prices));
        return reinterpret_cast<DbentoUnitPricesHandle>(
            databento_native::CreateValidatedHandle(databento_native::HandleType::UnitPrices, prices_wrapper));
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API size_t dbento_unit_prices_get_modes_count(
    DbentoUnitPricesHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<UnitPricesWrapper>(
            handle, databento_native::HandleType::UnitPrices, nullptr);
        return wrapper ? wrapper->prices.size() : 0;
    }
    catch (...) {
        return 0;
    }
}

DATABENTO_API int dbento_unit_prices_get_mode(
    DbentoUnitPricesHandle handle,
    size_t mode_index)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<UnitPricesWrapper>(
            handle, databento_native::HandleType::UnitPrices, nullptr);
        if (!wrapper || mode_index >= wrapper->prices.size()) {
            return -1;
        }
        return static_cast<int>(wrapper->prices[mode_index].mode);
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API size_t dbento_unit_prices_get_schema_count(
    DbentoUnitPricesHandle handle,
    size_t mode_index)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<UnitPricesWrapper>(
            handle, databento_native::HandleType::UnitPrices, nullptr);
        if (!wrapper || mode_index >= wrapper->prices.size()) {
            return 0;
        }
        return wrapper->prices[mode_index].unit_prices.size();
    }
    catch (...) {
        return 0;
    }
}

DATABENTO_API int dbento_unit_prices_get_schema_price(
    DbentoUnitPricesHandle handle,
    size_t mode_index,
    size_t schema_index,
    int* out_schema,
    double* out_price)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<UnitPricesWrapper>(
            handle, databento_native::HandleType::UnitPrices, nullptr);
        if (!wrapper || mode_index >= wrapper->prices.size() || !out_schema || !out_price) {
            return -1;
        }

        const auto& prices_map = wrapper->prices[mode_index].unit_prices;
        if (schema_index >= prices_map.size()) {
            return -2; // Index out of bounds
        }

        // Iterate to the schema_index-th element
        auto it = prices_map.begin();
        std::advance(it, schema_index);

        *out_schema = static_cast<int>(it->first);
        *out_price = it->second;
        return 0;
    }
    catch (...) {
        return -1;
    }
}

DATABENTO_API void dbento_unit_prices_destroy(DbentoUnitPricesHandle handle)
{
    try {
        auto* wrapper = databento_native::ValidateAndCast<UnitPricesWrapper>(
            handle, databento_native::HandleType::UnitPrices, nullptr);
        if (wrapper) {
            delete wrapper;
            databento_native::DestroyValidatedHandle(handle);
        }
    }
    catch (...) {
        // Swallow exceptions in cleanup
    }
}

// ============================================================================
// Helper for allocating strings that can be freed with dbento_free_string
// ============================================================================

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
// Metadata Listing API
// ============================================================================

DATABENTO_API const char* dbento_metadata_list_datasets(
    DbentoHistoricalClientHandle handle,
    const char* venue,
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

        // Call databento-cpp method
        // Note: The databento-cpp MetadataListDatasets() method returns all datasets
        // The venue parameter is currently not supported by the underlying C++ API
        (void)venue;  // Suppress unused parameter warning
        std::vector<std::string> datasets = wrapper->client->MetadataListDatasets();

        // Convert to JSON array
        json j = json::array();
        for (const auto& dataset : datasets) {
            j.push_back(dataset);
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_metadata_list_publishers(
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

        // Call databento-cpp method
        auto publishers = wrapper->client->MetadataListPublishers();

        // Convert to JSON array - match C# PublisherDetail properties (PascalCase)
        json j = json::array();
        for (const auto& publisher : publishers) {
            json pub_obj;
            pub_obj["PublisherId"] = publisher.publisher_id;
            pub_obj["Venue"] = publisher.venue;
            pub_obj["Dataset"] = publisher.dataset;
            pub_obj["Description"] = publisher.description;
            j.push_back(pub_obj);
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_metadata_list_schemas(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
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

        if (!dataset || dataset[0] == '\0') {
            SafeStrCopy(error_buffer, error_buffer_size, "Dataset cannot be null or empty");
            return nullptr;
        }

        // Call databento-cpp method
        auto schemas = wrapper->client->MetadataListSchemas(dataset);

        // Convert to JSON array of schema enum values (as strings)
        json j = json::array();
        for (const auto& schema : schemas) {
            j.push_back(databento::ToString(schema));
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_metadata_list_fields(
    DbentoHistoricalClientHandle handle,
    const char* encoding,
    const char* schema,
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

        if (!encoding || encoding[0] == '\0') {
            SafeStrCopy(error_buffer, error_buffer_size, "Encoding cannot be null or empty");
            return nullptr;
        }

        if (!schema || schema[0] == '\0') {
            SafeStrCopy(error_buffer, error_buffer_size, "Schema cannot be null or empty");
            return nullptr;
        }

        // Parse encoding and schema
        db::Encoding enc;
        if (std::strcmp(encoding, "dbn") == 0) {
            enc = db::Encoding::Dbn;
        } else if (std::strcmp(encoding, "csv") == 0) {
            enc = db::Encoding::Csv;
        } else if (std::strcmp(encoding, "json") == 0) {
            enc = db::Encoding::Json;
        } else {
            SafeStrCopy(error_buffer, error_buffer_size, "Invalid encoding. Must be 'dbn', 'csv', or 'json'");
            return nullptr;
        }

        db::Schema parsed_schema;
        try {
            parsed_schema = ParseSchema(schema);
        } catch (const std::exception& e) {
            SafeStrCopy(error_buffer, error_buffer_size, e.what());
            return nullptr;
        }

        // Call databento-cpp method
        auto fields = wrapper->client->MetadataListFields(enc, parsed_schema);

        // Convert to JSON array - match C# FieldDetail properties
        json j = json::array();
        for (const auto& field : fields) {
            json field_obj;
            field_obj["Name"] = field.name;
            field_obj["TypeName"] = field.type;
            // Note: EncodingType is set in C# from the encoding parameter
            j.push_back(field_obj);
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_metadata_get_dataset_condition(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
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

        ValidateNonEmptyString("dataset", dataset);

        std::vector<db::DatasetConditionDetail> conditions =
            wrapper->client->MetadataGetDatasetCondition(dataset);

        // Convert to JSON - match C# DatasetConditionInfo properties
        json j = json::object();
        if (!conditions.empty()) {
            const auto& condition = conditions[0];
            j["Dataset"] = dataset;

            // Convert condition string to PascalCase for C# enum (e.g., "available" -> "Available")
            std::string condition_str = db::ToString(condition.condition);
            if (!condition_str.empty()) {
                condition_str[0] = static_cast<char>(std::toupper(static_cast<unsigned char>(condition_str[0])));
            }
            j["Condition"] = condition_str;

            // LastModified date (databento-cpp already provides ISO 8601 format)
            if (condition.last_modified_date) {
                j["LastModified"] = *condition.last_modified_date;
            }
            // Note: databento-cpp doesn't provide a message field
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_metadata_get_dataset_condition_with_date_range(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* start_date,
    const char* end_date,  // Can be nullptr
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

        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("start_date", start_date);

        // Create DateRange - if end_date is nullptr or empty, create with just start
        // Call databento-cpp method with date range
        std::vector<db::DatasetConditionDetail> conditions;
        if (end_date && *end_date != '\0') {
            conditions = wrapper->client->MetadataGetDatasetCondition(dataset, db::DateRange{start_date, end_date});
        } else {
            conditions = wrapper->client->MetadataGetDatasetCondition(dataset, db::DateRange{start_date});
        }

        // Convert to JSON array - match C# DatasetConditionDetail properties
        json j = json::array();
        for (const auto& condition : conditions) {
            json condition_obj;
            condition_obj["Date"] = condition.date;

            // Convert condition string to PascalCase for C# enum (e.g., "available" -> "Available")
            std::string condition_str = db::ToString(condition.condition);
            if (!condition_str.empty()) {
                condition_str[0] = static_cast<char>(std::toupper(static_cast<unsigned char>(condition_str[0])));
            }
            condition_obj["Condition"] = condition_str;

            // LastModifiedDate (optional)
            if (condition.last_modified_date) {
                condition_obj["LastModifiedDate"] = *condition.last_modified_date;
            }

            j.push_back(condition_obj);
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_metadata_get_dataset_range(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
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

        ValidateNonEmptyString("dataset", dataset);

        db::DatasetRange range = wrapper->client->MetadataGetDatasetRange(dataset);

        // Convert to JSON - match C# DatasetRange properties
        // databento-cpp already provides ISO 8601 format, use as-is
        json j = json::object();
        j["Start"] = range.start;
        j["End"] = range.end;

        // Include range_by_schema mapping
        if (!range.range_by_schema.empty()) {
            json range_by_schema_obj = json::object();
            for (const auto& [schema, schema_range] : range.range_by_schema) {
                // Convert schema enum to string - use as-is since C# uses string keys
                std::string schema_str = db::ToString(schema);

                json schema_range_obj = json::object();
                schema_range_obj["Start"] = schema_range.start;
                schema_range_obj["End"] = schema_range.end;

                range_by_schema_obj[schema_str] = schema_range_obj;
            }
            j["RangeBySchema"] = range_by_schema_obj;
        }

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API uint64_t dbento_metadata_get_record_count(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char** symbols,
    size_t symbol_count,
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
            return UINT64_MAX;
        }

        // Validate inputs
        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);
        ValidateSymbolArray(symbols, symbol_count);
        ValidateTimeRange(start_time_ns, end_time_ns);

        // Convert symbols to vector
        std::vector<std::string> symbol_vec;
        for (size_t i = 0; i < symbol_count; ++i) {
            if (symbols[i]) {
                symbol_vec.emplace_back(symbols[i]);
            }
        }

        // Parse schema
        db::Schema schema_enum = ParseSchema(schema);

        // Convert timestamps
        auto start_unix = NsToUnixNanos(start_time_ns);
        auto end_unix = NsToUnixNanos(end_time_ns);
        db::DateTimeRange<db::UnixNanos> datetime_range{start_unix, end_unix};

        // Get record count
        uint64_t count = wrapper->client->MetadataGetRecordCount(
            dataset, datetime_range, symbol_vec, schema_enum);

        return count;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return UINT64_MAX;
    }
}

DATABENTO_API uint64_t dbento_metadata_get_billable_size(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char** symbols,
    size_t symbol_count,
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
            return UINT64_MAX;
        }

        // Validate inputs
        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);
        ValidateSymbolArray(symbols, symbol_count);
        ValidateTimeRange(start_time_ns, end_time_ns);

        // Convert symbols to vector
        std::vector<std::string> symbol_vec;
        for (size_t i = 0; i < symbol_count; ++i) {
            if (symbols[i]) {
                symbol_vec.emplace_back(symbols[i]);
            }
        }

        // Parse schema
        db::Schema schema_enum = ParseSchema(schema);

        // Convert timestamps
        auto start_unix = NsToUnixNanos(start_time_ns);
        auto end_unix = NsToUnixNanos(end_time_ns);
        db::DateTimeRange<db::UnixNanos> datetime_range{start_unix, end_unix};

        // Get billable size
        uint64_t size = wrapper->client->MetadataGetBillableSize(
            dataset, datetime_range, symbol_vec, schema_enum);

        return size;
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return UINT64_MAX;
    }
}

DATABENTO_API const char* dbento_metadata_get_cost(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char** symbols,
    size_t symbol_count,
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

        // Validate inputs
        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);
        ValidateSymbolArray(symbols, symbol_count);
        ValidateTimeRange(start_time_ns, end_time_ns);

        // Convert symbols to vector
        std::vector<std::string> symbol_vec;
        for (size_t i = 0; i < symbol_count; ++i) {
            if (symbols[i]) {
                symbol_vec.emplace_back(symbols[i]);
            }
        }

        // Parse schema
        db::Schema schema_enum = ParseSchema(schema);

        // Convert timestamps
        auto start_unix = NsToUnixNanos(start_time_ns);
        auto end_unix = NsToUnixNanos(end_time_ns);
        db::DateTimeRange<db::UnixNanos> datetime_range{start_unix, end_unix};

        // Get cost
        double cost = wrapper->client->MetadataGetCost(
            dataset, datetime_range, symbol_vec, schema_enum);

        // Return cost as string
        std::string cost_str = std::to_string(cost);
        return AllocateString(cost_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}

DATABENTO_API const char* dbento_metadata_get_billing_info(
    DbentoHistoricalClientHandle handle,
    const char* dataset,
    const char* schema,
    int64_t start_time_ns,
    int64_t end_time_ns,
    const char** symbols,
    size_t symbol_count,
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

        // Validate inputs
        ValidateNonEmptyString("dataset", dataset);
        ValidateNonEmptyString("schema", schema);
        ValidateSymbolArray(symbols, symbol_count);
        ValidateTimeRange(start_time_ns, end_time_ns);

        // Convert symbols to vector
        std::vector<std::string> symbol_vec;
        for (size_t i = 0; i < symbol_count; ++i) {
            if (symbols[i]) {
                symbol_vec.emplace_back(symbols[i]);
            }
        }

        // Parse schema
        db::Schema schema_enum = ParseSchema(schema);

        // Convert timestamps
        auto start_unix = NsToUnixNanos(start_time_ns);
        auto end_unix = NsToUnixNanos(end_time_ns);
        db::DateTimeRange<db::UnixNanos> datetime_range{start_unix, end_unix};

        // Get all billing info in one go
        uint64_t record_count = wrapper->client->MetadataGetRecordCount(
            dataset, datetime_range, symbol_vec, schema_enum);
        uint64_t billable_size = wrapper->client->MetadataGetBillableSize(
            dataset, datetime_range, symbol_vec, schema_enum);
        double cost = wrapper->client->MetadataGetCost(
            dataset, datetime_range, symbol_vec, schema_enum);

        // Convert to JSON
        json j = json::object();
        j["RecordCount"] = record_count;
        j["BillableSizeBytes"] = billable_size;
        j["Cost"] = cost;

        std::string json_str = j.dump();
        return AllocateString(json_str);
    }
    catch (const std::exception& e) {
        SafeStrCopy(error_buffer, error_buffer_size, e.what());
        return nullptr;
    }
}
