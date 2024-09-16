@echo off
dotnet publish -c Release -o dist

REM Copy the compiled executable into ~/bin folder
set "HOME=%USERPROFILE%"
set "BIN_DIR=%HOME%\bin"

if not exist "%BIN_DIR%" (
    mkdir "%BIN_DIR%"
)

copy /Y "dist\NatsUpdater.exe" "%BIN_DIR%\NatsUpdater.exe"
echo Copied NatsUpdater.exe to %BIN_DIR%
