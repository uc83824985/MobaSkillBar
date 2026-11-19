@echo off
setlocal
where dotnet >nul 2>&1
if errorlevel 1 (
    echo Install the .NET 8 SDK and run this file again.
    pause
    exit /b 1
)
dotnet run --project "%~dp0source\MobaSkillBar.csproj" --configuration Release
if errorlevel 1 (
    pause
    exit /b 1
)
