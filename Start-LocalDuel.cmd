@echo off
cd /d "%~dp0"
if not exist "%~dp0.venv\Scripts\python.exe" (
  echo Python environment is missing in this checkout.
  echo Follow the First-time setup instructions in README.md.
  pause
  exit /b 1
)
if not exist "%~dp0build\client\HearthstoneClone.exe" (
  echo Client build is missing in this checkout.
  echo Prepare local assets and build the client as described in docs\BUILD.md.
  pause
  exit /b 1
)
if not exist "%~dp0local\runtime\registry.json" (
  echo Local asset registry is missing in this checkout.
  echo Prepare the game assets as described in docs\BUILD.md.
  pause
  exit /b 1
)
"%~dp0.venv\Scripts\python.exe" -X utf8 -m tools.launch_duel %*
if errorlevel 1 pause
