@echo off
setlocal
title Admin.NET - Stop All Services

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop-all.ps1"
set "EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%EXIT_CODE%"=="0" (
  echo Some services could not be stopped.
) else (
  echo All Admin.NET services have been stopped.
)
echo Press any key to close this window.
pause >nul
exit /b %EXIT_CODE%
