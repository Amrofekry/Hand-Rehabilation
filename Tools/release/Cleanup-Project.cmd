@echo off
rem Double-click to clean the project (old releases, logs, unused environments and caches). Close Unity and the game first.
rem Do not click inside the window while it runs: Windows then pauses it (press Esc to continue).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Cleanup-Project.ps1"
echo.
echo Done. Press any key to close this window.
pause >nul
