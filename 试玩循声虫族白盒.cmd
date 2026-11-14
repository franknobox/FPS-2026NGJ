@echo off
cd /d "%~dp0"
if not exist "Builds\SlugWhitebox\SlugWhitebox.exe" (
  echo Build missing. Open Assets/SlugWhitebox/Scenes/SlugWhitebox.unity in Unity.
  pause
  exit /b 1
)
start "" "Builds\SlugWhitebox\SlugWhitebox.exe"
