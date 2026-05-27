Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

dotnet restore .\RaceEngineer.sln
dotnet build .\RaceEngineer.sln --configuration Release --no-restore

