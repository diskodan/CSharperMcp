#!/bin/bash
# Get the current published version from NuGet.org
# Uses API key to detect pending-release versions
# Outputs the version number to stdout

set -e

PACKAGE_ID="CSharperMcp"
PACKAGE_ID_LOWER=$(echo "$PACKAGE_ID" | tr '[:upper:]' '[:lower:]')

# Check for NUGET_KEY
if [ -z "$NUGET_KEY" ]; then
    echo "Error: NUGET_KEY environment variable not set" >&2
    exit 1
fi

# Try the authenticated API first to get pending releases
# This uses the package metadata service which shows validation status
AUTH_RESPONSE=$(curl -s -H "X-NuGet-ApiKey: $NUGET_KEY" \
    "https://api.nuget.org/v3/registration5-semver1/${PACKAGE_ID_LOWER}/index.json" 2>/dev/null || echo "")

if [ -n "$AUTH_RESPONSE" ]; then
    # Extract all versions from the catalogEntry items
    VERSION=$(echo "$AUTH_RESPONSE" | grep -o '"version":"[^"]*"' | grep -o '[0-9]\+\.[0-9]\+\.[0-9]\+[^"]*' | sort -V | tail -1)
fi

# Fallback to public API if authenticated request didn't work
if [ -z "$VERSION" ]; then
    PUBLIC_RESPONSE=$(curl -s "https://api.nuget.org/v3-flatcontainer/${PACKAGE_ID_LOWER}/index.json")
    VERSION=$(echo "$PUBLIC_RESPONSE" | grep -o '"[0-9]\+\.[0-9]\+\.[0-9]\+"' | tr -d '"' | sort -V | tail -1)
fi

# If still no version found, use 0.0.0
if [ -z "$VERSION" ]; then
    VERSION="0.0.0"
fi

echo "$VERSION"
