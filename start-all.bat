@echo off
setlocal EnableExtensions
title Admin.NET - Start All Services

set "ROOT=%~dp0"
set "NODE_DIR=%ROOT%vben-web\.runtime\node-v22.22.0-win-x64"
set "NODE_EXE=%NODE_DIR%\node.exe"
set "LOG_DIR=%ROOT%.run-logs"
set "RUNTIME_DATA=%ROOT%.runtime-data"

if not exist "%NODE_EXE%" (
  echo [ERROR] Local Node runtime was not found:
  echo         %NODE_EXE%
  pause
  exit /b 1
)
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
if not exist "%RUNTIME_DATA%" mkdir "%RUNTIME_DATA%"

call :is_listening 5005
if errorlevel 1 (
  echo [STARTING] backend  http://localhost:5005
  start "Admin.NET Backend :5005" /min powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\run-service.ps1" backend
) else (
  echo [RUNNING] backend  http://localhost:5005
)

call :is_listening 8888
if errorlevel 1 (
  echo [STARTING] legacy-web  http://localhost:8888
  start "Admin.NET Legacy Web :8888" /min powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\run-service.ps1" legacy-web
) else (
  echo [RUNNING] legacy-web  http://localhost:8888
)

call :is_listening 5666
if errorlevel 1 (
  echo [STARTING] vben-web  http://localhost:5666
  start "Admin.NET Vben Web :5666" /min powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%scripts\run-service.ps1" vben-web
) else (
  echo [RUNNING] vben-web  http://localhost:5666
)

echo.
echo Waiting for services, up to 60 seconds...
for /l %%I in (1,1,60) do (
  call :all_ready
  if not errorlevel 1 goto :ready
  ping.exe 127.0.0.1 -n 2 >nul
)

echo.
echo [WARNING] Some services did not start. Check logs in:
echo           %LOG_DIR%
call :print_status 5005 Backend
call :print_status 8888 "Legacy Web"
call :print_status 5666 "Vben Web"
echo Press any key to close this window.
pause >nul
exit /b 1

:ready
echo.
call :print_status 5005 Backend
call :print_status 8888 "Legacy Web"
call :print_status 5666 "Vben Web"
echo.
echo All services are ready. Closing this window will not stop them.
echo Use stop-all.bat when you want to stop the services.
echo Press any key to close this window.
pause >nul
exit /b 0

:all_ready
call :is_listening 5005
if errorlevel 1 exit /b 1
call :is_listening 8888
if errorlevel 1 exit /b 1
call :is_listening 5666
if errorlevel 1 exit /b 1
exit /b 0

:print_status
call :is_listening %1
if errorlevel 1 (
  echo [NOT RUNNING] %~2: http://localhost:%1
) else (
  echo [READY] %~2: http://localhost:%1
)
exit /b 0

:is_listening
netstat.exe -ano -p TCP | findstr.exe /R /C:":%1 .*LISTENING" >nul 2>nul
exit /b %ERRORLEVEL%
