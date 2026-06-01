Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

powershell -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "tests\scripts\publish-windows-release.ps1") -Development @args
