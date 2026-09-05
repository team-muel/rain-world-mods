@echo off
setlocal
set PY=C:\Users\Ueueh\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe
if not exist "%PY%" (
  echo Python was not found at:
  echo %PY%
  echo.
  echo Install Python or run rw_map_helper.py with another Python executable.
  pause
  exit /b 1
)
"%PY%" "%~dp0rw_map_helper.py" install
pause
