@echo off
setlocal EnableExtensions

set "ROOT=%~dp0.."
call "%~dp0stop-web.bat"

timeout /t 2 /nobreak >nul

echo Starting PlatformHost backend...
start "Atlas.PlatformHost" cmd /k "cd /d ""%ROOT%"" && dotnet run --project src\backend\Atlas.PlatformHost"

echo Starting AppHost backend...
start "Atlas.AppHost" cmd /k "cd /d ""%ROOT%"" && dotnet run --project src\backend\Atlas.AppHost"

echo Starting PlatformWeb frontend...
start "Atlas.PlatformWeb" cmd /k "cd /d ""%ROOT%\src\frontend"" && pnpm run dev:platform-web"

echo.
echo All services are starting in separate windows.
endlocal
