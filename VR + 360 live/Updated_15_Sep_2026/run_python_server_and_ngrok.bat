@echo off
setlocal enabledelayedexpansion
title Pico 4 VR Stream Launcher
cd /d "%~dp0"

echo ===================================================
echo    Starting Pico 4 Stream Server & ngrok Tunnel
echo ===================================================
echo.

:: ตรวจหา Python ที่พร้อมใช้งานในเครื่องโดยอัตโนมัติ
set "PY_CMD="

:: 1. ลองใช้ py launcher มาตรฐานของ Windows
where py >nul 2>&1
if %ERRORLEVEL% equ 0 (
    set "PY_CMD=py"
    goto :found_python
)

:: 2. เช็กโฟลเดอร์ Programs มาตรฐาน (Python 3.10 - 3.14)
for %%V in (314 313 312 311 310) do (
    if exist "%LOCALAPPDATA%\Programs\Python\Python%%V\python.exe" (
        set "PY_CMD="%LOCALAPPDATA%\Programs\Python\Python%%V\python.exe""
        goto :found_python
    )
)

:: 3. ลองคำสั่ง python จาก PATH ทั่วไป
where python >nul 2>&1
if %ERRORLEVEL% equ 0 (
    set "PY_CMD=python"
    goto :found_python
)

:: 4. เช็กกรณีมีเฉพาะ MSYS2 MinGW64
if exist "C:\msys64\mingw64\bin\python.exe" (
    set "PY_CMD=C:\msys64\mingw64\bin\python.exe"
    goto :found_python
)

:found_python
if "%PY_CMD%"=="" (
    echo [ERROR] ไม่พบการติดตั้ง Python ในเครื่องนี้ กรุณาติดตั้ง Python ก่อนใช้งาน
    pause
    exit /b
)

echo [INFO] ใช้ Python จาก: %PY_CMD%
echo.

:: 1. เปิดหน้าต่างรัน Python FastAPI / Uvicorn
echo [1/2] Starting Python Server...
start "Python Server (Port 8000)" cmd /k "%PY_CMD% -m uvicorn python_server:app --host 0.0.0.0 --port 8000"

:: หน่วงเวลา 2 วินาทีเพื่อให้เซิร์ฟเวอร์เริ่มทำงานก่อน
timeout /t 2 /nobreak >nul

:: 2. เปิดหน้าต่างรัน ngrok
echo [2/2] Starting ngrok Tunnel...
start "ngrok Tunnel" cmd /k "ngrok http 8000"

echo.
echo ===================================================
echo    Services Started! 
echo    - Copy the HTTPS link from the ngrok window
echo    - You can close this launcher window now.
echo ===================================================
pause