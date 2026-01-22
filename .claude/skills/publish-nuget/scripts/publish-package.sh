#!/bin/bash
# Publish the NuGet package to NuGet.org
# Usage: ./publish-package.sh <nupkg-file-path>
# Example: ./publish-package.sh ./artifacts/CSharperMcp.0.1.4.nupkg

set -e

NUPKG_FILE="$1"

if [ -z "$NUPKG_FILE" ]; then
    echo "Error: No package file provided" >&2
    echo "Usage: $0 <nupkg-file-path>" >&2
    exit 1
fi

if [ -z "$NUGET_KEY" ]; then
    echo "Error: NUGET_KEY environment variable not set" >&2
    exit 1
fi

if [ ! -f "$NUPKG_FILE" ]; then
    echo "Error: Package file not found at $NUPKG_FILE" >&2
    exit 1
fi

echo "Publishing $NUPKG_FILE to NuGet.org..."
dotnet nuget push "$NUPKG_FILE" \
  --api-key "$NUGET_KEY" \
  --source https://api.nuget.org/v3/index.json

echo "✓ Package published successfully"
