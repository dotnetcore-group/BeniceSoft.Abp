@echo off
setlocal EnableExtensions
cd /d "%~dp0"

rem GitHub Packages NuGet feed (org: dotnetcore-group)
set "source=https://nuget.pkg.github.com/dotnetcore-group/index.json"
set "source_name=github"
set "package_dir=..\src\bin"

rem 1) Prefer env PAT  2) Else reuse password already stored for source "github"
set "api_key="
if defined GH_PACKAGES_PAT set "api_key=%GH_PACKAGES_PAT%"
if not defined api_key if defined GITHUB_TOKEN set "api_key=%GITHUB_TOKEN%"

if not defined api_key (
    for /f "usebackq delims=" %%A in (`powershell -NoProfile -Command "$c=[xml](Get-Content -Raw \"$env:APPDATA\NuGet\NuGet.Config\"); $n=$c.configuration.packageSourceCredentials.github; if($n){ ($n.add | ? key -eq 'ClearTextPassword').value }"`) do set "api_key=%%A"
)

if not defined api_key (
    echo ERROR: No GitHub Packages credentials found.
    echo Options:
    echo   1^) set GH_PACKAGES_PAT=ghp_xxxxxxxx   then re-run
    echo   2^) or once: dotnet nuget add source "%source%" --name %source_name% --username YOUR_USER --password YOUR_PAT --store-password-in-clear-text
    exit /b 1
)

if not defined GH_USERNAME set "GH_USERNAME=%USERNAME%"

echo ============ Ensure GitHub NuGet source ============
dotnet nuget list source | findstr /i /c:"%source_name%" >nul
if errorlevel 1 (
    echo Adding source "%source_name%" ...
    dotnet nuget add source "%source%" --name "%source_name%" --username "%GH_USERNAME%" --password "%api_key%" --store-password-in-clear-text
    if errorlevel 1 exit /b 1
) else (
    echo Source "%source_name%" already registered ^(will reuse stored credentials^).
)

echo.
echo ============ Build Solution (Release) ============
echo nupkg will be generated to %package_dir% via GeneratePackageOnBuild
dotnet build ..\BeniceSoft.Abp.sln -c Release
if errorlevel 1 exit /b 1

echo.
echo ============ Push Packages to GitHub Packages ============
dir /b "%package_dir%\*.nupkg" >nul 2>&1
if errorlevel 1 (
    echo No .nupkg found in %package_dir%, please build the solution first.
    exit /b 1
)

for %%f in ("%package_dir%\*.nupkg") do (
    echo %%~nxf | findstr /i "Sample" >nul
    if not errorlevel 1 (
        del "%%f"
        echo skip/delete %%~nxf ^(Sample package^)
    ) else (
        echo %%~nxf | findstr /i ".symbols." >nul
        if not errorlevel 1 (
            echo skip %%~nxf ^(symbols^)
        ) else (
            echo push nuget package %%~nxf
            dotnet nuget push "%%f" --source "%source_name%" --api-key "%api_key%" --skip-duplicate
            if errorlevel 1 exit /b 1
            del "%%f"
            echo package %%~nxf pushed and deleted locally.
        )
    )
)

echo.
echo ============ Done ============
echo View: https://github.com/orgs/dotnetcore-group/packages
pause
endlocal
