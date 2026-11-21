@echo off
setlocal enabledelayedexpansion

echo ======================================================================
echo Building TrafficMonitor Antigravity Plugin (x64)
echo ======================================================================

call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to initialize vcvars64 environment!
    exit /b 1
)

cd /d "%~dp0"

echo.
echo Compiling AntigravityPlugin.dll...
cl /nologo /LD /EHsc /O2 /std:c++17 AntigravityPlugin.cpp /DEF:AntigravityPlugin.def /Fe:AntigravityPlugin.dll /link /MACHINE:X64
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to build AntigravityPlugin.dll!
    exit /b 1
)

echo.
echo Compiling verify_plugin.exe...
cl /nologo /EHsc /std:c++17 verify_plugin.cpp /Fe:verify_plugin.exe /link /MACHINE:X64
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to build verify_plugin.exe!
    exit /b 1
)

echo.
echo Verifying local AntigravityPlugin.dll...
verify_plugin.exe AntigravityPlugin.dll
if %ERRORLEVEL% neq 0 (
    echo Error: Plugin verification failed!
    exit /b 1
)

echo.
echo Deploying to D:\TrafficMonitor\plugins...
if not exist "D:\TrafficMonitor\plugins" (
    mkdir "D:\TrafficMonitor\plugins"
    echo Created directory D:\TrafficMonitor\plugins
)

copy /y AntigravityPlugin.dll "D:\TrafficMonitor\plugins\AntigravityPlugin.dll"
if %ERRORLEVEL% neq 0 (
    echo Error: Failed to copy AntigravityPlugin.dll to D:\TrafficMonitor\plugins!
    exit /b 1
)

echo.
echo Verifying deployed DLL at D:\TrafficMonitor\plugins\AntigravityPlugin.dll...
verify_plugin.exe "D:\TrafficMonitor\plugins\AntigravityPlugin.dll"
if %ERRORLEVEL% neq 0 (
    echo Error: Deployed plugin verification failed!
    exit /b 1
)

echo.
echo ======================================================================
echo Build and deployment completed successfully!
echo ======================================================================
exit /b 0
