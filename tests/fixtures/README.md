# Local telemetry fixtures

## `sample-three-lap-run.jsonl`

Deterministic raw-packet capture with:

- 3 completed laps (best lap 88.0s)
- Throttle, brake, speed, and steering variation
- Fuel drop across the stint
- Lap 2 invalid/off-track flag telemetry (`flags: "invalid"`)
- Lap progress wraps for lap boundary detection

## Regenerate fixture

From repo root:

```powershell
dotnet build tests/RaceEngineer.SmokeTests/RaceEngineer.SmokeTests.csproj
dotnet run --project tests/RaceEngineer.SmokeTests/RaceEngineer.SmokeTests.csproj --no-build -- --write-fixture tests/fixtures
```

## Run local end-to-end verification

```powershell
./tests/scripts/run-local-e2e.ps1
```

This replays the fixture through `PacketReplayTool` and verifies parsing, events, completed laps, analytics, lap intelligence, telemetry traces, and coach evidence answers without UDP, SimHub, or the game.
