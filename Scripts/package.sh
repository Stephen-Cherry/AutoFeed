#!/usr/bin/env bash
set -euo pipefail

# Read version from csproj
VERSION=$(grep -oP '(?<=<Version>)[^<]+' AutoFeed.csproj)
ZIP="AutoFeed-${VERSION}.zip"

echo "Building AutoFeed v${VERSION}..."
dotnet build AutoFeed.csproj -c Release

echo "Packaging..."
STAGING=$(mktemp -d)
cp manifest.json "$STAGING/"
cp icon.png "$STAGING/"
cp README.md "$STAGING/"
cp "bin/Release/net48/Narolith.AutoFeed.dll" "$STAGING/"

(cd "$STAGING" && zip -r "$OLDPWD/$ZIP" .)
rm -rf "$STAGING"

echo "Done: $ZIP"
