$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

Write-Host "Building RaceEngineer.sln..."
dotnet build RaceEngineer.sln --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running local end-to-end smoke tests..."
dotnet run --project tests/RaceEngineer.SmokeTests/RaceEngineer.SmokeTests.csproj --no-build
exit $LASTEXITCODE
