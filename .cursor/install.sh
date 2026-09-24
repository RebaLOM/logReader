#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap for the logReader .NET 10 solution.
# Installs the pinned .NET SDK (if missing), makes `dotnet` available on PATH,
# then restores and builds the cross-platform projects (core library + tests).
#
# Note: logReader.UI targets net10.0-windows (WinForms) and only runs on
# Windows. It is intentionally excluded from the default Linux build/test flow.
set -euo pipefail

DOTNET_INSTALL_DIR="${DOTNET_INSTALL_DIR:-$HOME/.dotnet}"
DOTNET_CHANNEL="10.0"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

# Install the .NET SDK only when the pinned major version is not already present.
if ! "$DOTNET_INSTALL_DIR/dotnet" --list-sdks 2>/dev/null | grep -q '^10\.'; then
  echo "Installing .NET SDK (channel $DOTNET_CHANNEL) into $DOTNET_INSTALL_DIR ..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel "$DOTNET_CHANNEL" --install-dir "$DOTNET_INSTALL_DIR"
else
  echo ".NET 10 SDK already present in $DOTNET_INSTALL_DIR"
fi

export PATH="$DOTNET_INSTALL_DIR:$PATH"

# Make `dotnet` discoverable from every shell/agent (persists into snapshots).
if [ ! -e /usr/local/bin/dotnet ] && command -v sudo >/dev/null 2>&1; then
  sudo ln -sf "$DOTNET_INSTALL_DIR/dotnet" /usr/local/bin/dotnet || true
fi

dotnet --info | head -n 12

# Restore and build the cross-platform projects.
dotnet restore logReader/logReader.csproj
dotnet restore logReader.Tests/logReader.Tests.csproj
dotnet build logReader.Tests/logReader.Tests.csproj -c Debug

echo "logReader environment bootstrap complete."
