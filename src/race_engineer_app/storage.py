from __future__ import annotations

import json
import sqlite3
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from uuid import uuid4

from race_engineer_app.models import DriverProfile, TelemetryEvent, TelemetrySnapshot


class SessionStore:
    def __init__(self, path: Path | str = "race_engineer.sqlite3") -> None:
        self.path = Path(path)
        self._conn = sqlite3.connect(self.path, check_same_thread=False)
        self._conn.row_factory = sqlite3.Row
        self._init_db()

    def _init_db(self) -> None:
        self._conn.executescript(
            """
            CREATE TABLE IF NOT EXISTS sessions (
              id TEXT PRIMARY KEY,
              started_at TEXT NOT NULL,
              ended_at TEXT,
              track TEXT,
              car TEXT,
              mode TEXT NOT NULL DEFAULT 'live'
            );
            CREATE TABLE IF NOT EXISTS snapshots (
              id TEXT PRIMARY KEY,
              session_id TEXT NOT NULL,
              timestamp TEXT NOT NULL,
              payload_json TEXT NOT NULL,
              FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS events (
              id TEXT PRIMARY KEY,
              session_id TEXT NOT NULL,
              timestamp TEXT NOT NULL,
              type TEXT NOT NULL,
              severity TEXT NOT NULL,
              payload_json TEXT NOT NULL,
              FOREIGN KEY(session_id) REFERENCES sessions(id)
            );
            CREATE TABLE IF NOT EXISTS notes (
              id TEXT PRIMARY KEY,
              kind TEXT NOT NULL,
              track TEXT,
              car TEXT,
              content TEXT NOT NULL,
              created_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS driver_profile (
              driver_id TEXT PRIMARY KEY,
              payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS metadata (
              id TEXT PRIMARY KEY,
              kind TEXT NOT NULL,
              name TEXT NOT NULL,
              payload_json TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS coaching_preferences (
              id TEXT PRIMARY KEY,
              payload_json TEXT NOT NULL
            );
            """
        )
        self._conn.commit()

    def create_session(self, track: str | None = None, car: str | None = None, mode: str = "live") -> str:
        session_id = str(uuid4())
        self._conn.execute(
            "INSERT INTO sessions (id, started_at, track, car, mode) VALUES (?, ?, ?, ?, ?)",
            (session_id, _now(), track, car, mode),
        )
        self._conn.commit()
        return session_id

    def end_session(self, session_id: str) -> None:
        self._conn.execute("UPDATE sessions SET ended_at = ? WHERE id = ?", (_now(), session_id))
        self._conn.commit()

    def save_snapshot(self, session_id: str, snapshot: TelemetrySnapshot) -> None:
        self._conn.execute(
            "INSERT INTO snapshots (id, session_id, timestamp, payload_json) VALUES (?, ?, ?, ?)",
            (snapshot.id, session_id, snapshot.timestamp.isoformat(), snapshot.model_dump_json(by_alias=True)),
        )
        self._conn.commit()

    def save_events(self, session_id: str, events: list[TelemetryEvent]) -> None:
        if not events:
            return
        self._conn.executemany(
            "INSERT INTO events (id, session_id, timestamp, type, severity, payload_json) VALUES (?, ?, ?, ?, ?, ?)",
            [
                (
                    event.id,
                    session_id,
                    event.timestamp.isoformat(),
                    event.type.value,
                    event.severity.value,
                    event.model_dump_json(),
                )
                for event in events
            ],
        )
        self._conn.commit()

    def recent_events(self, session_id: str, limit: int = 30) -> list[TelemetryEvent]:
        rows = self._conn.execute(
            "SELECT payload_json FROM events WHERE session_id = ? ORDER BY timestamp DESC LIMIT ?",
            (session_id, limit),
        ).fetchall()
        return [TelemetryEvent.model_validate_json(row["payload_json"]) for row in reversed(rows)]

    def latest_snapshot(self, session_id: str) -> TelemetrySnapshot | None:
        row = self._conn.execute(
            "SELECT payload_json FROM snapshots WHERE session_id = ? ORDER BY timestamp DESC LIMIT 1",
            (session_id,),
        ).fetchone()
        if row is None:
            return None
        return TelemetrySnapshot.model_validate_json(row["payload_json"])

    def add_note(self, kind: str, content: str, track: str | None = None, car: str | None = None) -> str:
        note_id = str(uuid4())
        self._conn.execute(
            "INSERT INTO notes (id, kind, track, car, content, created_at) VALUES (?, ?, ?, ?, ?, ?)",
            (note_id, kind, track, car, content, _now()),
        )
        self._conn.commit()
        return note_id

    def list_notes(self, kind: str | None = None) -> list[dict[str, Any]]:
        if kind is None:
            rows = self._conn.execute("SELECT * FROM notes ORDER BY created_at DESC").fetchall()
        else:
            rows = self._conn.execute("SELECT * FROM notes WHERE kind = ? ORDER BY created_at DESC", (kind,)).fetchall()
        return [dict(row) for row in rows]

    def get_driver_profile(self, driver_id: str = "default") -> DriverProfile:
        row = self._conn.execute("SELECT payload_json FROM driver_profile WHERE driver_id = ?", (driver_id,)).fetchone()
        if row is None:
            return DriverProfile(driver_id=driver_id)
        return DriverProfile.model_validate_json(row["payload_json"])

    def save_driver_profile(self, profile: DriverProfile) -> None:
        self._conn.execute(
            "INSERT OR REPLACE INTO driver_profile (driver_id, payload_json) VALUES (?, ?)",
            (profile.driver_id, profile.model_dump_json()),
        )
        self._conn.commit()

    def event_counts(self, session_id: str) -> dict[str, int]:
        rows = self._conn.execute(
            "SELECT type, COUNT(*) AS count FROM events WHERE session_id = ? GROUP BY type ORDER BY count DESC",
            (session_id,),
        ).fetchall()
        return {row["type"]: row["count"] for row in rows}

    def session_counts(self, session_id: str) -> tuple[int, int]:
        snapshots = self._conn.execute("SELECT COUNT(*) AS count FROM snapshots WHERE session_id = ?", (session_id,)).fetchone()["count"]
        events = self._conn.execute("SELECT COUNT(*) AS count FROM events WHERE session_id = ?", (session_id,)).fetchone()["count"]
        return int(snapshots), int(events)

    def session_started_at(self, session_id: str) -> datetime:
        row = self._conn.execute("SELECT started_at FROM sessions WHERE id = ?", (session_id,)).fetchone()
        if row is None:
            return datetime.now(timezone.utc)
        return datetime.fromisoformat(row["started_at"])


def _now() -> str:
    return datetime.now(timezone.utc).isoformat()
