#!/usr/bin/env bash
# もざや - 一括ビルド（Web のテスト＋Windows 版のビルド）
set -euo pipefail
cd "$(dirname "$0")"

echo "===== Web テスト ====="
npm test

echo "===== Windows 版ビルド ====="
./windows/build.sh

echo "===== 完了 ====="
echo "成果物: $(pwd)/windows/dist/Mozaya.exe"
