# ItemConduit - Rebuild and Deploy Script (PowerShell)
# Rebuilds the solution and deploys to CLIENT and SERVER locations

param(
    [ValidateSet('Debug', 'Release')]
    [string]$BuildConfig = 'Debug',

    [string]$EnvFile = '',

    [switch]$Help
)

# Error handling
$ErrorActionPreference = 'Stop'

# Script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

# Default values
$ProjectName = 'ItemConduit'

# Color functions
function Write-Info {
    param([string]$Message)
    Write-Host "[INFO] $Message" -ForegroundColor Blue
}

function Write-Success {
    param([string]$Message)
    Write-Host "[SUCCESS] $Message" -ForegroundColor Green
}

function Write-Warning {
    param([string]$Message)
    Write-Host "[WARNING] $Message" -ForegroundColor Yellow
}

function Write-Error {
    param([string]$Message)
    Write-Host "[ERROR] $Message" -ForegroundColor Red
}

function Write-Header {
    param([string]$Message)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Blue
    Write-Host "  $Message" -ForegroundColor Blue
    Write-Host "========================================" -ForegroundColor Blue
    Write-Host ""
}

# Show help
if ($Help) {
    Write-Host "ItemConduit - Rebuild and Deploy"
    Write-Host ""
    Write-Host "Usage: .\rebuild-and-deploy.ps1 [OPTIONS]"
    Write-Host ""
    Write-Host "Options:"
    Write-Host "  -BuildConfig <Debug|Release>  Build configuration (default: Debug)"
    Write-Host "  -EnvFile <path>               Path to environment file (default: deploy.env)"
    Write-Host "  -Help                         Show this help message"
    Write-Host ""
    Write-Host "Example:"
    Write-Host "  .\rebuild-and-deploy.ps1 -BuildConfig Release"
    exit 0
}

# Load environment file
function Load-Environment {
    if (-not $EnvFile) {
        $EnvFile = Join-Path $ProjectRoot 'deploy.env'
    }

    if (-not (Test-Path $EnvFile)) {
        Write-Error "Configuration file not found: $EnvFile"
        Write-Info "Please copy deploy.env.example to deploy.env and configure it"
        exit 1
    }

    Write-Info "Loading configuration from $EnvFile"

    # Parse environment file
    $envContent = Get-Content $EnvFile
    foreach ($line in $envContent) {
        # Skip comments and empty lines
        if ($line -match '^\s*#' -or $line -match '^\s*$') {
            continue
        }

        # Parse KEY="VALUE" or KEY=VALUE format
        if ($line -match '^([^=]+)=(.*)$') {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim().Trim('"').Trim("'")
            Set-Variable -Name $key -Value $value -Scope Script
        }
    }

    # Override BUILD_CONFIG if specified via parameter
    if ($BuildConfig) {
        $script:BUILD_CONFIG = $BuildConfig
    }
}

# Validate configuration
function Test-Configuration {
    Write-Info "Validating configuration..."

    if (-not $script:CLIENT_PATH -and -not $script:SERVER_PATH) {
        Write-Error "At least one deployment path (CLIENT_PATH or SERVER_PATH) must be configured"
        exit 1
    }

    if ($script:CLIENT_PATH -and -not (Test-Path $script:CLIENT_PATH)) {
        Write-Warning "Client path does not exist: $($script:CLIENT_PATH)"
    }

    if ($script:SERVER_PATH -and -not (Test-Path $script:SERVER_PATH)) {
        Write-Warning "Server path does not exist: $($script:SERVER_PATH)"
    }

    Write-Success "Configuration validated"
}

# Clean build artifacts
function Clear-BuildArtifacts {
    Write-Header "Cleaning Build Artifacts"

    Push-Location (Join-Path $ProjectRoot $ProjectName)

    try {
        if (Get-Command dotnet -ErrorAction SilentlyContinue) {
            Write-Info "Running dotnet clean..."
            dotnet clean --configuration $script:BUILD_CONFIG --nologo
            Write-Success "Clean completed"
        }
        else {
            Write-Error "dotnet command not found. Please install .NET SDK"
            exit 1
        }
    }
    finally {
        Pop-Location
    }
}

# Rebuild solution
function Build-Solution {
    Write-Header "Rebuilding Solution"

    Push-Location (Join-Path $ProjectRoot $ProjectName)

    try {
        Write-Info "Building $ProjectName in $($script:BUILD_CONFIG) mode..."
        dotnet build --configuration $script:BUILD_CONFIG --nologo --verbosity minimal

        if ($LASTEXITCODE -eq 0) {
            Write-Success "Build completed successfully"
        }
        else {
            Write-Error "Build failed"
            exit 1
        }
    }
    finally {
        Pop-Location
    }
}

# Deploy to a single location
function Deploy-ToLocation {
    param(
        [string]$LocationName,
        [string]$TargetPath
    )

    if (-not $TargetPath) {
        Write-Info "Skipping $LocationName deployment (path not configured)"
        return
    }

    Write-Info "Deploying to $LocationName : $TargetPath"

    # Construct BepInEx plugins path
    $DeployPath = Join-Path $TargetPath 'BepInEx\plugins'
    $PluginFolder = Join-Path $DeployPath $ProjectName

    # Create deployment directory
    New-Item -ItemType Directory -Path $PluginFolder -Force | Out-Null

    # Source paths
    $BuildOutput = Join-Path $ProjectRoot "$ProjectName\bin\$($script:BUILD_CONFIG)\net48"
    $DllFile = Join-Path $BuildOutput "$ProjectName.dll"
    $PdbFile = Join-Path $BuildOutput "$ProjectName.pdb"
    $MdbFile = Join-Path $BuildOutput "$ProjectName.dll.mdb"

    # Check if DLL exists
    if (-not (Test-Path $DllFile)) {
        Write-Error "Build output not found: $DllFile"
        return
    }

    # Copy DLL
    Write-Info "Copying $ProjectName.dll..."
    Copy-Item $DllFile -Destination $PluginFolder -Force

    # Copy PDB if exists (Debug symbols)
    if (Test-Path $PdbFile) {
        Write-Info "Copying $ProjectName.pdb (debug symbols)..."
        Copy-Item $PdbFile -Destination $PluginFolder -Force
    }

    # Copy MDB if exists (Mono debug symbols)
    if (Test-Path $MdbFile) {
        Write-Info "Copying $ProjectName.dll.mdb (mono debug symbols)..."
        Copy-Item $MdbFile -Destination $PluginFolder -Force
    }

    Write-Success "$LocationName deployment completed: $PluginFolder"
}

# Deploy to configured locations
function Deploy-Mod {
    Write-Header "Deploying to Configured Locations"

    $Deployed = $false

    # Deploy to CLIENT
    if ($script:CLIENT_PATH) {
        Deploy-ToLocation -LocationName 'CLIENT' -TargetPath $script:CLIENT_PATH
        $Deployed = $true
    }

    # Deploy to SERVER
    if ($script:SERVER_PATH) {
        Deploy-ToLocation -LocationName 'SERVER' -TargetPath $script:SERVER_PATH
        $Deployed = $true
    }

    if (-not $Deployed) {
        Write-Warning "No deployments were performed"
    }
}

# Show deployment summary
function Show-Summary {
    Write-Header "Deployment Summary"

    Write-Host "Project: " -NoNewline
    Write-Host $ProjectName -ForegroundColor Cyan

    Write-Host "Build Config: " -NoNewline
    Write-Host $script:BUILD_CONFIG -ForegroundColor Cyan

    if ($script:CLIENT_PATH) {
        Write-Host "Client: " -NoNewline
        Write-Host "$($script:CLIENT_PATH)\BepInEx\plugins\$ProjectName" -ForegroundColor Cyan
    }

    if ($script:SERVER_PATH) {
        Write-Host "Server: " -NoNewline
        Write-Host "$($script:SERVER_PATH)\BepInEx\plugins\$ProjectName" -ForegroundColor Cyan
    }

    Write-Host ""
    Write-Success "All operations completed successfully!"
    Write-Host "Your mod is ready to use." -ForegroundColor Green
    Write-Host ""
}

# Main execution
function Main {
    Write-Header "ItemConduit - Rebuild and Deploy"

    try {
        Load-Environment
        Test-Configuration
        Clear-BuildArtifacts
        Build-Solution
        Deploy-Mod
        Show-Summary
    }
    catch {
        Write-Error "Deployment failed: $_"
        exit 1
    }
}

# Run main function
Main
