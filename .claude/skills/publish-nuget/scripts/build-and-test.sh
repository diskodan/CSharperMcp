#!/bin/bash
# Build and test in Debug mode
# Exits with non-zero code if build or tests fail

set -e

echo "Building in Debug mode..."
dotnet build -c Debug

echo "Running tests..."
dotnet test -c Debug --no-build

echo "✓ Build and tests passed"
