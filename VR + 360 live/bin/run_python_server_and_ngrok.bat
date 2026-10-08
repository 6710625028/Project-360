@echo off
title Pico 4 VR Stream Launcher
cd /d "%~dp0"

echo ===================================================
echo   Starting Pico 4 Stream Server & ngrok Tunnel
echo ===================================================
echo.

:: 1. เปิดหน้าต่างรัน Python FastAPI / Uvicorn
echo [1/2] Starting Python Server...
start "Python Server (Port 8000)" cmd /k "python -m uvicorn python_server:app --host 0.0.0.0 --port 8000"

:: หน่วงเวลา 2 วินาทีเพื่อให้เซิร์ฟเวอร์เริ่มทำงานก่อน
timeout /t 2 /nobreak >nul

:: 2. เปิดหน้าต่างรัน ngrok
echo [2/2] Starting ngrok Tunnel...
start "ngrok Tunnel" cmd /k "ngrok http 8000"

echo.
echo ===================================================
echo   Services Started! 
echo   - Copy the HTTPS link from the ngrok window
echo   - You can close this launcher window now.
echo ===================================================
pause