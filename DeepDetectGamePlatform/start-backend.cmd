@echo off
setlocal
cd /d "%~dp0"

if not exist ".env" (
  echo Missing .env. Create DeepDetectGamePlatform\.env with OPENAI_API_KEY first.
  exit /b 1
)

set "PYTHON="
if exist ".venv\Scripts\python.exe" set "PYTHON=%CD%\.venv\Scripts\python.exe"
if not defined PYTHON if exist "..\Tools\Python311\python.exe" set "PYTHON=%CD%\..\Tools\Python311\python.exe"
if not defined PYTHON for %%P in (python.exe) do if not "%%~$PATH:P"=="" set "PYTHON=%%~$PATH:P"
if not defined PYTHON for %%P in (py.exe) do if not "%%~$PATH:P"=="" set "PYTHON=%%~$PATH:P"
if not defined PYTHON (
  echo Python was not found on PATH.
  echo Run setup-backend.cmd after installing Python 3.11+.
  exit /b 1
)

"%PYTHON%" -m uvicorn backend.app.main:app --host 127.0.0.1 --port 8765
