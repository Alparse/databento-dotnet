#pragma once

#include <cstring>

namespace databento_native {

/**
 * Safely copy a C string to a buffer with null termination
 * @param dest Destination buffer
 * @param dest_size Size of destination buffer
 * @param src Source string (can be nullptr)
 */
inline void SafeStrCopy(char* dest, size_t dest_size, const char* src) {
    // Validate destination
    if (!dest || dest_size == 0) {
        return;
    }

    // Handle null source
    if (!src) {
        dest[0] = '\0';
        return;
    }

    // Copy with bounds checking
    strncpy(dest, src, dest_size - 1);
    dest[dest_size - 1] = '\0';  // Ensure null termination
}

}  // namespace databento_native
