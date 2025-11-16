# Building databento-dotnet

This guide covers the complete build process for the databento-dotnet project.

## Prerequisites

### Required Tools

1. **.NET 8 SDK**
   - Download: https://dotnet.microsoft.com/download/dotnet/8.0
   - Verify: `dotnet --version` (should be 8.0 or later)

2. **CMake 3.24+**
   - Download: https://cmake.org/download/
   - Verify: `cmake --version`

3. **C++17 Compiler**
   - **Windows**: Visual Studio 2019+ with C++ workload
   - **Linux**: GCC 9+ or Clang 10+
   - **macOS**: Xcode 11+

### Platform-Specific Setup

#### Windows

```powershell
# Install via Chocolatey (optional)
choco install cmake visualstudio2022buildtools --installargs "--add Microsoft.VisualStudio.Workload.VCTools"

# Or download Visual Studio with C++ Desktop Development workload
# https://visualstudio.microsoft.com/downloads/
```

#### Linux (Ubuntu/Debian)

```bash
sudo apt-get update
sudo apt-get install -y cmake build-essential libssl-dev libzstd-dev pkg-config
```

#### macOS

```bash
# Install Xcode Command Line Tools
xcode-select --install

# Install dependencies via Homebrew
brew install cmake openssl zstd
```

## Build Steps

### Option 1: Build Everything (Recommended)

```bash
# Windows
.\build\build-all.ps1 -Configuration Release

# Linux/macOS
./build/build-all.sh --configuration Release
```

This builds:
1. Native C++ library (databento_native)
2. All .NET projects (Databento.Client, Databento.Interop)
3. All example projects (26+ examples)

### Option 2: Step-by-Step Build

#### Step 1: Build Native Library

```bash
# Windows
cd src\Databento.Native
mkdir build
cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build . --config Release

# Linux/macOS
cd src/Databento.Native
mkdir build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
cmake --build .
```

The native library will be automatically copied to:
```
src/Databento.Interop/runtimes/{RID}/native/
```

Where `{RID}` is:
- `win-x64` for Windows
- `linux-x64` for Linux
- `osx-x64` or `osx-arm64` for macOS

#### Step 2: Build .NET Solution

```bash
cd ../../..  # Back to root
dotnet restore
dotnet build databento-dotnet.sln -c Release
```

## Verifying the Build

### Check Native Library

```bash
# Windows
dir src\Databento.Interop\runtimes\win-x64\native\databento_native.dll

# Linux
ls -la src/Databento.Interop/runtimes/linux-x64/native/libdatabento_native.so

# macOS
ls -la src/Databento.Interop/runtimes/osx-arm64/native/libdatabento_native.dylib
```

### Check .NET Build

```bash
ls src/Databento.Client/bin/Release/net8.0/
```

You should see:
- `Databento.Client.dll`
- `Databento.Interop.dll`
- Native library (copied)

### Run an Example

```bash
# Set your API key
export DATABENTO_API_KEY="your-api-key"  # Linux/macOS
$env:DATABENTO_API_KEY="your-api-key"   # Windows

# Run an example
dotnet run --project examples/LiveStreaming.Example
```

## Troubleshooting

### CMake Can't Find Compiler

**Windows:**
```powershell
# Use Visual Studio Developer Command Prompt
# Or specify generator:
cmake .. -G "Visual Studio 17 2022" -A x64
```

**Linux/macOS:**
```bash
# Ensure compiler is in PATH
which gcc
which g++
```

### CMake Can't Fetch databento-cpp

Ensure you have internet access and git installed:

```bash
git --version
```

If behind a proxy, configure git:

```bash
git config --global http.proxy http://proxy:port
```

### OpenSSL Not Found

**Linux:**
```bash
sudo apt-get install libssl-dev
```

**macOS:**
```bash
brew install openssl
export OPENSSL_ROOT_DIR=$(brew --prefix openssl)
cmake .. -DOPENSSL_ROOT_DIR=$OPENSSL_ROOT_DIR
```

**Windows:**
CMake should find OpenSSL via vcpkg or system installation.

### Native Library Not Copied

Manually copy the library:

```bash
# Windows
copy build\native\Release\databento_native.dll src\Databento.Interop\runtimes\win-x64\native\

# Linux
cp build/native/libdatabento_native.so src/Databento.Interop/runtimes/linux-x64/native/

# macOS
cp build/native/libdatabento_native.dylib src/Databento.Interop/runtimes/osx-arm64/native/
```

### .NET Build Fails with P/Invoke Errors

Ensure native library is present and has correct permissions:

```bash
# Linux/macOS
chmod +x src/Databento.Interop/runtimes/*/native/*
```

## Clean Build

To clean all build artifacts:

```bash
# Windows
.\build\build-native.ps1 -Clean
dotnet clean

# Linux/macOS
./build/build-native.sh --clean
dotnet clean

# Or manually
rm -rf build/native
rm -rf src/Databento.Interop/runtimes
dotnet clean
```

## Cross-Platform Builds

### Building for Multiple Platforms

You can build for different platforms using Docker or cross-compilation:

```bash
# Build Linux binary on Windows via WSL
wsl ./build/build-native.sh

# Build using Docker
docker run -v $(pwd):/src -w /src mcr.microsoft.com/dotnet/sdk:8.0 \
  /bin/bash -c "apt-get update && apt-get install -y cmake build-essential && ./build/build-all.sh"
```

## Build Configuration Options

### CMake Options

```bash
# Debug build
cmake .. -DCMAKE_BUILD_TYPE=Debug

# Release with debug symbols
cmake .. -DCMAKE_BUILD_TYPE=RelWithDebInfo

# Custom install prefix
cmake .. -DCMAKE_INSTALL_PREFIX=/usr/local

# Verbose build
cmake --build . --verbose
```

### .NET Build Options

```bash
# Debug build
dotnet build -c Debug

# Release build with symbols
dotnet build -c Release /p:DebugType=portable

# Specific framework
dotnet build -f net8.0
```

## Advanced: NuGet Package Creation

To create a NuGet package with all platform binaries:

1. Build native libraries for all platforms
2. Place them in `src/Databento.Interop/runtimes/{RID}/native/`
3. Pack:

```bash
dotnet pack src/Databento.Client/Databento.Client.csproj -c Release
```

The NuGet package will include all native libraries.

## Continuous Integration

Example CI build (GitHub Actions workflows to be added):

```bash
# Install dependencies
dotnet restore

# Build native (skip on hosted runners, use pre-built)
./build/build-native.sh

# Build .NET
dotnet build --no-restore -c Release

# Run examples (optional verification)
dotnet run --project examples/LiveStreaming.Example

# Pack
dotnet pack --no-build -c Release
```

## Next Steps

After successful build:
1. Run examples: See [README.md](README.md#running-examples)
2. Check API documentation: XML docs are generated in `bin/` folders
3. Review [QUICKSTART.md](QUICKSTART.md) for usage examples
