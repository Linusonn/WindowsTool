@echo off
REM ==================================================================
REM  RTD Platform - frontend (Vue 3 + Vite 6)
REM  started by Windows Tool  |  http://localhost:5173  (/api -> 8081)
REM  log:   ...\RTD GUI\logs\rtd-frontend.log
REM ==================================================================
setlocal
set "PROJ=C:\Users\Linusonn\Documents\Project\RTD GUI\rtd-frontend"
set "LOGDIR=C:\Users\Linusonn\Documents\Project\RTD GUI\logs"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set "LOG=%LOGDIR%\rtd-frontend.log"

REM node/npm: use PATH if present, else fall back to the bundled runtime
where node >nul 2>&1 || set "PATH=C:\Users\Linusonn\AppData\Local\hermes\node;%PATH%"

if not exist "%PROJ%\package.json" goto :nodir
cd /d "%PROJ%"
if not exist "node_modules\vite" goto :noinstall

echo. >> "%LOG%"
echo ================================================== >> "%LOG%"
echo [RTD] %DATE% %TIME%  start vite dev server >> "%LOG%"
call npm run dev >> "%LOG%" 2>&1
echo [RTD] vite exited code=%errorlevel% >> "%LOG%"
endlocal
exit /b 0

:noinstall
echo [RTD] node_modules missing - run "npm install" in %PROJ% first
echo [RTD] node_modules missing >> "%LOG%"
pause
endlocal
exit /b 1

:nodir
echo [RTD] frontend dir not found: %PROJ%
pause
endlocal
exit /b 1
