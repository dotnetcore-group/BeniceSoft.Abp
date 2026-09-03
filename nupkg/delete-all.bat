@echo off
cd /d "%~dp0"

echo ============ Delete BeniceSoft packages from Nexus ============
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0delete-all.ps1"
set err=%errorlevel%

echo.
if %err% neq 0 (
    echo ============ Some deletes failed, see output above ============
) else (
    echo ============ All done. Run pack.bat to upload again ============
)
echo.
pause
exit /b %err%
