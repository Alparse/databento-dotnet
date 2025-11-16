#pragma once

#include <cstdint>
#include <mutex>
#include <unordered_set>
#include <string>

namespace databento_native {

// Magic number to identify valid handles (DATABENTO in hex: 0xDA7ABE70)
constexpr uint32_t HANDLE_MAGIC = 0xDA7ABE70;

// Handle types for type safety
enum class HandleType : uint32_t {
    LiveClient = 1,
    HistoricalClient = 2,
    TsSymbolMap = 3,
    PitSymbolMap = 4,
    DbnFileReader = 5,
    DbnFileWriter = 6,
    Metadata = 7,
    SymbologyResolution = 8,
    UnitPrices = 9,
    BatchJob = 10,
    LiveBlocking = 11  // Pull-based LiveBlocking client
};

/**
 * Handle header prepended to each wrapper object
 * Provides type safety and validation
 */
struct HandleHeader {
    uint32_t magic;          // Magic number for validation
    HandleType type;         // Type of wrapper
    void* wrapper_ptr;       // Pointer to actual wrapper object

    HandleHeader(HandleType t, void* ptr)
        : magic(HANDLE_MAGIC)
        , type(t)
        , wrapper_ptr(ptr)
    {}
};

/**
 * Thread-safe handle registry
 * Tracks all valid handles to prevent use-after-free
 */
class HandleRegistry {
public:
    static HandleRegistry& Instance() {
        static HandleRegistry instance;
        return instance;
    }

    // Register a new handle
    void Register(HandleHeader* handle) {
        std::lock_guard<std::mutex> lock(mutex_);
        handles_.insert(handle);
    }

    // Unregister a handle (when destroying)
    void Unregister(HandleHeader* handle) {
        std::lock_guard<std::mutex> lock(mutex_);
        handles_.erase(handle);
    }

    // Check if handle is registered
    bool IsRegistered(HandleHeader* handle) {
        std::lock_guard<std::mutex> lock(mutex_);
        return handles_.find(handle) != handles_.end();
    }

    // Get count of registered handles (for diagnostics)
    size_t Count() {
        std::lock_guard<std::mutex> lock(mutex_);
        return handles_.size();
    }

private:
    HandleRegistry() = default;
    ~HandleRegistry() = default;
    HandleRegistry(const HandleRegistry&) = delete;
    HandleRegistry& operator=(const HandleRegistry&) = delete;

    std::mutex mutex_;
    std::unordered_set<HandleHeader*> handles_;
};

/**
 * Validation error codes
 */
enum class ValidationError {
    Success = 0,
    NullHandle = 1,
    InvalidMagic = 2,
    NotRegistered = 3,
    WrongType = 4,
    NullWrapperPtr = 5
};

/**
 * Get error message for validation error
 */
inline const char* GetValidationErrorMessage(ValidationError error) {
    switch (error) {
        case ValidationError::Success:
            return "Success";
        case ValidationError::NullHandle:
            return "Handle is NULL";
        case ValidationError::InvalidMagic:
            return "Invalid handle magic number (corrupted or invalid handle)";
        case ValidationError::NotRegistered:
            return "Handle not registered (possibly freed or never created)";
        case ValidationError::WrongType:
            return "Handle type mismatch (wrong wrapper type)";
        case ValidationError::NullWrapperPtr:
            return "Wrapper pointer is NULL";
        default:
            return "Unknown validation error";
    }
}

/**
 * Validate and cast a handle to its wrapper type
 * Thread-safe validation with comprehensive checks
 *
 * @param handle Opaque handle pointer
 * @param expected_type Expected wrapper type
 * @param error_out Optional output parameter for error code
 * @return Pointer to wrapper object, or nullptr if validation fails
 */
template<typename WrapperType>
WrapperType* ValidateAndCast(void* handle, HandleType expected_type, ValidationError* error_out = nullptr) {
    // Check for NULL handle
    if (!handle) {
        if (error_out) *error_out = ValidationError::NullHandle;
        return nullptr;
    }

    // Cast to handle header
    auto* header = static_cast<HandleHeader*>(handle);

    // Check magic number
    if (header->magic != HANDLE_MAGIC) {
        if (error_out) *error_out = ValidationError::InvalidMagic;
        return nullptr;
    }

    // Check if handle is registered
    if (!HandleRegistry::Instance().IsRegistered(header)) {
        if (error_out) *error_out = ValidationError::NotRegistered;
        return nullptr;
    }

    // Check handle type
    if (header->type != expected_type) {
        if (error_out) *error_out = ValidationError::WrongType;
        return nullptr;
    }

    // Check wrapper pointer
    if (!header->wrapper_ptr) {
        if (error_out) *error_out = ValidationError::NullWrapperPtr;
        return nullptr;
    }

    if (error_out) *error_out = ValidationError::Success;
    return static_cast<WrapperType*>(header->wrapper_ptr);
}

/**
 * Create a validated handle
 * Allocates handle header and registers it
 *
 * @param type Handle type
 * @param wrapper_ptr Pointer to wrapper object
 * @return Opaque handle pointer
 */
inline void* CreateValidatedHandle(HandleType type, void* wrapper_ptr) {
    if (!wrapper_ptr) {
        return nullptr;
    }

    auto* header = new HandleHeader(type, wrapper_ptr);
    HandleRegistry::Instance().Register(header);
    return static_cast<void*>(header);
}

/**
 * Destroy a validated handle
 * Unregisters and frees the handle header
 * NOTE: Does NOT free the wrapper object - caller must do that
 *
 * @param handle Opaque handle pointer
 */
inline void DestroyValidatedHandle(void* handle) {
    if (!handle) {
        return;
    }

    auto* header = static_cast<HandleHeader*>(handle);

    // Unregister first
    HandleRegistry::Instance().Unregister(header);

    // Invalidate magic to catch double-free
    header->magic = 0xDEADDEAD;

    // Free handle header
    delete header;
}

} // namespace databento_native
