Set-StrictMode -Version Latest

function Get-SmartAppControlState {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    try {
        $status = Get-MpComputerStatus -ErrorAction Stop
        if ($null -ne $status.SmartAppControlState) {
            return [string]$status.SmartAppControlState
        }
    }
    catch {
        # Fall back to registry when Defender cmdlets are unavailable.
    }

    try {
        $policyPath = 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy'
        if (-not (Test-Path -LiteralPath $policyPath)) {
            return 'Unknown'
        }

        $policy = Get-ItemProperty -LiteralPath $policyPath -ErrorAction Stop
        $stateValue = $policy.VerifiedAndReputablePolicyState
        if ($null -eq $stateValue) {
            return 'Unknown'
        }

        switch ([int]$stateValue) {
            0 { return 'Off' }
            1 { return 'Eval' }
            2 { return 'On' }
            default { return 'Unknown' }
        }
    }
    catch {
        return 'Unknown'
    }
}

function Test-SmartAppControlIsActive {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter()]
        [string]$State = (Get-SmartAppControlState)
    )

    return $State -eq 'On' -or $State -eq 'Eval'
}

function Get-WindowsDesktopRuntimeVersion {
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $runtimes = @(dotnet --list-runtimes 2>$null)
    foreach ($line in $runtimes) {
        if ($line -match '^Microsoft\.WindowsDesktop\.App\s+(\S+)') {
            return $Matches[1]
        }
    }

    return $null
}

function Write-SmartAppControlEnvironmentReport {
    [CmdletBinding()]
    param(
        [Parameter()]
        [ValidateSet('publish', 'verify', 'launch')]
        [string]$Context = 'publish',

        [Parameter()]
        [switch]$Development
    )

    $state = Get-SmartAppControlState
    $active = Test-SmartAppControlIsActive -State $state
    $desktopRuntime = Get-WindowsDesktopRuntimeVersion

    Write-Host ""
    Write-Host "Windows Smart App Control / WDAC check ($Context):"
    Write-Host "  Smart App Control state: $state"

    if ($active) {
        Write-Host "  Smart App Control is active and may block unsigned local builds."
    }
    elseif ($state -eq 'Unknown') {
        Write-Host "  Smart App Control state could not be determined (Defender cmdlets or CI policy registry unavailable)."
    }
    else {
        Write-Host "  Smart App Control is off."
    }

    if ($Development) {
        if ($null -ne $desktopRuntime) {
            Write-Host "  .NET Windows Desktop runtime: $desktopRuntime"
        }
        else {
            Write-Host "  .NET Windows Desktop runtime: not found (install .NET 8 Desktop Runtime for framework-dependent publish)."
        }
    }

    if ($active -and -not $Development -and $Context -eq 'publish') {
        Write-Host ""
        Write-Host "WARNING: Self-contained release publish may be blocked on this PC."
        Write-Host "For local development/testing, rerun with -Development:"
        Write-Host "  powershell -ExecutionPolicy Bypass -File .\tests\scripts\publish-windows-release.ps1 -Development"
        Write-Host "Or run without publishing:"
        Write-Host "  dotnet run --project src\RaceEngineer.Desktop.Wpf\RaceEngineer.Desktop.Wpf.csproj"
    }

    Write-Host ""
}

function Write-SmartAppControlBlockingHelp {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,

        [Parameter()]
        [int]$ExitCode = -1,

        [Parameter()]
        [switch]$Development
    )

    $state = Get-SmartAppControlState
    $active = Test-SmartAppControlIsActive -State $state
    $publishDir = Split-Path -Parent $ExecutablePath

    Write-Host ""
    Write-Host "Launch verification failed for:"
    Write-Host "  $ExecutablePath"
    if ($ExitCode -ge 0) {
        Write-Host "  Exit code: $ExitCode"
    }

    Write-Host ""
    Write-Host "Smart App Control / WDAC diagnosis:"
    Write-Host "  Smart App Control state: $state"

    if ($active) {
        Write-Host "  Likely cause: Windows blocked this unsigned local build under Smart App Control or WDAC."
        Write-Host "  Unblock-File only removes Mark-of-the-Web; it does not bypass Smart App Control."
    }
    else {
        Write-Host "  Smart App Control appears off. Check Event Viewer (Microsoft-Windows-CodeIntegrity/Operational) for WDAC blocks."
    }

    Write-Host ""
    Write-Host "Recommended local development options:"
    Write-Host "  1. Framework-dependent dev publish (requires .NET 8 Desktop Runtime):"
    Write-Host "       powershell -ExecutionPolicy Bypass -File .\tests\scripts\publish-windows-release.ps1 -Development"
    Write-Host "     Then launch:"
    Write-Host "       dotnet `"$publishDir\RaceEngineer.Desktop.Wpf.dll`""
    Write-Host "  2. Run directly from source (no publish folder):"
    Write-Host "       dotnet run --project src\RaceEngineer.Desktop.Wpf\RaceEngineer.Desktop.Wpf.csproj"
    Write-Host "  3. Release/self-contained publish for machines without Smart App Control, or after code signing."

    if (-not $Development) {
        Write-Host ""
        Write-Host "This machine has Smart App Control enabled; use -Development for day-to-day local testing."
    }

    Write-Host ""
}
