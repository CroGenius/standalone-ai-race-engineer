from __future__ import annotations

from datetime import datetime, timezone
from enum import Enum
from typing import Any, Literal
from uuid import uuid4

from pydantic import BaseModel, ConfigDict, Field


ProviderName = Literal["simhub_datacore", "manual", "replay"]


class SourceInfo(BaseModel):
    model_config = ConfigDict(populate_by_name=True)

    provider: ProviderName
    telemetry_schema: str | None = Field(default=None, alias="schema")
    schema_version: int | None = None


class CarState(BaseModel):
    speed_kmh: float | None = None
    rpm: float | None = None
    gear: int | None = None
    damage: Any | None = None


class DriverInputs(BaseModel):
    throttle: float | None = None
    brake: float | None = None
    steering: float | None = None


class LapState(BaseModel):
    lap_time_ms: int | None = None
    lap_time_s: float | None = None
    lap_progress: float | None = None
    lap_number: int | None = None
    valid_lap: bool | None = None


class RaceState(BaseModel):
    position: int | None = None
    flags: Any | None = None


class TyreBrakeFuelState(BaseModel):
    fuel: float | None = None
    tyre_temp_c: list[float | None] | None = None
    tyre_pressure: list[float | None] | None = None
    brake_temp_c: list[float | None] | None = None
    tyre_wear: list[float | None] | None = None


class TelemetrySnapshot(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    timestamp: datetime = Field(default_factory=lambda: datetime.now(timezone.utc))
    source: SourceInfo
    car: CarState = Field(default_factory=CarState)
    inputs: DriverInputs = Field(default_factory=DriverInputs)
    lap: LapState = Field(default_factory=LapState)
    race: RaceState = Field(default_factory=RaceState)
    condition: TyreBrakeFuelState = Field(default_factory=TyreBrakeFuelState)


class EventSeverity(str, Enum):
    info = "info"
    advisory = "advisory"
    warning = "warning"
    critical = "critical"


class EventType(str, Enum):
    heavy_braking = "heavy_braking"
    unstable_braking = "unstable_braking"
    abrupt_brake_release = "abrupt_brake_release"
    throttle_hesitation = "throttle_hesitation"
    early_throttle_with_steering = "early_throttle_with_steering"
    steering_overuse = "steering_overuse"
    traction_loss = "traction_loss"
    tyre_overheating = "tyre_overheating"
    brake_overheating = "brake_overheating"
    low_fuel = "low_fuel"
    invalid_lap_or_flags = "invalid_lap_or_flags"
    lap_start = "lap_start"
    lap_end = "lap_end"


class TelemetryEvent(BaseModel):
    id: str = Field(default_factory=lambda: str(uuid4()))
    type: EventType
    severity: EventSeverity
    confidence: float = Field(ge=0.0, le=1.0)
    timestamp: datetime
    lap_progress: float | None = None
    lap_number: int | None = None
    evidence: dict[str, Any] = Field(default_factory=dict)
    suggested_action: str


class SessionSummary(BaseModel):
    session_id: str
    started_at: datetime
    ended_at: datetime | None = None
    snapshot_count: int = 0
    event_count: int = 0
    recurring_mistakes: list[str] = Field(default_factory=list)
    tyre_brake_fuel_summary: list[str] = Field(default_factory=list)
    improvement_plan: list[str] = Field(default_factory=list)


class DriverProfile(BaseModel):
    driver_id: str = "default"
    name: str | None = None
    strengths: list[str] = Field(default_factory=list)
    focus_areas: list[str] = Field(default_factory=list)
    reminders: list[str] = Field(default_factory=list)


class CoachMessage(BaseModel):
    role: Literal["user", "coach", "system"]
    content: str
    grounded_event_ids: list[str] = Field(default_factory=list)
    uncertainty: str | None = None
