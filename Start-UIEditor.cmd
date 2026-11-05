@echo off
cd /d "%~dp0"
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
