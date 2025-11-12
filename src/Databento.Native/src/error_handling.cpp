#include "databento_native.h"
#include <cstring>
#include <string>

// ============================================================================
// Error Handling Utilities
// ============================================================================

// Error codes
namespace ErrorCodes {
    constexpr int Success = 0;
    constexpr int InvalidHandle = -1;
    constexpr int InvalidParameter = -2;
    constexpr int ApiError = -3;
    constexpr int NetworkError = -4;
    constexpr int ParseError = -5;
    constexpr int TimeoutError = -6;
    constexpr int UnknownError = -99;
}

// Helper function to categorize exceptions
int CategorizeException(const std::exception& e) {
    std::string msg = e.what();

    if (msg.find("network") != std::string::npos ||
        msg.find("connection") != std::string::npos) {
        return ErrorCodes::NetworkError;
    }

    if (msg.find("parse") != std::string::npos ||
        msg.find("invalid") != std::string::npos) {
        return ErrorCodes::ParseError;
    }

    if (msg.find("timeout") != std::string::npos) {
        return ErrorCodes::TimeoutError;
    }

    if (msg.find("API") != std::string::npos ||
        msg.find("unauthorized") != std::string::npos) {
        return ErrorCodes::ApiError;
    }

    return ErrorCodes::UnknownError;
}

// ============================================================================
// Memory Management
// ============================================================================

DATABENTO_API void dbento_free_string(char* str) {
    if (str) {
        delete[] str;
    }
}
