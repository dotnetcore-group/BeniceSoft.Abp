@echo off
cd /d "%~dp0"

set source=https://mes-nexus.wecharmer.com/repository/nuget-hosted
set api_key=ad571298-4c13-34fd-a3e1-0b6632b0476f
set package_dir=..\src\bin

echo ============ Build Solution (Release) ============
echo nupkg will be generated to %package_dir% via GeneratePackageOnBuild
dotnet build ..\BeniceSoft.Abp.sln -c Release
if errorlevel 1 exit /b 1

echo.
echo ============ Push Packages from src\bin ============
dir /b "%package_dir%\*.nupkg" >nul 2>&1
if errorlevel 1 (
    echo No .nupkg found in %package_dir%, please build the solution first.
    exit /b 1
)

for %%f in ("%package_dir%\*.nupkg") do (
    echo %%~nxf | findstr /i "Sample" >nul
    if errorlevel 1 (
        echo push nuget package %%~nxf
        dotnet nuget push "%%f" -s %source% --api-key %api_key%
        if errorlevel 1 exit /b 1
        del "%%f"
        echo package %%~nxf deleted!
    ) else (
        del "%%f"
        echo package %%~nxf deleted! ^(Sample package^)
    )
)

echo.
echo ============ Done ============
pause
