# Standalone AI Race Engineer

Long-term target: a standalone C#/.NET Windows desktop application for conversational race engineering and driver coaching. The Python prototype remains only as a quick logic reference; the product architecture is C#/.NET with no Python dependency for end users.

## Final Stack

- C# / .NET 8 or newer
- WPF desktop UI for Phase 1
- Native UDP telemetry via `System.Net.Sockets`
- JSON parsing via `System.Text.Json`
- Local SQLite session/event/notes storage
- Voice input/output later through Windows-compatible APIs

## Phase 1 Scope

Build chat + event log + live telemetry status first. No overlay/HUD complexity.

Implemented C# services:

- `TelemetryReceiver`: UDP listener on `127.0.0.1:20999`
- `TelemetryDiagnostics`: receiver health, packet rates, schema seen, and parser warnings
- `RawPacketCapture`: user-controlled JSONL capture of recent raw packets and parse results
- `PacketReplayTool`: replay captured packets through parser/session/event logic without UDP
- `TelemetrySnapshot`: canonical nullable telemetry model
- `SessionState`: current session, latest snapshot, recent events
- `EventEngine`: deterministic factual event detection
- `CoachEngine`: grounded coaching responses from structured facts only
- `StorageService`: SQLite persistence
- `ResearchService`: optional offline knowledge layer for track/car/setup/strategy notes
- `VoiceService`: placeholder for push-to-talk and TTS controls

Safety boundary:

- Telemetry facts are deterministic and stored as structured data.
- Missing telemetry values are `null`, never fake zeros.
- AI/coaching text consumes snapshots, events, summaries, driver memory, and clearly labeled knowledge sources.
- External track/car/setup advice is never presented as telemetry fact.
- Coaching answers must state uncertainty when facts are missing.

## Solution Layout

```text
RaceEngineer.sln
src/
  RaceEngineer.Core/
    Telemetry/
    Events/
    Session/
    Coaching/
    Knowledge/
    Storage/
    Voice/
  RaceEngineer.Desktop.Wpf/
tests/
  RaceEngineer.SmokeTests/
```

## SimHub UDP Payload

Expected schema:

```json
{
  "schema": "acevo_engineer.simhub_datacore",
  "schema_version": 1,
  "speed_kmh": 182.4,
  "rpm": 7100,
  "gear": 4,
  "throttle": 0.42,
  "brake": 0.0,
  "steering": -0.18,
  "lap_time_ms": 83542,
  "lap_time_s": 83.542,
  "lap_progress": 0.63,
  "fuel": 18.2,
  "position": 7,
  "flags": null,
  "damage": null,
  "tyre_temp_c": [86.2, 88.1, 82.4, 83.0],
  "tyre_pressure": [26.1, 26.3, 25.8, 25.9],
  "brake_temp_c": [610, 624, 530, 536],
  "tyre_wear": [0.08, 0.09, 0.06, 0.06]
}
```

## SimHub Mapping Checklist

Configure the SimHub DataCore exporter to send UDP JSON to:

- Host: `127.0.0.1`
- Port: `20999`
- Schema: `acevo_engineer.simhub_datacore`
- Schema version: `1`

Required field names expected by the parser:

- `schema`
- `schema_version`
- `speed_kmh`
- `rpm`
- `gear`
- `throttle`
- `brake`
- `steering`
- `lap_time_ms`
- `lap_time_s`
- `lap_progress`
- `fuel`
- `position`
- `flags`
- `damage`
- `tyre_temp_c`
- `tyre_pressure`
- `brake_temp_c`
- `tyre_wear`

Missing optional values are allowed and remain `null`. They are never converted to fake zeros.

To verify the mapping:

1. Start the WPF app.
2. Check the Telemetry Diagnostics panel.
3. Confirm `UDP bind` is `127.0.0.1:20999`.
4. Confirm `Receiver` is `Running`.
5. Start SimHub output.
6. Confirm `Packets received` and `Valid packets` increase.
7. Confirm `Schema seen` reads `acevo_engineer.simhub_datacore v1`.

If packets arrive but valid count does not increase:

- Check `Last invalid` in the diagnostics panel.
- If it says wrong schema, set `schema` exactly to `acevo_engineer.simhub_datacore`.
- If it says wrong schema version, set `schema_version` to numeric `1`.
- If it says invalid JSON, enable raw packet capture and inspect the JSONL debug file shown in the UI.

Raw packet capture:

- Off by default.
- Toggle `Capture Off/On` in the diagnostics panel.
- Captures the last N local telemetry packets as JSONL with raw JSON and parse result.
- Use `Replay Capture` to replay the capture through parser/session/event logic without SimHub running.

## Research And Knowledge

Phase 1 knowledge is offline-first. The app can save manual notes and import Markdown, JSON, or text files as `KnowledgeSource` records in SQLite.

Knowledge sources store:

- `source_type`: `manual_note`, `imported_file`, or future `web`
- title
- optional URL or imported file path
- retrieved timestamp
- content
- confidence/reliability note
- optional car, track, session type, and category

The Track Knowledge panel can:

- save manual car/track/setup/strategy notes
- import local notes files
- search saved knowledge
- delete outdated notes

Coach answers keep source labels separate:

- `Telemetry evidence`
- `Session evidence`
- `Stored track notes`
- `External research`

When no web provider is configured, external research remains unavailable and core telemetry coaching still works offline.

## Build And Run

```powershell
dotnet restore RaceEngineer.sln
dotnet build RaceEngineer.sln
dotnet run --project tests/RaceEngineer.SmokeTests/RaceEngineer.SmokeTests.csproj
dotnet run --project src/RaceEngineer.Desktop.Wpf/RaceEngineer.Desktop.Wpf.csproj
```

PowerShell scripts are included:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
powershell -ExecutionPolicy Bypass -File .\test.ps1
powershell -ExecutionPolicy Bypass -File .\publish.ps1
```

`build.ps1` restores and builds Release. `test.ps1` builds Release and runs the C# smoke tests. `publish.ps1` creates a self-contained Windows x64 folder.

The published app is written to:

```text
artifacts\publish\win-x64\RaceEngineer.Desktop.Wpf.exe
```

Equivalent direct publish command:

```powershell
dotnet publish src/RaceEngineer.Desktop.Wpf/RaceEngineer.Desktop.Wpf.csproj -c Release -r win-x64 --self-contained true -o artifacts\publish\win-x64
```

## App Settings

Runtime settings live in `appsettings.json` next to the WPF executable. Defaults:

```json
{
  "udpBindIp": "127.0.0.1",
  "udpPort": 20999,
  "databasePath": "%LOCALAPPDATA%\\RaceEngineer\\race_engineer.sqlite3",
  "voiceEnabledDefault": false,
  "captureFolder": "%LOCALAPPDATA%\\RaceEngineer\\Debug",
  "replayFolder": "%LOCALAPPDATA%\\RaceEngineer\\Replays"
}
```

If the settings file is missing, malformed, or invalid, the app falls back to safe defaults and reports a startup warning in chat.

SQLite data is stored by default at:

```text
%LOCALAPPDATA%\RaceEngineer\race_engineer.sqlite3
```

Raw packet captures are stored by default under:

```text
%LOCALAPPDATA%\RaceEngineer\Debug
```

## Release Checks

Before sharing a build:

1. Run `powershell -ExecutionPolicy Bypass -File .\test.ps1`.
2. Run `powershell -ExecutionPolicy Bypass -File .\publish.ps1`.
3. Launch `artifacts\publish\win-x64\RaceEngineer.Desktop.Wpf.exe`.
4. Confirm Telemetry Diagnostics shows receiver `Running`.
5. Start SimHub UDP output to `127.0.0.1:20999`.
6. Confirm valid packet count increases and schema shows `acevo_engineer.simhub_datacore v1`.

Startup errors are surfaced in the app chat:

- UDP port already in use: check the bind address/port and stop the other process.
- DB cannot open: check `databasePath` permissions.
- Voice/TTS unavailable: app continues with speech output disabled.
- Invalid settings: app falls back to defaults.
