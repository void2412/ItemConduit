#!/bin/bash

# ItemConduit - Rebuild and Deploy Script
# Rebuilds the solution and deploys to CLIENT and SERVER locations

set -e  # Exit on error

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Script directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Default values
BUILD_CONFIG="Debug"
PROJECT_NAME="ItemConduit"
ENV_FILE="$PROJECT_ROOT/deploy.env"

# Function to print colored messages
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

print_header() {
    echo -e "\n${BLUE}========================================${NC}"
    echo -e "${BLUE}  $1${NC}"
    echo -e "${BLUE}========================================${NC}\n"
}

# Load environment file
load_env() {
    if [ -f "$ENV_FILE" ]; then
        print_info "Loading configuration from $ENV_FILE"
        # Export variables from deploy.env
        set -a
        source "$ENV_FILE"
        set +a

        # Convert Windows backslashes to forward slashes for cross-platform compatibility
        if [ -n "$CLIENT_PATH" ]; then
            CLIENT_PATH=$(echo "$CLIENT_PATH" | sed 's/\\/\//g')
        fi
        if [ -n "$SERVER_PATH" ]; then
            SERVER_PATH=$(echo "$SERVER_PATH" | sed 's/\\/\//g')
        fi
    else
        print_error "Configuration file not found: $ENV_FILE"
        print_info "Please copy deploy.env.example to deploy.env and configure it"
        exit 1
    fi
}

# Validate configuration
validate_config() {
    print_info "Validating configuration..."

    if [ -z "$CLIENT_PATH" ] && [ -z "$SERVER_PATH" ]; then
        print_error "At least one deployment path (CLIENT_PATH or SERVER_PATH) must be configured"
        exit 1
    fi

    if [ -n "$CLIENT_PATH" ] && [ ! -d "$CLIENT_PATH" ]; then
        print_warning "Client path does not exist: $CLIENT_PATH"
    fi

    if [ -n "$SERVER_PATH" ] && [ ! -d "$SERVER_PATH" ]; then
        print_warning "Server path does not exist: $SERVER_PATH"
    fi

    print_success "Configuration validated"
}

# Clean build artifacts
clean_build() {
    print_header "Cleaning Build Artifacts"

    cd "$PROJECT_ROOT/$PROJECT_NAME"

    if command -v dotnet &> /dev/null; then
        print_info "Running dotnet clean..."
        dotnet clean --configuration "$BUILD_CONFIG" --nologo
        print_success "Clean completed"
    else
        print_error "dotnet command not found. Please install .NET SDK"
        exit 1
    fi
}

# Rebuild solution
rebuild_solution() {
    print_header "Rebuilding Solution"

    cd "$PROJECT_ROOT/$PROJECT_NAME"

    print_info "Building $PROJECT_NAME in $BUILD_CONFIG mode..."
    dotnet build --configuration "$BUILD_CONFIG" --nologo --verbosity minimal

    if [ $? -eq 0 ]; then
        print_success "Build completed successfully"
    else
        print_error "Build failed"
        exit 1
    fi

    # Generate .mdb file for Debug builds
    if [ "$BUILD_CONFIG" = "Debug" ]; then
        generate_mdb_file
    fi
}

# Generate MDB file for Debug builds using pdb2mdb.exe
generate_mdb_file() {
    print_header "Generating Debug Symbols (MDB)"

    local BUILD_OUTPUT="$PROJECT_ROOT/$PROJECT_NAME/bin/$BUILD_CONFIG/net48"
    local DLL_FILE="$BUILD_OUTPUT/$PROJECT_NAME.dll"
    local PDB_FILE="$BUILD_OUTPUT/$PROJECT_NAME.pdb"
    local PDB2MDB="$PROJECT_ROOT/libraries/Debug/pdb2mdb.exe"

    # Check if pdb2mdb.exe exists
    if [ ! -f "$PDB2MDB" ]; then
        print_warning "pdb2mdb.exe not found at $PDB2MDB"
        print_warning "Skipping .mdb generation"
        return 0
    fi

    # Check if PDB file exists
    if [ ! -f "$PDB_FILE" ]; then
        print_warning "PDB file not found: $PDB_FILE"
        print_warning "Skipping .mdb generation"
        return 0
    fi

    print_info "Creating .mdb file for $PROJECT_NAME.dll..."

    # Run pdb2mdb.exe to generate .mdb file
    "$PDB2MDB" "$DLL_FILE"

    if [ $? -eq 0 ] && [ -f "$BUILD_OUTPUT/$PROJECT_NAME.dll.mdb" ]; then
        print_success "Generated $PROJECT_NAME.dll.mdb successfully"
    else
        print_warning "Failed to generate .mdb file (this is non-critical)"
    fi
}

# Deploy to a single location
deploy_to_location() {
    local LOCATION_NAME=$1
    local TARGET_PATH=$2

    if [ -z "$TARGET_PATH" ]; then
        print_info "Skipping $LOCATION_NAME deployment (path not configured)"
        return 0
    fi

    print_info "Deploying to $LOCATION_NAME: $TARGET_PATH"

    # Construct BepInEx plugins path
    local DEPLOY_PATH="$TARGET_PATH/BepInEx/plugins"
    local PLUGIN_FOLDER="$DEPLOY_PATH/$PROJECT_NAME"

    # Create deployment directory
    mkdir -p "$PLUGIN_FOLDER"

    # Source paths
    local BUILD_OUTPUT="$PROJECT_ROOT/$PROJECT_NAME/bin/$BUILD_CONFIG/net48"
    local DLL_FILE="$BUILD_OUTPUT/$PROJECT_NAME.dll"
    local PDB_FILE="$BUILD_OUTPUT/$PROJECT_NAME.pdb"
    local MDB_FILE="$BUILD_OUTPUT/$PROJECT_NAME.dll.mdb"

    # Check if DLL exists
    if [ ! -f "$DLL_FILE" ]; then
        print_error "Build output not found: $DLL_FILE"
        return 1
    fi

    # Copy DLL (always)
    print_info "Copying $PROJECT_NAME.dll..."
    cp "$DLL_FILE" "$PLUGIN_FOLDER/"

    # For Debug builds, copy debug symbols
    if [ "$BUILD_CONFIG" = "Debug" ]; then
        # Copy PDB if exists (Debug symbols for .NET debugging)
        if [ -f "$PDB_FILE" ]; then
            print_info "Copying $PROJECT_NAME.pdb (debug symbols)..."
            cp "$PDB_FILE" "$PLUGIN_FOLDER/"
        fi

        # Copy MDB if exists (Mono debug symbols for BepInEx)
        if [ -f "$MDB_FILE" ]; then
            print_info "Copying $PROJECT_NAME.dll.mdb (mono debug symbols)..."
            cp "$MDB_FILE" "$PLUGIN_FOLDER/"
        else
            print_warning "No .mdb file found - debugging in Valheim may be limited"
        fi
    else
        # Release build - clean up any old debug symbols
        print_info "Release build - removing debug symbols from deployment folder..."
        rm -f "$PLUGIN_FOLDER/$PROJECT_NAME.pdb"
        rm -f "$PLUGIN_FOLDER/$PROJECT_NAME.dll.mdb"
    fi

    print_success "$LOCATION_NAME deployment completed: $PLUGIN_FOLDER"
}

# Deploy to configured locations
deploy() {
    print_header "Deploying to Configured Locations"

    local DEPLOYED=0

    # Deploy to CLIENT
    if [ -n "$CLIENT_PATH" ]; then
        deploy_to_location "CLIENT" "$CLIENT_PATH"
        DEPLOYED=1
    fi

    # Deploy to SERVER
    if [ -n "$SERVER_PATH" ]; then
        deploy_to_location "SERVER" "$SERVER_PATH"
        DEPLOYED=1
    fi

    if [ $DEPLOYED -eq 0 ]; then
        print_warning "No deployments were performed"
    fi
}

# Show deployment summary
show_summary() {
    print_header "Deployment Summary"

    echo -e "${BLUE}Project:${NC} $PROJECT_NAME"
    echo -e "${BLUE}Build Config:${NC} $BUILD_CONFIG"

    if [ -n "$CLIENT_PATH" ]; then
        echo -e "${BLUE}Client:${NC} $CLIENT_PATH/BepInEx/plugins/$PROJECT_NAME"
    fi

    if [ -n "$SERVER_PATH" ]; then
        echo -e "${BLUE}Server:${NC} $SERVER_PATH/BepInEx/plugins/$PROJECT_NAME"
    fi

    echo ""
    print_success "All operations completed successfully!"
    echo -e "${GREEN}Your mod is ready to use.${NC}\n"
}

# Main execution
main() {
    print_header "ItemConduit - Rebuild and Deploy"

    # Parse command line arguments
    while [[ $# -gt 0 ]]; do
        case $1 in
            --config)
                BUILD_CONFIG="$2"
                shift 2
                ;;
            --env)
                ENV_FILE="$2"
                shift 2
                ;;
            --help)
                echo "Usage: $0 [OPTIONS]"
                echo ""
                echo "Options:"
                echo "  --config <Debug|Release>  Build configuration (default: Debug)"
                echo "  --env <path>              Path to environment file (default: deploy.env)"
                echo "  --help                    Show this help message"
                echo ""
                echo "Example:"
                echo "  $0 --config Release"
                exit 0
                ;;
            *)
                print_error "Unknown option: $1"
                echo "Use --help for usage information"
                exit 1
                ;;
        esac
    done

    # Execute deployment pipeline
    load_env
    validate_config
    clean_build
    rebuild_solution
    deploy
    show_summary
}

# Run main function
main "$@"
