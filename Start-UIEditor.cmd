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
if not exist "%~dp0build\client\HearthstoneClone.exe" (
  echo Client build is missing in this checkout. See docs\BUILD.md.
  pause
  exit /b 1
)
if not exist "%~dp0local\runtime\registry.json" (
  echo Local asset registry is missing in this checkout. See docs\BUILD.md.
  pause
  exit /b 1
)
start "" "%~dp0build\client\HearthstoneClone.exe" --ui-editor --profile ui-editor --name UIEditor -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 %*
