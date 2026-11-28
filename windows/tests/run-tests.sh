#!/usr/bin/env bash
# もざや for Windows - ロジックテスト（Linux / この開発環境用）
# 純粋ロジック（MozayaLogic）だけを対象にするため、Windows でなくても実行できる。
set -euo pipefail
cd "$(dirname "$0")"

DOTNET="${DOTNET:-$HOME/workspace/tools/dotnet/dotnet}"
export TMPDIR="${TMPDIR:-$HOME/workspace/.tmp-dotnet}"
mkdir -p "$TMPDIR"
export TAR_OPTIONS=--no-same-owner

"$DOTNET" run --project MozayaLogicTests.csproj -c Release
