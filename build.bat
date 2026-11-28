@echo off
rem もざや - 一括ビルド（Web のテスト＋Windows 版のビルド）
setlocal
cd /d %~dp0

echo ===== Web テスト =====
call npm test
if errorlevel 1 (
  echo Web テストに失敗しました。
  exit /b 1
)

echo ===== Windows 版ビルド =====
call windows\build.bat
if errorlevel 1 (
  echo Windows 版のビルドに失敗しました。
  exit /b 1
)

echo ===== 完了 =====
echo 成果物: %CD%\windows\dist\Mozaya.exe
endlocal
