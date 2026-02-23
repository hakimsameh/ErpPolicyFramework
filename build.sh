#!/usr/bin/env bash
# =============================================================================
# build.sh — Build, test, and run the ERP Policy Framework
# Usage: chmod +x build.sh && ./build.sh
# Requires: .NET 8 SDK (https://dotnet.microsoft.com/download)
# =============================================================================

set -e

SOLUTION="ErpPolicyFramework.sln"
HOST_PROJECT="src/PolicyFramework.Host/PolicyFramework.Host.csproj"

echo "══════════════════════════════════════════════════════════"
echo "  ERP Policy Framework — Build & Test Runner"
echo "══════════════════════════════════════════════════════════"

echo ""
echo "▶  Restoring NuGet packages..."
dotnet restore "$SOLUTION"

echo ""
echo "▶  Building solution (Release)..."
dotnet build "$SOLUTION" --configuration Release --no-restore

echo ""
echo "▶  Running unit and integration tests..."
dotnet test "$SOLUTION" \
    --configuration Release \
    --no-build \
    --verbosity normal \
    --logger "console;verbosity=detailed"

echo ""
echo "▶  Running demo host application..."
dotnet run --project "$HOST_PROJECT" --configuration Release --no-build

echo ""
echo "══════════════════════════════════════════════════════════"
echo "  ✓  Build complete."
echo "══════════════════════════════════════════════════════════"
