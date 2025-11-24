# Deployment Guide - v3.0.24-beta

**Version**: 3.0.24-beta
**Release Date**: November 20, 2025
**Type**: Critical Bug Fix Release

---

## Pre-Deployment Checklist

Before deploying, verify all items are complete:

- [x] **Code Changes**: All bug fixes implemented and tested
- [x] **Native Library**: databento_native.dll built and updated (784KB)
- [x] **Version Numbers**: Updated in all .csproj files to 3.0.24-beta
- [x] **Documentation**:
  - [x] CHANGELOG.md created
  - [x] RELEASE_NOTES_v3.0.24-beta.md created
  - [x] API_REFERENCE.md updated (crash warnings removed)
  - [x] README.md updated (version number)
  - [x] LOG_FORMAT_VERIFICATION.md created
- [x] **Testing**:
  - [x] All 33 examples passing (100% success rate)
  - [x] Critical bug verified fixed
  - [x] No regressions detected
- [x] **Git Commit**: All changes committed to master branch
  - Commit: aa93f93 "fix: Resolve critical AccessViolationException in Historical and Batch APIs"

---

## Deployment Steps

### Step 1: Final Build Verification

Build the Release configuration and verify all projects compile:

```bash
# Clean previous builds
dotnet clean -c Release

# Build solution
dotnet build databento-dotnet.sln -c Release

# Expected output:
# Build succeeded.
#     0 Error(s)
#     89 Warning(s) (XML documentation only - EXPECTED)
```

**Verification**:
- ✅ Zero errors
- ✅ Only XML documentation warnings (expected and acceptable)
- ✅ All projects build successfully

### Step 2: Create NuGet Package

Build the NuGet package from the main library project:

```bash
# Navigate to the Client project
cd src/Databento.Client

# Create NuGet package
dotnet pack -c Release -o ../../

# Expected output:
# Successfully created package 'C:\Users\serha\source\repos\databento-dotnet\Databento.Client.3.0.24-beta.nupkg'
```

**Verification**:
- ✅ Package created: `Databento.Client.3.0.24-beta.nupkg`
- ✅ Package size: ~800KB (includes native DLL + dependencies)

### Step 3: Inspect Package Contents

Verify the package contains all necessary files:

```bash
# Extract and inspect (PowerShell)
Expand-Archive -Path Databento.Client.3.0.24-beta.nupkg -DestinationPath ./pkg-inspect

# Or use NuGet Package Explorer (GUI tool)
# Download from: https://github.com/NuGetPackageExplorer/NuGetPackageExplorer
```

**Expected Contents**:
```
Databento.Client.3.0.24-beta.nupkg/
├── lib/net8.0/
│   ├── Databento.Client.dll
│   ├── Databento.Client.xml (documentation)
│   └── Databento.Interop.dll
├── runtimes/
│   └── win-x64/native/
│       ├── databento_native.dll (784KB)
│       ├── libcrypto-3-x64.dll
│       ├── libssl-3-x64.dll
│       ├── zstd.dll
│       ├── zlib1.dll
│       ├── legacy.dll
│       ├── msvcp140.dll
│       ├── vcruntime140.dll
│       └── vcruntime140_1.dll
├── build/
│   └── Databento.Client.targets
├── README.md
└── Databento.Client.nuspec
```

**Verification Checklist**:
- [x] Native DLL included (databento_native.dll)
- [x] All dependency DLLs included
- [x] XML documentation included
- [x] README.md included
- [x] Build targets included

### Step 4: Local Testing

Test the package locally before publishing:

```bash
# Create a test project
mkdir nuget-test
cd nuget-test
dotnet new console -n TestPackage
cd TestPackage

# Add local package source
dotnet nuget add source C:\Users\serha\source\repos\databento-dotnet --name LocalPackages

# Install the package
dotnet add package Databento.Client --version 3.0.24-beta --source LocalPackages

# Verify package restored
dotnet restore

# Create simple test
# (Edit Program.cs to use Databento.Client)

# Run test
dotnet run
```

**Verification**:
- ✅ Package installs without errors
- ✅ Native DLL loads correctly
- ✅ No DllNotFoundException
- ✅ API accessible and functional

### Step 5: Push to Git Remote

Push the commit and create a release tag:

```bash
# Push commit to remote
git push origin master

# Create and push version tag
git tag v3.0.24-beta
git push origin v3.0.24-beta

# Expected output:
# To https://github.com/Alparse/databento-dotnet.git
#  * [new tag]         v3.0.24-beta -> v3.0.24-beta
```

**Verification**:
- ✅ Commit pushed successfully
- ✅ Tag created and pushed
- ✅ GitHub shows new tag at: https://github.com/Alparse/databento-dotnet/releases

### Step 6: Create GitHub Release

Create a GitHub release for the tag:

1. Navigate to: https://github.com/Alparse/databento-dotnet/releases/new
2. Select tag: `v3.0.24-beta`
3. Set release title: `v3.0.24-beta - Critical Bug Fix Release`
4. Copy release notes from `RELEASE_NOTES_v3.0.24-beta.md`
5. Attach package file: `Databento.Client.3.0.24-beta.nupkg`
6. Mark as pre-release: ✅ (beta version)
7. Click "Publish release"

**Verification**:
- ✅ Release visible at: https://github.com/Alparse/databento-dotnet/releases/tag/v3.0.24-beta
- ✅ Release notes formatted correctly
- ✅ Package file attached

### Step 7: Publish to NuGet.org

Publish the package to the public NuGet repository:

```bash
# Set NuGet API key (one-time setup)
# Get key from: https://www.nuget.org/account/apikeys
dotnet nuget push Databento.Client.3.0.24-beta.nupkg \
    --api-key YOUR_NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json

# Expected output:
# Pushing Databento.Client.3.0.24-beta.nupkg to 'https://www.nuget.org'...
# Your package was pushed.
```

**⚠️ IMPORTANT**: Double-check all verifications before publishing. NuGet packages cannot be deleted, only unlisted.

**Verification**:
- ✅ Package uploaded successfully
- ✅ Package visible at: https://www.nuget.org/packages/Databento.Client/3.0.24-beta
- ✅ NuGet page shows correct version, description, and release notes
- ✅ Dependencies listed correctly

### Step 8: Post-Deployment Verification

After publishing, verify the package is usable:

```bash
# Wait 5-10 minutes for NuGet indexing

# Create fresh test project
mkdir nuget-verify
cd nuget-verify
dotnet new console -n VerifyDeploy
cd VerifyDeploy

# Install from NuGet.org
dotnet add package Databento.Client --version 3.0.24-beta --prerelease

# Verify installation
dotnet restore
dotnet build

# Run basic functionality test
dotnet run
```

**Verification**:
- ✅ Package installs from NuGet.org
- ✅ No errors during restore/build
- ✅ Native DLL loads correctly
- ✅ API functional

---

## Post-Deployment Tasks

### Update Repository

- [x] Commit all deployment documentation
- [ ] Update GitHub README badges (if version badge exists)
- [ ] Close related issues (#1 - AccessViolationException)
- [ ] Announce release in discussions/community

### Communication

**Internal**:
- Notify team of successful deployment
- Document any deployment issues encountered

**External**:
- Consider announcing on:
  - GitHub Discussions
  - Twitter/X (if applicable)
  - Developer blog (if applicable)

**Sample Announcement**:
```
🎉 databento-dotnet v3.0.24-beta is now available!

✅ CRITICAL FIX: Resolved AccessViolationException crash in Historical
   and Batch APIs when server returns warnings

✅ All 33 examples now passing (100% success rate)
✅ Zero API changes - fully backward compatible
✅ Enhanced logging with better diagnostics

📦 Install: dotnet add package Databento.Client --version 3.0.24-beta --prerelease
📚 Release Notes: https://github.com/Alparse/databento-dotnet/releases/tag/v3.0.24-beta

#databento #dotnet #marketdata #bugfix
```

### Monitor

After deployment, monitor for:
- Download statistics (NuGet.org)
- GitHub issues (new bug reports)
- User feedback (discussions, social media)
- Error reports (if telemetry enabled)

**Monitoring Links**:
- NuGet Stats: https://www.nuget.org/stats/packages/Databento.Client?groupby=Version
- GitHub Issues: https://github.com/Alparse/databento-dotnet/issues
- GitHub Insights: https://github.com/Alparse/databento-dotnet/pulse

---

## Rollback Procedure

If critical issues are discovered after deployment:

### Option 1: Unlist Package (Recommended)

```bash
# Unlist the problematic version (makes it invisible but doesn't delete)
dotnet nuget delete Databento.Client 3.0.24-beta \
    --api-key YOUR_NUGET_API_KEY \
    --source https://api.nuget.org/v3/index.json \
    --non-interactive

# Note: This unlists but doesn't delete. Users with explicit version
# references can still use it.
```

### Option 2: Hotfix Release

If the issue can be quickly fixed:

1. Create hotfix branch: `git checkout -b hotfix/3.0.25-beta`
2. Implement fix
3. Update version to 3.0.25-beta
4. Test thoroughly
5. Follow deployment steps for 3.0.25-beta
6. Update CHANGELOG.md with hotfix details

### Option 3: Revert to Previous Version

Communicate to users:
```
⚠️ CRITICAL: Please downgrade to v3.0.23-beta

We discovered a critical issue in v3.0.24-beta that affects [describe issue].

To downgrade:
dotnet add package Databento.Client --version 3.0.23-beta

We are working on a fix and will release v3.0.25-beta shortly.

Sorry for the inconvenience!
```

---

## Troubleshooting Deployment Issues

### Issue: Package Build Fails

**Symptoms**: `dotnet pack` fails with errors

**Solutions**:
1. Clean solution: `dotnet clean -c Release`
2. Delete bin/obj folders: `rm -rf src/*/bin src/*/obj`
3. Rebuild: `dotnet build -c Release`
4. Try pack again: `dotnet pack -c Release`

### Issue: Native DLL Not Included

**Symptoms**: Package installs but DllNotFoundException at runtime

**Solutions**:
1. Verify DLL exists: `ls src/Databento.Interop/runtimes/win-x64/native/databento_native.dll`
2. Check .csproj packaging rules in Databento.Interop.csproj
3. Manually inspect package contents (Step 3)
4. Rebuild native library: `./build/build-native.ps1 -Configuration Release`

### Issue: NuGet Push Fails

**Symptoms**: `dotnet nuget push` returns error

**Common Causes**:
1. **Invalid API Key**: Verify key at https://www.nuget.org/account/apikeys
2. **Version Already Exists**: Cannot push same version twice
3. **Package Validation Errors**: Fix validation errors and rebuild
4. **Network Issues**: Retry with `--timeout 300` flag

### Issue: Package Not Appearing on NuGet.org

**Wait Time**: NuGet indexing can take 5-15 minutes

**Verification**:
1. Check upload status: https://www.nuget.org/packages/Databento.Client
2. Search by exact version: https://www.nuget.org/packages/Databento.Client/3.0.24-beta
3. Clear local NuGet cache: `dotnet nuget locals all --clear`

---

## Deployment Checklist Summary

### Pre-Deployment
- [x] Code complete and tested
- [x] Documentation updated
- [x] Version numbers bumped
- [x] Git commit created

### Deployment
- [ ] Build Release configuration
- [ ] Create NuGet package
- [ ] Inspect package contents
- [ ] Local testing passed
- [ ] Git push and tag
- [ ] GitHub release created
- [ ] NuGet.org publish
- [ ] Post-deployment verification

### Post-Deployment
- [ ] Close related issues
- [ ] Update badges/README
- [ ] Announce release
- [ ] Monitor for issues

---

## Notes

- **Version Convention**: `MAJOR.MINOR.PATCH-beta` (Semantic Versioning)
- **API Key Security**: Never commit API keys to version control
- **Package Immutability**: NuGet packages cannot be deleted, only unlisted
- **Testing Priority**: Always test locally before publishing to NuGet.org

---

## References

- [NuGet Package Publishing Guide](https://docs.microsoft.com/en-us/nuget/nuget-org/publish-a-package)
- [Semantic Versioning](https://semver.org/)
- [Keep a Changelog](https://keepachangelog.com/)
- [GitHub Releases Documentation](https://docs.github.com/en/repositories/releasing-projects-on-github)

---

**Deployment Guide Version**: 1.0
**Last Updated**: November 20, 2025
**Status**: Ready for deployment
