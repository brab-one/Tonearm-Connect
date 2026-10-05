#!/usr/bin/env bash
# Builds the Tonearm Connect plugin against a Lidarr release and packs it for installation.
# Output: dist/Tonearm-Connect-v<version>.net8.0.zip. Lidarr's plugin installer picks the release asset
# whose name contains "net8.0.zip" and unpacks it into <Lidarr AppData>/plugins/brab-one/Tonearm-Connect/.
set -euo pipefail
cd "$(dirname "$0")"
LIDARR_VERSION="${LIDARR_VERSION:-3.1.6.5078}"
DOTNET="${DOTNET:-$(command -v dotnet || echo "$HOME/.dotnet/dotnet")}"

if [ ! -f .lidarr/Lidarr/Lidarr.Core.dll ]; then
  echo "Fetching Lidarr $LIDARR_VERSION to compile against…"
  mkdir -p .lidarr
  curl -fsSL "https://github.com/Lidarr/Lidarr/releases/download/v$LIDARR_VERSION/Lidarr.develop.$LIDARR_VERSION.linux-core-x64.tar.gz" | tar xz -C .lidarr
fi

"$DOTNET" build src/TonearmConnect.csproj -c Release -o out --nologo -v quiet
version=$(grep -oP '(?<=<Version>)[^<]+' src/TonearmConnect.csproj)
mkdir -p dist
rm -f dist/*.zip
cp plugin.json out/
(cd out && zip -q "../dist/Tonearm-Connect-v$version.net8.0.zip" Lidarr.Plugin.TonearmConnect.dll plugin.json)
echo "Built dist/Tonearm-Connect-v$version.net8.0.zip"
