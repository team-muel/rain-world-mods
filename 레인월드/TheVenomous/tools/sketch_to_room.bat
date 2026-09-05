@echo off
setlocal
set PY=C:\Users\Ueueh\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe
if not exist "%PY%" (
  echo Python was not found at:
  echo %PY%
  pause
  exit /b 1
)
if "%~1"=="" (
  echo Usage:
  echo   sketch_to_room.bat image.png FG_A04
  echo.
  echo Colors:
  echo   black line   = ground or wall
  echo   blue         = water
  echo   red          = threat
  echo   orange circle= exit
  echo   green circle = entrance
  pause
  exit /b 1
)
if "%~2"=="" (
  echo Missing room name. Example: FG_A04
  pause
  exit /b 1
)
"%PY%" "%~dp0sketch_to_room.py" "%~1" "%~2"
pause
