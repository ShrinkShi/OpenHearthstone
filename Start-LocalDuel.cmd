@echo off
cd /d "%~dp0"
if not exist "%~dp0local\ui-calibration.json" if exist "%~dp0assets\ui-calibration.json" (
  if not exist "%~dp0local" mkdir "%~dp0local"
  copy /Y "%~dp0assets\ui-calibration.json" "%~dp0local\ui-calibration.json" >nul
  if errorlevel 1 exit /b 1
)
if not exist "%~dp0local\card-database.json" if exist "%~dp0assets\card-database.json" (
  if not exist "%~dp0local" mkdir "%~dp0local"
  copy /Y "%~dp0assets\card-database.json" "%~dp0local\card-database.json" >nul
  if errorlevel 1 exit /b 1
)
if not exist "%~dp0local\runtime" if exist "%~dp0assets\runtime" (
  if not exist "%~dp0local" mkdir "%~dp0local"
  mklink /J "%~dp0local\runtime" "%~dp0assets\runtime" >nul
  if errorlevel 1 (
    echo Could not connect the local runtime asset directory.
    pause
    exit /b 1
  )
)
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
