from __future__ import annotations

from collections import deque

from race_engineer_app.models import EventSeverity, EventType, TelemetryEvent, TelemetrySnapshot


class EventEngine:
    def __init__(self) -> None:
        self.history: deque[TelemetrySnapshot] = deque(maxlen=30)
        self.current_lap = 0
        self._last_lap_progress: float | None = None
        self._throttle_hesitation_ticks = 0

    def process(self, snapshot: TelemetrySnapshot) -> list[TelemetryEvent]:
        events: list[TelemetryEvent] = []
        previous = self.history[-1] if self.history else None
        lap_progress = snapshot.lap.lap_progress

        if previous and previous.lap.lap_progress is not None and lap_progress is not None:
            if previous.lap.lap_progress > 0.92 and lap_progress < 0.08:
                events.append(self._event(snapshot, EventType.lap_end, EventSeverity.info, 0.95, {"previous_progress": previous.lap.lap_progress}, "Review the completed lap for repeatable gains."))
                self.current_lap += 1
                events.append(self._event(snapshot, EventType.lap_start, EventSeverity.info, 0.95, {"lap_number": self.current_lap}, "Build the next lap cleanly from the entry phase."))

        brake = snapshot.inputs.brake
        throttle = snapshot.inputs.throttle
        steering = snapshot.inputs.steering
        speed = snapshot.car.speed_kmh

        if brake is not None and brake >= 0.82 and speed is not None and speed >= 80:
            events.append(self._event(snapshot, EventType.heavy_braking, EventSeverity.info, 0.8, {"brake": brake, "speed_kmh": speed}, "Keep the initial hit firm, then release progressively."))

        if previous and brake is not None and previous.inputs.brake is not None:
            brake_delta = brake - previous.inputs.brake
            if previous.inputs.brake >= 0.55 and brake_delta <= -0.45:
                events.append(self._event(snapshot, EventType.abrupt_brake_release, EventSeverity.advisory, 0.82, {"brake_delta": brake_delta, "previous_brake": previous.inputs.brake, "brake": brake}, "Smooth the brake trail-off to keep the platform settled."))
            if abs(brake_delta) >= 0.28 and previous.inputs.brake >= 0.25 and brake >= 0.25:
                events.append(self._event(snapshot, EventType.unstable_braking, EventSeverity.advisory, 0.72, {"brake_delta": brake_delta, "previous_brake": previous.inputs.brake, "brake": brake}, "Hold brake pressure steadier before turn-in."))

        if throttle is not None and brake is not None and steering is not None:
            if throttle >= 0.25 and abs(steering) >= 0.35 and brake <= 0.08:
                events.append(self._event(snapshot, EventType.early_throttle_with_steering, EventSeverity.advisory, 0.78, {"throttle": throttle, "steering": steering}, "Wait for steering to unwind before adding more throttle."))

        if steering is not None and speed is not None and abs(steering) >= 0.72 and speed >= 90:
            events.append(self._event(snapshot, EventType.steering_overuse, EventSeverity.advisory, 0.7, {"steering": steering, "speed_kmh": speed}, "Reduce steering angle and let the car rotate on entry."))

        if previous and throttle is not None and previous.inputs.throttle is not None:
            if 0.08 <= throttle <= 0.22 and abs(throttle - previous.inputs.throttle) <= 0.04 and brake is not None and brake <= 0.05:
                self._throttle_hesitation_ticks += 1
            else:
                self._throttle_hesitation_ticks = 0
            if self._throttle_hesitation_ticks == 5:
                events.append(self._event(snapshot, EventType.throttle_hesitation, EventSeverity.advisory, 0.65, {"throttle": throttle, "ticks": self._throttle_hesitation_ticks}, "Commit earlier once the car is pointed."))

        if previous and throttle is not None and steering is not None and speed is not None:
            prev_speed = previous.car.speed_kmh
            if prev_speed is not None and throttle >= 0.65 and abs(steering) >= 0.28 and speed + 1.0 < prev_speed:
                events.append(self._event(snapshot, EventType.traction_loss, EventSeverity.warning, 0.62, {"throttle": throttle, "steering": steering, "speed_delta_kmh": speed - prev_speed}, "Reduce throttle pickup until rear grip is stable."))

        max_tyre_temp = _max_or_none(snapshot.condition.tyre_temp_c)
        if max_tyre_temp is not None and max_tyre_temp >= 105:
            events.append(self._event(snapshot, EventType.tyre_overheating, EventSeverity.warning, 0.9, {"max_tyre_temp_c": max_tyre_temp}, "Reduce sliding and protect the hot tyre."))

        max_brake_temp = _max_or_none(snapshot.condition.brake_temp_c)
        if max_brake_temp is not None and max_brake_temp >= 850:
            events.append(self._event(snapshot, EventType.brake_overheating, EventSeverity.warning, 0.9, {"max_brake_temp_c": max_brake_temp}, "Open the braking phase slightly and avoid dragging the pedal."))

        fuel = snapshot.condition.fuel
        if fuel is not None and fuel <= 3.0:
            events.append(self._event(snapshot, EventType.low_fuel, EventSeverity.critical, 0.86, {"fuel": fuel}, "Fuel is tight; lift earlier into heavy braking zones."))

        if snapshot.race.flags:
            events.append(self._event(snapshot, EventType.invalid_lap_or_flags, EventSeverity.warning, 0.8, {"flags": snapshot.race.flags}, "Respect race control and treat lap data as uncertain."))

        self.history.append(snapshot)
        self._last_lap_progress = lap_progress
        return events

    def _event(
        self,
        snapshot: TelemetrySnapshot,
        event_type: EventType,
        severity: EventSeverity,
        confidence: float,
        evidence: dict,
        suggested_action: str,
    ) -> TelemetryEvent:
        return TelemetryEvent(
            type=event_type,
            severity=severity,
            confidence=confidence,
            timestamp=snapshot.timestamp,
            lap_progress=snapshot.lap.lap_progress,
            lap_number=snapshot.lap.lap_number or self.current_lap,
            evidence=evidence,
            suggested_action=suggested_action,
        )


def _max_or_none(values: list[float | None] | None) -> float | None:
    if not values:
        return None
    concrete = [value for value in values if value is not None]
    if not concrete:
        return None
    return max(concrete)

