#!/bin/bash
# Build the NuGet package in Release mode
# Usage: ./pack-release.sh <version>
# Example: ./pack-release.sh 0.1.4

set -e

VERSION="$1"
CSPROJ_PATH="src/CSharperMcp.Server/CSharperMcp.Server.csproj"
ARTIFACTS_DIR="./artifacts"

if [ -z "$VERSION" ]; then
    echo "Error: No version provided" >&2
    echo "Usage: $0 <version>" >&2
    exit 1
fi

echo "Building NuGet package version $VERSION in Release mode..."
dotnet pack "$CSPROJ_PATH" -c Release -o "$ARTIFACTS_DIR" --version "$VERSION"

NUPKG_FILE="${ARTIFACTS_DIR}/CSharperMcp.${VERSION}.nupkg"

if [ ! -f "$NUPKG_FILE" ]; then
    echo "Error: Expected package file not found at $NUPKG_FILE" >&2
    exit 1
fi

echo "✓ Package built: $NUPKG_FILE"
echo "$NUPKG_FILE"
