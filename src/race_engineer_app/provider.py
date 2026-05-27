from __future__ import annotations

import asyncio
import json
from collections.abc import Callable
from datetime import datetime, timezone
from typing import Any

from race_engineer_app.models import (
    CarState,
    DriverInputs,
    LapState,
    RaceState,
    SourceInfo,
    TelemetrySnapshot,
    TyreBrakeFuelState,
)


EXPECTED_SCHEMA = "acevo_engineer.simhub_datacore"
EXPECTED_SCHEMA_VERSION = 1


def _float_or_none(value: Any) -> float | None:
    if value is None or value == "":
        return None
    try:
        return float(value)
    except (TypeError, ValueError):
        return None


def _int_or_none(value: Any) -> int | None:
    if value is None or value == "":
        return None
    try:
        return int(value)
    except (TypeError, ValueError):
        return None


def _float_list_or_none(value: Any) -> list[float | None] | None:
    if value is None:
        return None
    if not isinstance(value, list):
        return None
    return [_float_or_none(item) for item in value]


def normalize_simhub_payload(payload: dict[str, Any]) -> TelemetrySnapshot:
    timestamp = datetime.now(timezone.utc)
    schema = payload.get("schema")
    schema_version = _int_or_none(payload.get("schema_version"))

    return TelemetrySnapshot(
        timestamp=timestamp,
        source=SourceInfo(provider="simhub_datacore", telemetry_schema=schema, schema_version=schema_version),
        car=CarState(
            speed_kmh=_float_or_none(payload.get("speed_kmh")),
            rpm=_float_or_none(payload.get("rpm")),
            gear=_int_or_none(payload.get("gear")),
            damage=payload.get("damage"),
        ),
        inputs=DriverInputs(
            throttle=_float_or_none(payload.get("throttle")),
            brake=_float_or_none(payload.get("brake")),
            steering=_float_or_none(payload.get("steering")),
        ),
        lap=LapState(
            lap_time_ms=_int_or_none(payload.get("lap_time_ms")),
            lap_time_s=_float_or_none(payload.get("lap_time_s")),
            lap_progress=_float_or_none(payload.get("lap_progress")),
        ),
        race=RaceState(position=_int_or_none(payload.get("position")), flags=payload.get("flags")),
        condition=TyreBrakeFuelState(
            fuel=_float_or_none(payload.get("fuel")),
            tyre_temp_c=_float_list_or_none(payload.get("tyre_temp_c")),
            tyre_pressure=_float_list_or_none(payload.get("tyre_pressure")),
            brake_temp_c=_float_list_or_none(payload.get("brake_temp_c")),
            tyre_wear=_float_list_or_none(payload.get("tyre_wear")),
        ),
    )


class SimHubUdpProtocol(asyncio.DatagramProtocol):
    def __init__(self, on_snapshot: Callable[[TelemetrySnapshot], None]):
        self.on_snapshot = on_snapshot

    def datagram_received(self, data: bytes, addr: tuple[str, int]) -> None:
        try:
            payload = json.loads(data.decode("utf-8"))
        except (UnicodeDecodeError, json.JSONDecodeError):
            return
        if not isinstance(payload, dict):
            return
        snapshot = normalize_simhub_payload(payload)
        self.on_snapshot(snapshot)


class SimHubUdpReceiver:
    def __init__(self, host: str = "127.0.0.1", port: int = 20999):
        self.host = host
        self.port = port
        self._transport: asyncio.DatagramTransport | None = None

    async def start(self, on_snapshot: Callable[[TelemetrySnapshot], None]) -> None:
        loop = asyncio.get_running_loop()
        transport, _ = await loop.create_datagram_endpoint(
            lambda: SimHubUdpProtocol(on_snapshot),
            local_addr=(self.host, self.port),
        )
        self._transport = transport

    def stop(self) -> None:
        if self._transport is not None:
            self._transport.close()
            self._transport = None
