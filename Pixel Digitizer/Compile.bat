@echo off
:: Find MSBuild
set "MSBUILD=C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"

if not exist "%MSBUILD%" (
    echo [ERROR] MSBuild 4.0 not found at %MSBUILD%
    echo Please ensure the .NET Framework 4.0 or 4.5+ is installed.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo   Compiling PiXeL Digitizer ^(Release Mode^)
echo ============================================================
echo.

:: Run Build
"%MSBUILD%" SnesGFX.csproj /p:Configuration=Release /p:Platform=x86 /m /v:m

if %ERRORLEVEL% neq 0 (
    echo.
    echo [ERROR] Build failed! Please check the output above for details.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo   SUCCESS! PiXeL Digitizer has been compiled.
echo   Executable: bin\Release\PiXeL_Digitizer.exe
echo ============================================================
echo.
pause
