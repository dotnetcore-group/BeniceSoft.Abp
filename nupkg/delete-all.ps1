# delete-all.bat calls this script
$Source = "https://mes-nexus.wecharmer.com/repository/nuget-hosted"
$ApiKey = "ad571298-4c13-34fd-a3e1-0b6632b0476f"
$NexusUrl = "https://mes-nexus.wecharmer.com"
$Repository = "nuget-hosted"
$PackagePrefix = "BeniceSoft"

$ErrorActionPreference = "Continue"

function Get-PackagesFromServer {
    $allItems = @()
    $continuationToken = $null
    $uriBase = "$NexusUrl/service/rest/v1/search"

    do {
        $query = "repository=$Repository&format=nuget"
        if ($continuationToken) {
            $query += "&continuationToken=$continuationToken"
        }
        $response = Invoke-RestMethod -Uri "$uriBase`?$query" -Method GET
        foreach ($item in $response.items) {
            if ($item.name -like "$PackagePrefix*") {
                $allItems += [PSCustomObject]@{ name = $item.name; version = $item.version }
            }
        }
        $continuationToken = $response.continuationToken
    } while ($continuationToken)

    return $allItems
}

Write-Host "Source: $Source"
Write-Host "Delete prefix: $PackagePrefix*"
Write-Host "Listing all packages from Nexus (q=BeniceSoft search is unreliable on this server)..."
Write-Host ""

$packages = Get-PackagesFromServer

if (-not $packages -or $packages.Count -eq 0) {
    Write-Host "No BeniceSoft packages found on server." -ForegroundColor Yellow
    exit 0
}

$unique = $packages | Sort-Object name, version -Unique
Write-Host "Will delete $($unique.Count) version(s):"
$unique | ForEach-Object { Write-Host "  $($_.name) $($_.version)" }
Write-Host ""

$deleted = 0
$failed = 0

foreach ($pkg in $unique) {
    Write-Host "Deleting $($pkg.name) $($pkg.version) ..."
    & dotnet nuget delete $pkg.name $pkg.version --source $Source --api-key $ApiKey --non-interactive
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  OK" -ForegroundColor Green
        $deleted++
    }
    else {
        Write-Host "  FAIL" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
Write-Host "Done. OK: $deleted, FAIL: $failed"
if ($failed -gt 0) {
    exit 1
}
