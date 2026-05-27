Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

dotnet restore .\RaceEngineer.sln
dotnet build .\RaceEngineer.sln --configuration Release --no-restore
dotnet run --project .\tests\RaceEngineer.SmokeTests\RaceEngineer.SmokeTests.csproj --configuration Release --no-build

