@echo off
cd /d "%~dp0"
if not exist "%~dp0build\client\HearthstoneClone.exe" (
  echo Client build is missing. See README.md for build instructions.
  pause
  exit /b 1
)
start "" "%~dp0build\client\HearthstoneClone.exe" --ui-editor --profile ui-editor --name UIEditor -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 %*
