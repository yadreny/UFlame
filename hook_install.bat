@echo off
setlocal

rem Find repository root (works from any subfolder)
for /f "delims=" %%R in ('git rev-parse --show-toplevel 2^>NUL') do set REPO=%%R
if "%REPO%"=="" (
  echo [ERROR] Not a Git repository. Run inside a repo.
  exit /b 1
)

rem Run the PowerShell installer with the repo root as working directory
pushd "%REPO%" || (
  echo [ERROR] Cannot cd to repo root: %REPO%
  exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Set-Location -LiteralPath '%REPO%'; & '%~dp0hook_install.ps1'"

set ERR=%ERRORLEVEL%
popd

if not "%ERR%"=="0" (
  echo [ERROR] hook_install.ps1 failed with code %ERR%
  exit /b %ERR%
)

rem Ensure this repo points to .githooks (relative to repo root)
git -C "%REPO%" config core.hooksPath .githooks

echo.
echo OK. Hooks installed to: "%REPO%\.githooks"
echo core.hooksPath set to ".githooks"
