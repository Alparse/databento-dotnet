#!/bin/bash
# Build script for Databento.Native (Linux/macOS)

set -e

# Parse arguments
CONFIGURATION="Release"
CLEAN=false

while [[ $# -gt 0 ]]; do
    case $1 in
        -c|--configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        --clean)
            CLEAN=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Get directories
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
NATIVE_DIR="$ROOT_DIR/src/Databento.Native"
BUILD_DIR="$ROOT_DIR/build/native"

echo "========================================"
echo "Building Databento.Native"
echo "Configuration: $CONFIGURATION"
echo "========================================"

# Clean if requested
if [ "$CLEAN" = true ] && [ -d "$BUILD_DIR" ]; then
    echo "Cleaning build directory..."
    rm -rf "$BUILD_DIR"
fi

# Create build directory
mkdir -p "$BUILD_DIR"

# Check for CMake
if ! command -v cmake &> /dev/null; then
    echo "Error: CMake not found. Please install CMake."
    exit 1
fi

# Configure
echo ""
echo "Configuring CMake..."
cd "$BUILD_DIR"
cmake -S "$NATIVE_DIR" -B . -DCMAKE_BUILD_TYPE=$CONFIGURATION

# Build
echo ""
echo "Building..."
cmake --build . --config $CONFIGURATION

echo ""
echo "========================================"
echo "Build completed successfully!"
echo "========================================"
