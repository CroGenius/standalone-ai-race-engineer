Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$project = ".\src\RaceEngineer.Desktop.Wpf\RaceEngineer.Desktop.Wpf.csproj"
$output = ".\artifacts\publish\win-x64"

dotnet restore .\RaceEngineer.sln
dotnet publish $project `
  --configuration Release `
  --runtime win-x64 `
  --self-contained true `
  --output $output `
  -p:PublishSingleFile=false `
  -p:PublishReadyToRun=true

Write-Host "Published to $output"

