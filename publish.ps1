[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Development
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "tests\scripts\windows-smart-app-control.ps1")

$project = ".\src\RaceEngineer.Desktop.Wpf\RaceEngineer.Desktop.Wpf.csproj"
$outputName = if ($Development) { "win-x64-dev" } else { "win-x64" }
$output = ".\artifacts\publish\$outputName"
$selfContained = if ($Development) { "false" } else { "true" }

if ($Development) {
    Write-Host "Development publish: framework-dependent (requires .NET 8 Windows Desktop Runtime)."
}
else {
    Write-Host "Release publish: self-contained Windows x64."
}

Write-SmartAppControlEnvironmentReport -Context publish -Development:$Development

dotnet restore .\RaceEngineer.sln
dotnet publish $project `
  --configuration Release `
  --runtime win-x64 `
  --self-contained $selfContained `
  --output $output `
  -p:PublishSingleFile=false `
  -p:PublishReadyToRun=true

$publishedFiles = @(Get-ChildItem -Path $output -Recurse -File -Force -ErrorAction SilentlyContinue)
foreach ($file in $publishedFiles) {
    Unblock-File -LiteralPath $file.FullName -ErrorAction SilentlyContinue
}

Write-Host "Published to $output"

if ($Development) {
    Write-Host "Launch with: dotnet `"$output\RaceEngineer.Desktop.Wpf.dll`""
}
