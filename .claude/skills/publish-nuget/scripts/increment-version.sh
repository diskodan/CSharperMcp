#!/bin/bash
# Increment the patch version
# Usage: ./increment-version.sh <current-version>
# Example: ./increment-version.sh 0.1.3
# Output: 0.1.4

set -e

CURRENT_VERSION="$1"

if [ -z "$CURRENT_VERSION" ]; then
    echo "Error: No version provided" >&2
    echo "Usage: $0 <version>" >&2
    exit 1
fi

# Parse version components
IFS='.' read -r -a VERSION_PARTS <<< "$CURRENT_VERSION"
MAJOR="${VERSION_PARTS[0]}"
MINOR="${VERSION_PARTS[1]}"
PATCH="${VERSION_PARTS[2]}"

# Increment patch
NEW_PATCH=$((PATCH + 1))
NEW_VERSION="${MAJOR}.${MINOR}.${NEW_PATCH}"

echo "$NEW_VERSION"
