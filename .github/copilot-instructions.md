# .NET MAUI HybridWebView Development Instructions

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

## CRITICAL ENVIRONMENT REQUIREMENTS

**NEVER ATTEMPT TO BUILD ON LINUX**: This project requires Windows or macOS with Visual Studio and the .NET MAUI workload. Builds will fail in Linux environments or GitHub Codespaces.

**Required Development Environment:**
- Windows: Visual Studio 2022 with .NET MAUI workload
- macOS: Visual Studio for Mac with .NET MAUI workload  
- .NET 8 SDK (despite HybridWebView targeting .NET 9)
- MAUI workload: `dotnet workload install maui --ignore-failed-sources`

## Working Effectively

### Bootstrap and Build Process
**NEVER CANCEL BUILDS** - MAUI builds can take 15-45 minutes. Set timeouts to 60+ minutes.

1. **Install Prerequisites:**
   ```bash
   # Install .NET 8 SDK first (required despite .NET 9 targets)
   # Download from: https://dotnet.microsoft.com/download/dotnet/8.0
   dotnet --version  # Verify 8.0.x
   ```

2. **Install MAUI Workload (Windows/Mac only):**
   ```bash
   dotnet workload install maui --ignore-failed-sources
   # Takes 5-10 minutes. NEVER CANCEL.
   ```

3. **Restore and Build:**
   ```bash
   # CRITICAL: Takes 10-20 minutes. NEVER CANCEL. Set timeout to 30+ minutes.
   dotnet restore MauiCSharpInteropWebView.sln
   
   # Build core library (5-10 minutes)
   dotnet pack /p:Configuration=Release HybridWebView/HybridWebView.csproj
   
   # Build sample apps (each takes 15-30 minutes on first build)
   dotnet build /p:Configuration=Release MauiCSharpInteropWebView/MauiCSharpInteropWebView.csproj
   dotnet build /p:Configuration=Release MauiReactJSHybridApp/MauiReactJSHybridApp.csproj
   ```

### KNOWN BUILD ISSUES TO EXPECT
- **MauiVersion Property Missing**: Sample projects reference `$(MauiVersion)` which is undefined. This may cause build failures.
- **SDK Version Mismatch**: HybridWebView targets .NET 9 but workflows use .NET 8 SDK.
- **Platform Limitations**: Android/iOS builds require platform-specific SDKs and emulators.

## Validation Scenarios

**ALWAYS manually test after making changes** - there are no automated tests.

### Test MauiCSharpInteropWebView Sample:
1. Run the app from Visual Studio (Windows target for easiest testing)
2. Navigate through all demo pages:
   - **Main page**: Verify page loads and navigation works
   - **Raw messages**: Type text, click "Send to C#", verify native alert appears
   - **Method invoke**: Test async JavaScript ↔ C# method calls
   - **Proxy URLs**: Test dynamic content loading via proxy requests
3. Verify all JavaScript ↔ C# communication works in both directions

### Test MauiReactJSHybridApp Sample:
1. Run the React Todo app
2. Test todo functionality:
   - Add new todos
   - Mark todos complete/incomplete  
   - Delete todos
   - Verify data persists between app restarts
3. Verify React app communicates with .NET backend for data persistence

## Project Structure

### Key Projects:
- **HybridWebView/**: Core cross-platform control (.NET 9 target)
- **MauiCSharpInteropWebView/**: Demo app with 4 test scenarios
- **MauiReactJSHybridApp/**: React integration example

### Important Locations:
- **Web assets**: `Resources/Raw/hybrid_root/` (C# interop) and `Resources/Raw/ReactTodoApp/` (React app)
- **Interop demos**: `MauiCSharpInteropWebView/Resources/Raw/hybrid_root/` contains HTML/JS examples
- **React source**: External repo at https://github.com/Eilon/todo-react

## Updating React App Content

**COMPLEX PROCESS** - Only do this if specifically required:

1. **Clone external React repo:**
   ```bash
   git clone https://github.com/Eilon/todo-react
   cd todo-react
   ```

2. **Build React app:**
   ```bash
   yarn install                    # 2-5 minutes
   set PUBLIC_URL=/               # Windows
   export PUBLIC_URL=/            # Mac/Linux
   npm run build                  # 5-15 minutes. NEVER CANCEL.
   ```

3. **Handle build errors (common):**
   ```bash
   # If you get: Error: error:0308010C:digital envelope routines::unsupported
   set NODE_OPTIONS=--openssl-legacy-provider    # Windows  
   export NODE_OPTIONS=--openssl-legacy-provider # Mac/Linux
   npm run build                  # Retry build
   ```

4. **Update MAUI app:**
   ```bash
   # Navigate to MAUI project React folder
   cd /path/to/MauiReactJSHybridApp/Resources/Raw/ReactTodoApp
   
   # CRITICAL: Delete ALL existing files first
   rm -rf *
   
   # Copy new build output
   cp -r /path/to/todo-react/build/* .
   ```

5. **Test updated app** using validation scenarios above

## Common Development Tasks

### Quick Build Verification:
```bash
# Fast syntax check (2-3 minutes)
dotnet build HybridWebView/HybridWebView.csproj --no-restore

# Sample app syntax check (5-10 minutes each)  
dotnet build MauiCSharpInteropWebView/MauiCSharpInteropWebView.csproj --no-restore
```

### Debugging Web Content:
- **Local files**: Edit `Resources/Raw/hybrid_root/*.html` directly
- **JavaScript debugging**: Use browser dev tools when running on Windows
- **C# debugging**: Use Visual Studio debugger on the .NET side

### Key Files to Monitor:
- **HybridWebView.cs**: Core control implementation
- **MainPage.xaml.cs**: Sample app event handlers  
- **hybrid_app.html**: Main demo page
- **HybridWebView.js**: JavaScript ↔ .NET bridge (embedded resource)

## Performance Expectations

**Set proper timeouts** for these operations:

| Operation | Expected Time | Timeout Setting |
|-----------|---------------|-----------------|
| MAUI workload install | 5-10 minutes | 15+ minutes |
| Initial restore | 10-20 minutes | 30+ minutes |
| First build (per project) | 15-30 minutes | 45+ minutes |
| Incremental builds | 2-5 minutes | 10+ minutes |
| React app build | 5-15 minutes | 30+ minutes |

**NEVER CANCEL** any of these operations - MAUI builds are inherently slow.

## Platform Support Notes

- **Windows**: Full support, easiest for development and testing
- **macOS**: Full support via Visual Studio for Mac
- **Android**: Requires Android SDK and emulator setup
- **iOS**: Requires Xcode and iOS simulator (macOS only)
- **Linux**: **NOT SUPPORTED** - builds will fail

## CI/CD Pipeline

The `.github/workflows/_build.yml` pipeline:
- Runs on Windows and macOS agents
- Uses .NET 8 SDK with MAUI workload
- Builds all projects and creates NuGet packages
- Takes 45+ minutes total - **NEVER CANCEL**

Always verify your changes pass this pipeline before merging.