# ItemConduit Deployment Guide

This guide explains how to build and deploy the ItemConduit mod to your Valheim client and/or dedicated server.

## Quick Start

### 1. First Time Setup

Copy the example environment file and configure your paths:

```bash
cp deploy.env.example deploy.env
```

Edit `deploy.env` with your actual paths:

```bash
# Example for Windows (using Git Bash or WSL)
CLIENT_PATH="C:/Program Files (x86)/Steam/steamapps/common/Valheim"
SERVER_PATH="C:/ValheimServer"
BUILD_CONFIG="Debug"
```

```bash
# Example for Linux
CLIENT_PATH="/home/user/.local/share/Steam/steamapps/common/Valheim"
SERVER_PATH="/home/user/valheim-server"
BUILD_CONFIG="Debug"
```

### 2. Build and Deploy

Run the deployment script:

```bash
./scripts/rebuild-and-deploy.sh
```

**That's it!** The script will:
1. ✅ Clean previous build artifacts
2. ✅ Rebuild the solution
3. ✅ Deploy to CLIENT (if configured)
4. ✅ Deploy to SERVER (if configured)

---

## Configuration Reference

### Environment Variables

Edit `deploy.env` to configure these variables:

| Variable | Required | Description | Example |
|----------|----------|-------------|---------|
| `CLIENT_PATH` | Conditional* | Valheim client installation directory | `C:/Program Files (x86)/Steam/steamapps/common/Valheim` |
| `SERVER_PATH` | Conditional* | Valheim dedicated server directory | `C:/ValheimServer` |
| `BUILD_CONFIG` | No | Build configuration (`Debug` or `Release`) | `Debug` (default) |
| `PROJECT_NAME` | No | Project name (rarely needs changing) | `ItemConduit` (default) |

\* At least one path (CLIENT_PATH or SERVER_PATH) must be configured

### Deployment Paths

The mod will be deployed to:
- **Client**: `{CLIENT_PATH}/BepInEx/plugins/ItemConduit/`
- **Server**: `{SERVER_PATH}/BepInEx/plugins/ItemConduit/`

---

## Script Options

### Basic Usage

```bash
./scripts/rebuild-and-deploy.sh [OPTIONS]
```

### Available Options

| Option | Description | Default |
|--------|-------------|---------|
| `--config <Debug\|Release>` | Override build configuration | From `deploy.env` |
| `--env <path>` | Use custom environment file | `deploy.env` |
| `--help` | Show help message | - |

### Examples

**Build in Release mode:**
```bash
./scripts/rebuild-and-deploy.sh --config Release
```

**Use custom environment file:**
```bash
./scripts/rebuild-and-deploy.sh --env deploy.env.production
```

**Build Release with custom env:**
```bash
./scripts/rebuild-and-deploy.sh --config Release --env deploy.env.production
```

---

## Deployment Scenarios

### Scenario 1: Client Only Development

Configure only CLIENT_PATH in `deploy.env`:

```bash
CLIENT_PATH="C:/Program Files (x86)/Steam/steamapps/common/Valheim"
SERVER_PATH=""  # Leave empty
```

### Scenario 2: Dedicated Server Only

Configure only SERVER_PATH:

```bash
CLIENT_PATH=""  # Leave empty
SERVER_PATH="/home/user/valheim-server"
```

### Scenario 3: Client + Server (Recommended for Testing)

Configure both paths to test multiplayer scenarios:

```bash
CLIENT_PATH="C:/Program Files (x86)/Steam/steamapps/common/Valheim"
SERVER_PATH="C:/ValheimServer"
```

### Scenario 4: Multiple Environments

Create multiple environment files:

```bash
# Local development
deploy.env

# Production server
deploy.env.production

# Testing environment
deploy.env.testing
```

Use with `--env` flag:
```bash
./scripts/rebuild-and-deploy.sh --env deploy.env.production
```

---

## Troubleshooting

### Script fails with "Configuration file not found"

**Problem:** `deploy.env` file doesn't exist

**Solution:**
```bash
cp deploy.env.example deploy.env
# Edit deploy.env with your paths
```

### Build fails with "dotnet command not found"

**Problem:** .NET SDK is not installed or not in PATH

**Solution:**
- Install [.NET SDK 8.0+](https://dotnet.microsoft.com/download)
- Ensure `dotnet` is in your PATH

### Deployment path warnings

**Problem:** Script shows warnings about paths not existing

**Possible causes:**
1. Valheim/Server not installed at specified path
2. BepInEx not installed
3. Path syntax error (use forward slashes `/` even on Windows)

**Solution:**
- Verify paths exist
- Install BepInEx if needed
- Check path syntax in `deploy.env`

### Permission errors on Linux/Mac

**Problem:** "Permission denied" when running script

**Solution:**
```bash
chmod +x scripts/rebuild-and-deploy.sh
```

### DLL not found after deployment

**Problem:** Build succeeds but DLL isn't in plugins folder

**Check:**
1. Build output: `ItemConduit/bin/{Debug|Release}/net48/ItemConduit.dll`
2. Target path: `{CLIENT_PATH}/BepInEx/plugins/ItemConduit/ItemConduit.dll`
3. Script output for errors

---

## Build Configurations

### Debug Configuration

**Use for:** Development and testing

**Features:**
- ✅ Debug symbols included (.pdb, .dll.mdb)
- ✅ Easier debugging in IDE
- ✅ Faster builds
- ⚠️ Larger file size
- ⚠️ Slower runtime performance

```bash
BUILD_CONFIG="Debug"
```

### Release Configuration

**Use for:** Production deployment, distribution

**Features:**
- ✅ Optimized code
- ✅ Smaller file size
- ✅ Better runtime performance
- ❌ No debug symbols
- ❌ Harder to debug

```bash
BUILD_CONFIG="Release"
```

---

## Advanced Usage

### Custom Pre-Deploy Actions

Edit the script to add custom actions before deployment:

```bash
# Add after validate_config() function
pre_deploy_actions() {
    print_info "Running custom pre-deploy actions..."
    # Your custom logic here
}
```

### Post-Deploy Server Restart

Automatically restart server after deployment:

```bash
# Add to deploy() function after SERVER deployment
if [ -n "$SERVER_PATH" ]; then
    print_info "Restarting Valheim server..."
    systemctl restart valheim-server  # Adjust for your setup
fi
```

### Backup Before Deploy

Create backup of current mod before deploying:

```bash
# In deploy_to_location() before copying files
if [ -f "$PLUGIN_FOLDER/$PROJECT_NAME.dll" ]; then
    BACKUP_DIR="$PLUGIN_FOLDER/backup_$(date +%Y%m%d_%H%M%S)"
    mkdir -p "$BACKUP_DIR"
    cp "$PLUGIN_FOLDER"/*.dll "$BACKUP_DIR/"
fi
```

---

## Integration with CI/CD

### GitHub Actions Example

```yaml
name: Build and Deploy

on:
  push:
    branches: [ main ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Configure deployment
        run: |
          echo 'SERVER_PATH="${{ secrets.SERVER_PATH }}"' > deploy.env
          echo 'BUILD_CONFIG="Release"' >> deploy.env

      - name: Build and Deploy
        run: ./scripts/rebuild-and-deploy.sh
```

---

## File Structure

After deployment, your directory structure will look like:

```
Valheim/
└── BepInEx/
    └── plugins/
        └── ItemConduit/
            ├── ItemConduit.dll          # Main mod DLL
            ├── ItemConduit.pdb          # Debug symbols (Debug only)
            └── ItemConduit.dll.mdb      # Mono debug symbols (Debug only)
```

---

## See Also

- [README.md](README.md) - Main project documentation
- [Jötunn Documentation](https://valheim-modding.github.io/Jotunn/)
- [BepInEx Documentation](https://docs.bepinex.dev/)

---

## Support

For issues or questions:
1. Check this guide's troubleshooting section
2. Review script output for error messages
3. Check file permissions and paths
4. Ensure BepInEx is properly installed
