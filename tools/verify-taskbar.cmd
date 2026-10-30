@echo off
setlocal
cd /d "%~dp0.."
set DATA=artifacts\desktop-verify
echo.
echo  DeskOrder - taskbar transparency probe
echo  This will apply "clear" for 8 seconds and then restore it.
echo  Log: %DATA%\logs\app.log
echo.
if not exist "dist\DeskOrder.exe" (
  echo dist\DeskOrder.exe not found. Build it first.
  pause
  exit /b 1
)
"dist\DeskOrder.exe" --probe-taskbar 8 --mode clear --data-dir "%DATA%"
echo.
echo Result (look for "probe-taskbar applied=True/False"):
echo   %CD%\%DATA%\logs\app.log
echo.
pause
