@echo off
REM ==================================================================
REM  RTD Platform - backend (Spring Boot 3.4 + MyBatis-Plus)
REM  started by Windows Tool  |  http://localhost:8081/api
REM  usage: start-rtd-backend.bat [auto^|prod^|dev]   (default auto)
REM         auto = Oracle 1521 reachable ? prod : dev (H2 file)
REM  log:   ...\RTD GUI\logs\rtd-backend.log
REM ==================================================================
setlocal
set "PROJ=C:\Users\Linusonn\Documents\Project\RTD GUI\rtd-backend"
set "LOGDIR=C:\Users\Linusonn\Documents\Project\RTD GUI\logs"
set "MVN=C:\Users\Linusonn\tools\apache-maven-3.9.11\bin\mvn.cmd"
if not defined JAVA_HOME set "JAVA_HOME=C:\Program Files\Eclipse Adoptium\jdk-21.0.11.10-hotspot"
set "PATH=%JAVA_HOME%\bin;%PATH%"
if not exist "%LOGDIR%" mkdir "%LOGDIR%"
set "LOG=%LOGDIR%\rtd-backend.log"

if not exist "%PROJ%\pom.xml" goto :nodir
cd /d "%PROJ%"

echo. >> "%LOG%"
echo ================================================== >> "%LOG%"
echo [RTD] %DATE% %TIME%  start backend >> "%LOG%"

REM ---- 1. profile: Oracle up -> prod, else dev(H2) ----------------
set "PROFILE=%~1"
if "%PROFILE%"=="" set "PROFILE=auto"
if /i not "%PROFILE%"=="auto" goto :profiled
set "PROFILE=dev"
netstat -an | findstr /I "LISTENING" | findstr /C:":1521" >nul 2>&1 && set "PROFILE=prod"
:profiled
echo [RTD] profile=%PROFILE% >> "%LOG%"

REM ---- 2. build jar if missing or sources newer -------------------
if not exist "target\rtd-backend.jar" goto :build
powershell -NoProfile -Command "if ((Get-ChildItem -Recurse -File src,pom.xml | Sort-Object LastWriteTime -Descending | Select-Object -First 1).LastWriteTime -gt (Get-Item 'target\rtd-backend.jar').LastWriteTime) { exit 1 } else { exit 0 }"
if errorlevel 1 goto :build
goto :run

:build
echo [RTD] maven package (first run / sources changed) ... >> "%LOG%"
call "%MVN%" -B -q clean package -DskipTests >> "%LOG%" 2>&1
if errorlevel 1 goto :buildfail

:run
echo [RTD] java -jar target\rtd-backend.jar --spring.profiles.active=%PROFILE% >> "%LOG%"
java -jar "target\rtd-backend.jar" --spring.profiles.active=%PROFILE% >> "%LOG%" 2>&1
echo [RTD] backend exited code=%errorlevel% >> "%LOG%"
endlocal
exit /b 0

:buildfail
echo [RTD] MAVEN BUILD FAILED - see "%LOG%"
echo [RTD] MAVEN BUILD FAILED >> "%LOG%"
pause
endlocal
exit /b 1

:nodir
echo [RTD] backend dir not found: %PROJ%
pause
endlocal
exit /b 1
