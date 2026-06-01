[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Development
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "windows-smart-app-control.ps1")

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
Set-Location $repoRoot

$solution = Join-Path $repoRoot "RaceEngineer.sln"
$wpfProject = Join-Path $repoRoot "src\RaceEngineer.Desktop.Wpf\RaceEngineer.Desktop.Wpf.csproj"
$smokeProject = Join-Path $repoRoot "tests\RaceEngineer.SmokeTests\RaceEngineer.SmokeTests.csproj"
$appSettingsSource = Join-Path $repoRoot "src\RaceEngineer.Desktop.Wpf\appsettings.json"
$publishDirName = if ($Development) { "win-x64-dev" } else { "win-x64" }
$publishDir = Join-Path $repoRoot "artifacts\publish\$publishDirName"
$exePath = Join-Path $publishDir "RaceEngineer.Desktop.Wpf.exe"
$dllPath = Join-Path $publishDir "RaceEngineer.Desktop.Wpf.dll"
$selfContained = if ($Development) { "false" } else { "true" }

function Remove-BuildArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RootPath
    )

    Write-Host "Cleaning bin/, obj/, and *_wpftmp* under $RootPath ..."

    Get-ChildItem -Path $RootPath -Include bin, obj -Recurse -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[\\/]\.git[\\/]' } |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    Get-ChildItem -Path $RootPath -Filter "*_wpftmp*" -Recurse -Directory -Force -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

function Remove-ShippingArtifacts {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    Get-ChildItem -Path $OutputPath -Include bin, obj -Recurse -Directory -Force -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    Get-ChildItem -Path $OutputPath -Filter "*_wpftmp*" -Recurse -Force -ErrorAction SilentlyContinue |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

function Start-PublishedAppVerification {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,

        [Parameter()]
        [switch]$UseDotNetHost
    )

    if ($UseDotNetHost) {
        $dll = Join-Path (Split-Path -Parent $ExecutablePath) "RaceEngineer.Desktop.Wpf.dll"
        if (-not (Test-Path -LiteralPath $dll)) {
            throw "Framework-dependent publish is missing DLL: $dll"
        }

        return Start-Process -FilePath "dotnet" -ArgumentList "`"$dll`"" -PassThru -WindowStyle Minimized
    }

    return Start-Process -FilePath $ExecutablePath -PassThru -WindowStyle Minimized
}

if ($Development) {
    Write-Host "Development publish mode: framework-dependent (requires .NET 8 Windows Desktop Runtime)."
}
else {
    Write-Host "Release publish mode: self-contained Windows x64."
}

Write-SmartAppControlEnvironmentReport -Context publish -Development:$Development

Remove-BuildArtifacts -RootPath $repoRoot

if (Test-Path $publishDir) {
    Write-Host "Removing previous publish output at $publishDir ..."
    Remove-Item -Path $publishDir -Recurse -Force
}

Write-Host "Restoring solution..."
dotnet restore $solution
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Building Release..."
dotnet build $solution -c Release --no-restore
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Running smoke tests (Release)..."
dotnet run --project $smokeProject -c Release --no-build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Publishing WPF app to $publishDir (self-contained=$selfContained) ..."
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
dotnet publish $wpfProject `
    -c Release `
    -r win-x64 `
    --self-contained $selfContained `
    -o $publishDir `
    -p:PublishSingleFile=false `
    -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

if (-not (Test-Path $appSettingsSource)) {
    throw "Missing appsettings source file: $appSettingsSource"
}

Write-Host "Copying appsettings.json..."
Copy-Item -Path $appSettingsSource -Destination (Join-Path $publishDir "appsettings.json") -Force

Remove-ShippingArtifacts -OutputPath $publishDir

Write-Host "Unblocking published files (removes Zone.Identifier / Mark of the Web)..."
$publishedFiles = @(Get-ChildItem -Path $publishDir -Recurse -File -Force)
foreach ($file in $publishedFiles) {
    Unblock-File -LiteralPath $file.FullName -ErrorAction SilentlyContinue
}

$blockedAfterUnblock = @(
    $publishedFiles | Where-Object { Test-Path -LiteralPath ($_.FullName + ':Zone.Identifier') }
)
if ($blockedAfterUnblock.Count -gt 0) {
    $sample = ($blockedAfterUnblock | Select-Object -First 3 | ForEach-Object { $_.Name }) -join ', '
    throw "Unblock-File did not clear Zone.Identifier on $($blockedAfterUnblock.Count) file(s) (e.g. $sample). Run as admin or check Application Control policy."
}

Write-Host "Unblocked $($publishedFiles.Count) published file(s)."

if (-not (Test-Path $exePath)) {
    throw "Published executable was not found: $exePath"
}

Write-Host "Verifying published app starts..."
$verifyWithDotNet = $Development -and (Test-SmartAppControlIsActive)
if ($verifyWithDotNet) {
    Write-Host "Smart App Control is active; verifying via dotnet host instead of unsigned .exe."
}

$process = Start-PublishedAppVerification -ExecutablePath $exePath -UseDotNetHost:$verifyWithDotNet
Start-Sleep -Seconds 4
if ($process.HasExited) {
    Write-SmartAppControlBlockingHelp -ExecutablePath $exePath -ExitCode $process.ExitCode -Development:$Development
    throw "Published app exited early with code $($process.ExitCode)."
}

Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue

Write-Host ""
if ($Development) {
    Write-Host "Windows development package ready (framework-dependent)."
    Write-Host "Folder:   $publishDir"
    Write-Host "Launch:   dotnet `"$dllPath`""
    Write-Host "Alternate: $exePath"
}
else {
    Write-Host "Windows release package ready (self-contained)."
    Write-Host "Executable: $exePath"
}
