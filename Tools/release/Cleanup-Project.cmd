@echo off
rem Double-click to clean the project (old releases, logs, unused environments and caches). Close Unity, the game and Claude first.
echo GripRehab cleanup: close Unity, the game and Claude before continuing.
pause
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Cleanup-Project.ps1"
echo.
echo Done. The project size before and after is shown above.
pause
