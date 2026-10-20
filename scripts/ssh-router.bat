@echo off
setlocal
if "%~1"=="" (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ssh-router.ps1" -Action start
) else (
  powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ssh-router.ps1" -Action %~1
)
