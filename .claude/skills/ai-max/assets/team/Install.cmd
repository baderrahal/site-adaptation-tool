@echo off
rem Installs the tool for this Windows user from the folder this file sits in, a team zip
rem extracted. It starts Windows PowerShell 5.1 for this one run with the script policy bypassed,
rem needs no admin rights, and runs team-install.ps1 beside it. Put back the previous version.cmd
rem calls this file with -PutBack, so the way PowerShell is started is written once.
rem Rename this file to Install and the tool's name, and change the name in the other launcher too.
setlocal
set "here=%~dp0"
if not exist "%here%team-install.ps1" (
    echo REFUSED: this file was started from inside the zip, or the zip was not extracted whole. Right click the zip, choose Extract All, and double click this file in the folder that makes. Nothing was changed.
    echo.
    pause
    exit /b 2
)
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%here%team-install.ps1" %*
set "code=%errorlevel%"
echo.
pause
exit /b %code%
