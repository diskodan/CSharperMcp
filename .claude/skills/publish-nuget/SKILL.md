---
name: publish-nuget
description: Publish CSharperMcp package to NuGet.org with automatic version increment
disable-model-invocation: true
---

# Publish to NuGet Skill

This skill handles the complete process of building, testing, versioning, and publishing the CSharperMcp package to NuGet.org.

## What This Skill Does

1. Builds the project in Debug mode
2. Runs all tests to ensure everything passes
3. Queries NuGet.org for the current published version (unless version explicitly provided)
4. Automatically increments the patch version (unless version explicitly provided)
5. Builds the NuGet package in Release mode with the new version
6. Publishes to NuGet.org using the API key (or skips if "dry run" is specified)

## Parameters

- **Version number (optional)**: Specify an explicit version like `1.2.3` to skip auto-increment
- **"dry run" (optional)**: Skip the actual publish step, just build and test

## Prerequisites

The `NUGET_KEY` environment variable must be set with a valid NuGet.org API key.

## Instructions

When this skill is invoked:

### Parse Arguments

- Check if the user included "dry run" in their command
  - If "dry run" is detected, set a flag to skip the publish step at the end
  - Display "DRY RUN MODE - Will not actually publish" if in dry run mode

- Check if the user provided a specific version number (e.g., "1.2.3" or "0.1.5")
  - Look for a pattern matching semantic version format (X.Y.Z)
  - If a version is provided, skip Steps 2 and 3 (version query and increment)
  - If no version provided, proceed with automatic version increment

### Step 1: Verify Environment & Build/Test

Run the build and test script:
```bash
./.claude/skills/publish-nuget/scripts/build-and-test.sh
```

This will:
- Build the project in Debug mode
- Run all tests
- Abort if any tests fail

### Step 2: Query Current NuGet Version (Skip if version provided)

**Only run if user did NOT provide a specific version:**

Run the version query script:
```bash
CURRENT_VERSION=$(./.claude/skills/publish-nuget/scripts/get-current-version.sh)
```

This script:
- Uses the `$NUGET_KEY` to query NuGet.org API (including pending-release versions)
- Returns the highest version number found
- Falls back to "0.0.0" if no version exists

Display the current version to the user.

### Step 3: Increment Version (Skip if version provided)

**Only run if user did NOT provide a specific version:**

Run the version increment script:
```bash
NEW_VERSION=$(./.claude/skills/publish-nuget/scripts/increment-version.sh "$CURRENT_VERSION")
```

This increments the patch number (e.g., 0.1.3 → 0.1.4).

Display the new version to the user.

**If user provided a version:**
Set `NEW_VERSION` to the user-provided version and display it.

### Step 4: Build NuGet Package

Run the pack script:
```bash
NUPKG_FILE=$(./.claude/skills/publish-nuget/scripts/pack-release.sh "$NEW_VERSION")
```

This will:
- Build the package in Release mode
- Use `--version` flag to set the version without modifying the .csproj file
- Output the path to the created .nupkg file

### Step 5: Publish to NuGet.org (Skip if Dry Run)

**If NOT in dry run mode:**
Run the publish script:
```bash
./.claude/skills/publish-nuget/scripts/publish-package.sh "$NUPKG_FILE"
```

This will:
- Push the package to NuGet.org using `$NUGET_KEY`
- Verify the package file exists first

**If in dry run mode:**
- Display: "SKIPPING PUBLISH (dry run mode)"
- Show what would be published: the package path and version

### Step 6: Report Results

**If published successfully:**
Display:
- The new version number
- The NuGet package URL: `https://www.nuget.org/packages/CSharperMcp`
- Note that it may take a few minutes to appear on NuGet.org

**If dry run:**
Display:
- Summary: "Dry run complete - no package was published"
- Package that was built: `$NUPKG_FILE`
- Version that would be published: `$NEW_VERSION`
- Command to publish manually: `dotnet nuget push $NUPKG_FILE --api-key $NUGET_KEY --source https://api.nuget.org/v3/index.json`

## Safety Notes

- This skill should NEVER be automatically invoked
- It requires explicit user invocation with `/publish-nuget`
- Tests must pass before publishing
- The version increment is automatic (patch version only) unless explicitly provided
- The .csproj file is NOT modified (version passed via `--version` flag to `dotnet pack`)

## Example Output

**With auto-increment:**
```
=== Publishing CSharperMcp to NuGet.org ===

Step 1: Building and testing in Debug mode...
✓ Build and tests passed

Step 2: Querying current NuGet version...
Current version: 0.1.3

Step 3: Incrementing to new version...
New version: 0.1.4

Step 4: Building NuGet package in Release mode...
✓ Package built: ./artifacts/CSharperMcp.0.1.4.nupkg

Step 5: Publishing to NuGet.org...
✓ Published successfully

=== Publish Complete ===
Package: CSharperMcp v0.1.4
URL: https://www.nuget.org/packages/CSharperMcp

Note: It may take a few minutes for the package to appear on NuGet.org
```

**With explicit version:**
```
=== Publishing CSharperMcp to NuGet.org ===
Using specified version: 1.2.3

Step 1: Building and testing in Debug mode...
✓ Build and tests passed

Step 2: Building NuGet package in Release mode...
✓ Package built: ./artifacts/CSharperMcp.1.2.3.nupkg

Step 3: Publishing to NuGet.org...
✓ Published successfully

=== Publish Complete ===
Package: CSharperMcp v1.2.3
URL: https://www.nuget.org/packages/CSharperMcp

Note: It may take a few minutes for the package to appear on NuGet.org
```

## Usage

**Basic usage (auto-increment patch version):**
```
/publish-nuget
```
This will query NuGet.org for the current version and auto-increment the patch number.

**Specify a version explicitly:**
```
/publish-nuget 1.2.3
```
This skips the version query and increment steps, using the exact version you specify.

**Dry run mode (build and test without publishing):**
```
/publish-nuget dry run
```

**Dry run with specific version:**
```
/publish-nuget 1.2.3 dry run
```

In dry run mode, the skill will:
- Build and test the code
- Use the specified version (or query/increment if not provided)
- Build the .nupkg file
- Stop before publishing
- Show you the manual command to publish if you want to do it later
