@echo off
"%~dp0.venv-ai\Scripts\python.exe" "%~dp0run.py" %*
exit /b %errorlevel%
