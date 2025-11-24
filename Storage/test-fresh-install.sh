#!/bin/bash
# Test Fresh Install - Databento.Client VC++ Runtime DLLs
# This script simulates a first-time user installing the package
# and verifies all VC++ runtime DLLs are present in output directory

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

echo -e "${CYAN}=== Databento.Client Fresh Install Test ===${NC}"
echo ""

# Test configuration
TEST_DIR="/tmp/databento-fresh-install-test-$(date +%Y%m%d-%H%M%S)"
REQUIRED_DLLS=("databento_native.dll" "msvcp140.dll" "vcruntime140.dll" "vcruntime140_1.dll")

echo -e "${GRAY}Test Directory: $TEST_DIR${NC}"
echo ""

# Create isolated test directory
echo -e "${YELLOW}[1/7] Creating isolated test directory...${NC}"
mkdir -p "$TEST_DIR"
cd "$TEST_DIR"

# Clear NuGet cache to ensure fresh download
echo -e "${YELLOW}[2/7] Clearing NuGet cache (ensures fresh download from NuGet.org)...${NC}"
dotnet nuget locals all --clear > /dev/null 2>&1

# Create new console project
echo -e "${YELLOW}[3/7] Creating new .NET console project...${NC}"
dotnet new console --force --name FreshInstallTest > /dev/null 2>&1

cd FreshInstallTest

# Install package from NuGet.org (latest prerelease)
echo -e "${YELLOW}[4/7] Installing Databento.Client from NuGet.org...${NC}"
echo -e "${GRAY}      Running: dotnet add package Databento.Client --prerelease${NC}"

INSTALL_OUTPUT=$(dotnet add package Databento.Client --prerelease 2>&1)
VERSION=$(echo "$INSTALL_OUTPUT" | grep -oP "version '\K[^']+" | head -1)

if [ -n "$VERSION" ]; then
    echo -e "${GREEN}      Installed version: $VERSION${NC}"
else
    echo -e "${RED}      ERROR: Could not determine installed version!${NC}"
    echo "$INSTALL_OUTPUT"
    exit 1
fi

# Build the project
echo -e "${YELLOW}[5/7] Building project...${NC}"
BUILD_OUTPUT=$(dotnet build --verbosity quiet 2>&1)

if [ $? -ne 0 ]; then
    echo -e "${RED}      ERROR: Build failed!${NC}"
    echo "$BUILD_OUTPUT"
    exit 1
fi

echo -e "${GREEN}      Build succeeded${NC}"

# Detect target framework
echo -e "${YELLOW}[6/7] Detecting output directory...${NC}"
if [ ! -d "bin/Debug" ]; then
    echo -e "${RED}      ERROR: No output directory found!${NC}"
    exit 1
fi

TARGET_FRAMEWORK=$(ls bin/Debug | head -1)
OUTPUT_DIR="bin/Debug/$TARGET_FRAMEWORK"

echo -e "${GRAY}      Target Framework: $TARGET_FRAMEWORK${NC}"
echo -e "${GRAY}      Output Directory: $OUTPUT_DIR${NC}"

# Check for required DLLs
echo -e "${YELLOW}[7/7] Verifying VC++ runtime DLLs in output directory...${NC}"
echo ""

ALL_FOUND=true
MISSING_DLLS=()

for DLL in "${REQUIRED_DLLS[@]}"; do
    DLL_PATH="$OUTPUT_DIR/$DLL"

    if [ -f "$DLL_PATH" ]; then
        SIZE_KB=$(du -k "$DLL_PATH" | cut -f1)

        # Special highlighting for VC++ DLLs
        if [[ "$DLL" =~ (msvcp|vcruntime) ]]; then
            echo -e "   ${GREEN}✓ $DLL${NC} ${GRAY}($SIZE_KB KB)${NC} ${CYAN}← VC++ RUNTIME${NC}"
        else
            echo -e "   ${GREEN}✓ $DLL ($SIZE_KB KB)${NC}"
        fi
    else
        ALL_FOUND=false
        MISSING_DLLS+=("$DLL")

        if [[ "$DLL" =~ (msvcp|vcruntime) ]]; then
            echo -e "   ${RED}✗ $DLL ← MISSING VC++ RUNTIME!${NC}"
        else
            echo -e "   ${RED}✗ $DLL${NC}"
        fi
    fi
done

echo ""
echo -e "${CYAN}========================================${NC}"

if [ "$ALL_FOUND" = true ]; then
    echo -e "${GREEN}✅ TEST PASSED!${NC}"
    echo ""
    echo -e "${GREEN}All required VC++ runtime DLLs are present in output directory.${NC}"
    echo -e "${GREEN}Package version $VERSION is working correctly.${NC}"
    echo ""
    echo -e "This means:"
    echo -e "  • Users WITHOUT system VC++ runtime should be able to run the application"
    echo -e "  • DLLs are bundled correctly in the NuGet package"
    echo -e "  • Build targets are executing properly"
    echo ""
else
    echo -e "${RED}❌ TEST FAILED!${NC}"
    echo ""
    echo -e "${RED}Missing DLLs:${NC}"
    for DLL in "${MISSING_DLLS[@]}"; do
        echo -e "  ${RED}• $DLL${NC}"
    done
    echo ""
    echo -e "This means:"
    echo -e "  ${RED}• Users WITHOUT system VC++ runtime WILL experience DllNotFoundException${NC}"
    echo -e "  ${RED}• Either DLLs are not in the package OR build targets aren't executing${NC}"
    echo ""
fi

# Additional diagnostics
echo -e "${CYAN}========================================${NC}"
echo -e "${YELLOW}Additional Diagnostics:${NC}"
echo ""

# Check if DLLs exist in NuGet cache
echo -e "${GRAY}Checking NuGet package cache...${NC}"
NUGET_CACHE_PATH="$HOME/.nuget/packages/databento.client/$VERSION/runtimes/win-x64/native"
if [ -d "$NUGET_CACHE_PATH" ]; then
    echo -e "  ${GREEN}Cache path exists: $NUGET_CACHE_PATH${NC}"

    VC_DLL_COUNT=$(find "$NUGET_CACHE_PATH" -name "*vcruntime*.dll" -o -name "msvcp140.dll" | wc -l)

    if [ "$VC_DLL_COUNT" -eq 3 ]; then
        echo -e "  ${GREEN}✓ All 3 VC++ DLLs found in NuGet cache${NC}"
        find "$NUGET_CACHE_PATH" -name "*vcruntime*.dll" -o -name "msvcp140.dll" | xargs -I {} basename {} | sed "s/^/${GRAY}    - /" | sed "s/$/${NC}/"
    else
        echo -e "  ${RED}✗ VC++ DLLs missing from NuGet cache! (Found: $VC_DLL_COUNT/3)${NC}"
    fi
else
    echo -e "  ${RED}✗ NuGet cache path not found: $NUGET_CACHE_PATH${NC}"
fi

echo ""

# Check runtimes subdirectory
echo -e "${GRAY}Checking runtimes subdirectory in output...${NC}"
RUNTIMES_PATH="$OUTPUT_DIR/runtimes/win-x64/native"
if [ -d "$RUNTIMES_PATH" ]; then
    echo -e "  ${GREEN}✓ Runtimes subdirectory exists: $RUNTIMES_PATH${NC}"

    RUNTIME_VC_COUNT=$(find "$RUNTIMES_PATH" -name "*vcruntime*.dll" -o -name "msvcp140.dll" 2>/dev/null | wc -l)

    if [ "$RUNTIME_VC_COUNT" -gt 0 ]; then
        echo -e "  ${GRAY}Note: DLLs also in runtimes subdirectory (copied by NuGet)${NC}"
        echo -e "  ${GRAY}These are NOT used at runtime - root directory DLLs are used${NC}"
    fi
else
    echo -e "  ${GRAY}Runtimes subdirectory not found (this is OK if DLLs are in root)${NC}"
fi

echo ""

# Show test location
echo -e "${CYAN}========================================${NC}"
echo -e "${YELLOW}Test Location:${NC}"
echo -e "  $TEST_DIR"
echo ""
echo -e "${GRAY}To inspect manually:${NC}"
echo -e "${GRAY}  cd \"$TEST_DIR/FreshInstallTest\"${NC}"
echo -e "${GRAY}  ls -la $OUTPUT_DIR/*.dll${NC}"
echo ""

# Cleanup prompt
echo -n -e "${YELLOW}Keep test directory? [y/N]: ${NC}"
read -r KEEP

if [ "$KEEP" != "y" ] && [ "$KEEP" != "Y" ]; then
    echo -e "${GRAY}Cleaning up test directory...${NC}"
    cd /tmp
    rm -rf "$TEST_DIR"
    echo -e "${GREEN}Cleaned up.${NC}"
else
    echo -e "${GREEN}Test directory preserved for inspection.${NC}"
fi

echo ""
echo -e "${CYAN}Test complete!${NC}"

# Return exit code based on test result
if [ "$ALL_FOUND" = true ]; then
    exit 0
else
    exit 1
fi
