@echo off
cd /d "%~dp0"
"%~dp0.venv\Scripts\python.exe" -X utf8 -m tools.launch_duel %*
if errorlevel 1 pause
