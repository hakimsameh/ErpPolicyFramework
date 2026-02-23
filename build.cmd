@echo off
REM Policy Framework - Build and run demos (Windows)
REM For junior developers: double-click or run from command prompt

echo Building solution...
dotnet build ErpPolicyFramework.sln
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo Running tests...
dotnet test ErpPolicyFramework.sln --no-build
if %ERRORLEVEL% neq 0 exit /b %ERRORLEVEL%

echo.
echo Running demo...
dotnet run --project src/PolicyFramework.Host --no-build
