@echo off
echo ========================================
echo Junaid.GoogleGemini.Net Example Console
echo ========================================
echo.

REM Check if API key is set as environment variable
if "%GeminiApiKey%"=="" (
    echo WARNING: GeminiApiKey environment variable not set!
    echo.
    echo Please set your API key first:
    echo   set GeminiApiKey=your-actual-api-key-here
    echo.
    echo Or update appsettings.json with your API key.
    echo.
    pause
    exit /b 1
)

echo API Key found in environment variable.
echo Starting example application...
echo.

dotnet run

echo.
echo Application completed.
pause