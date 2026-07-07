@echo off
REM Launches the Nautilus ERP dev stack: API + client in their own terminals, then Chrome.
setlocal
cd /d "%~dp0"

echo Starting Nautilus ERP...
echo   - API    : http://localhost:5126
echo   - Client : http://localhost:5173
echo.

REM Backend API (SQLite dev provider, no Docker needed).
start "Nautilus API" cmd /k "dotnet run --project src/ERP.API"

REM Frontend SPA (Vite dev server, proxies to the API).
start "Nautilus Client" cmd /k "cd client ^&^& npm run dev"

echo Waiting for services to warm up...
timeout /t 22 /nobreak >nul

REM Open the app. Falls back to the default browser if Chrome isn't found.
start "" chrome "http://localhost:5173" 2>nul || start "" "http://localhost:5173"

echo.
echo Done. Two terminals are running (API + client); leave them open.
echo Sign in with admin@erp.local / Admin#12345
echo You can close THIS window.
endlocal
