cmake_minimum_required(VERSION 3.24)
project(Databento.Native VERSION 1.0.0 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

# ============================================================================
# Fetch databento-cpp (ONLY external dependency)
# ============================================================================
include(FetchContent)

message(STATUS "Fetching databento-cpp...")
FetchContent_Declare(
    databento-cpp
    GIT_REPOSITORY https://github.com/databento/databento-cpp.git
    GIT_TAG        main
)

# Make available - this will also fetch transitive dependencies
# (OpenSSL, zstd, nlohmann_json, etc.)
FetchContent_MakeAvailable(databento-cpp)

# ============================================================================
# Our Native Wrapper Library
# ============================================================================
add_library(databento_native SHARED
    src/live_client_wrapper.cpp
    src/historical_client_wrapper.cpp
    src/symbol_map_wrapper.cpp
    src/callback_bridge.cpp
    src/error_handling.cpp
)

# Link to databento-cpp (brings in all its dependencies)
target_link_libraries(databento_native
    PRIVATE
        databento::databento
)

target_include_directories(databento_native
    PUBLIC
        $<BUILD_INTERFACE:${CMAKE_CURRENT_SOURCE_DIR}/include>
        $<INSTALL_INTERFACE:include>
)

# ============================================================================
# Compiler Settings
# ============================================================================
if(MSVC)
    target_compile_definitions(databento_native PRIVATE DATABENTO_EXPORTS)
    target_compile_options(databento_native PRIVATE /W4)
else()
    target_compile_options(databento_native PRIVATE -Wall -Wextra -Wpedantic)
endif()

# ============================================================================
# Platform-specific Output Settings
# ============================================================================
if(WIN32)
    set_target_properties(databento_native PROPERTIES
        OUTPUT_NAME "databento_native"
        PREFIX ""
        SUFFIX ".dll"
    )
elseif(APPLE)
    set_target_properties(databento_native PROPERTIES
        OUTPUT_NAME "databento_native"
        PREFIX "lib"
        SUFFIX ".dylib"
        INSTALL_RPATH "@loader_path"
    )
else()
    set_target_properties(databento_native PROPERTIES
        OUTPUT_NAME "databento_native"
        PREFIX "lib"
        SUFFIX ".so"
        INSTALL_RPATH "$ORIGIN"
    )
endif()

# ============================================================================
# Determine Runtime Identifier (RID)
# ============================================================================
if(WIN32)
    if(CMAKE_SIZEOF_VOID_P EQUAL 8)
        set(RID "win-x64")
    else()
        set(RID "win-x86")
    endif()
elseif(APPLE)
    if(CMAKE_SYSTEM_PROCESSOR MATCHES "arm64|aarch64")
        set(RID "osx-arm64")
    else()
        set(RID "osx-x64")
    endif()
else()
    if(CMAKE_SYSTEM_PROCESSOR MATCHES "aarch64|arm64")
        set(RID "linux-arm64")
    else()
        set(RID "linux-x64")
    endif()
endif()

message(STATUS "Building for Runtime Identifier: ${RID}")

# ============================================================================
# Copy Output to .NET Runtimes Folder
# ============================================================================
set(DOTNET_RUNTIME_DIR "${CMAKE_SOURCE_DIR}/../Databento.Interop/runtimes/${RID}/native")

add_custom_command(TARGET databento_native POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${DOTNET_RUNTIME_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy $<TARGET_FILE:databento_native> "${DOTNET_RUNTIME_DIR}/"
    COMMENT "Copying databento_native to .NET runtime folder: ${DOTNET_RUNTIME_DIR}"
)

# ============================================================================
# Installation (Optional)
# ============================================================================
install(TARGETS databento_native
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
    RUNTIME DESTINATION bin
)

install(FILES include/databento_native.h
    DESTINATION include
)
