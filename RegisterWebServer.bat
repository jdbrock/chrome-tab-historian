@echo off
:: Registers a scheduled task to run the TabHistorian web server at system startup.
:: Must be run as Administrator.

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b 0
)

schtasks /query /tn "TabHistorian Web" >nul 2>&1
if %errorlevel% equ 0 (
    echo Removing existing TabHistorian Web scheduled task...
    schtasks /delete /tn "TabHistorian Web" /f >nul 2>&1
)

schtasks /create ^
    /tn "TabHistorian Web" ^
    /tr "\"%~dp0src\TabHistorian.Web\bin\TabHistorian.Web.exe\"" ^
    /sc onlogon ^
    /rl highest ^
    /f

if %errorlevel% equ 0 (
    echo.
    echo TabHistorian Web scheduled task created successfully.
    echo It will start automatically at system startup.
) else (
    echo.
    echo ERROR: Failed to create scheduled task.
)

pause
