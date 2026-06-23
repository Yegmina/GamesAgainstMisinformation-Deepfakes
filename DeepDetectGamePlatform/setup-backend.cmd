@echo off
setlocal
cd /d "%~dp0"

set "PYTHON="
if exist "..\Tools\Python311\python.exe" set "PYTHON=%CD%\..\Tools\Python311\python.exe"
for %%P in (python.exe) do if not "%%~$PATH:P"=="" set "PYTHON=%%~$PATH:P"
if not defined PYTHON for %%P in (py.exe) do if not "%%~$PATH:P"=="" set "PYTHON=%%~$PATH:P"
if not defined PYTHON (
  echo Python was not found on PATH.
  echo Install Python 3.11+ and rerun this script.
  exit /b 1
)

"%PYTHON%" -m venv .venv
if errorlevel 1 exit /b 1

".venv\Scripts\python.exe" -m pip install --upgrade pip
if errorlevel 1 exit /b 1

".venv\Scripts\python.exe" -m pip install -r requirements.txt
if errorlevel 1 exit /b 1

echo Backend environment is ready.
