from __future__ import annotations

from collections import deque
from datetime import datetime, timezone

from race_engineer_app.models import CoachMessage, EventSeverity, EventType, SessionSummary, TelemetryEvent, TelemetrySnapshot
from race_engineer_app.storage import SessionStore


PRIORITY_ORDER = {
    EventSeverity.critical: 0,
    EventSeverity.warning: 1,
    EventSeverity.advisory: 2,
    EventSeverity.info: 3,
}


class CoachService:
    def __init__(self, store: SessionStore) -> None:
        self.store = store
        self.callout_cooldowns: dict[EventType, datetime] = {}
        self.chat_history: deque[CoachMessage] = deque(maxlen=100)

    def choose_live_callout(self, events: list[TelemetryEvent]) -> CoachMessage | None:
        if not events:
            return None
        event = sorted(events, key=lambda item: PRIORITY_ORDER[item.severity])[0]
        return CoachMessage(role="coach", content=event.suggested_action, grounded_event_ids=[event.id])

    def answer_chat(self, session_id: str, user_message: str) -> CoachMessage:
        snapshot = self.store.latest_snapshot(session_id)
        events = self.store.recent_events(session_id, limit=8)
        lowered = user_message.lower()

        if "fuel" in lowered:
            response = self._fuel_answer(snapshot, events)
        elif "tyre" in lowered or "tire" in lowered:
            response = self._tyre_answer(snapshot, events)
        elif "brake" in lowered:
            response = self._brake_answer(snapshot, events)
        elif "summary" in lowered or "review" in lowered:
            summary = self.build_session_summary(session_id)
            response = CoachMessage(
                role="coach",
                content="Session summary: "
                + "; ".join(summary.recurring_mistakes + summary.tyre_brake_fuel_summary + summary.improvement_plan),
                uncertainty=None if summary.event_count else "No telemetry events have been recorded yet.",
            )
        else:
            response = self._general_answer(snapshot, events)

        self.chat_history.append(CoachMessage(role="user", content=user_message))
        self.chat_history.append(response)
        return response

    def build_session_summary(self, session_id: str) -> SessionSummary:
        counts = self.store.event_counts(session_id)
        snapshot_count, event_count = self.store.session_counts(session_id)
        recurring = []
        if counts.get(EventType.abrupt_brake_release.value, 0) >= 3:
            recurring.append("Brake release is repeatedly too abrupt.")
        if counts.get(EventType.early_throttle_with_steering.value, 0) >= 3:
            recurring.append("Throttle is often applied while steering lock remains high.")
        if counts.get(EventType.steering_overuse.value, 0) >= 3:
            recurring.append("Steering angle is repeatedly high at speed.")
        if counts.get(EventType.throttle_hesitation.value, 0) >= 2:
            recurring.append("Throttle commitment is hesitant on exits.")

        condition = []
        if counts.get(EventType.tyre_overheating.value, 0):
            condition.append("Tyre temperatures exceeded the configured overheating threshold.")
        if counts.get(EventType.brake_overheating.value, 0):
            condition.append("Brake temperatures exceeded the configured overheating threshold.")
        if counts.get(EventType.low_fuel.value, 0):
            condition.append("Fuel reached a critical low threshold.")

        plan = []
        if recurring:
            plan.extend(["Pick one braking zone and smooth the final 40 percent of brake release.", "Delay throttle pickup until steering is unwinding on corner exit."])
        if not plan:
            plan.append("Collect more laps, then compare repeat events by corner and lap phase.")

        return SessionSummary(
            session_id=session_id,
            started_at=self.store.session_started_at(session_id),
            snapshot_count=snapshot_count,
            event_count=event_count,
            recurring_mistakes=recurring,
            tyre_brake_fuel_summary=condition,
            improvement_plan=plan,
        )

    def _fuel_answer(self, snapshot: TelemetrySnapshot | None, events: list[TelemetryEvent]) -> CoachMessage:
        fuel = snapshot.condition.fuel if snapshot else None
        related = [event.id for event in events if event.type == EventType.low_fuel]
        if fuel is None:
            return CoachMessage(role="coach", content="I do not have a fuel value in the current telemetry.", uncertainty="Fuel is missing from the latest snapshot.")
        if related:
            return CoachMessage(role="coach", content=f"Fuel is critical at {fuel:.1f}. Lift earlier into heavy braking zones.", grounded_event_ids=related)
        return CoachMessage(role="coach", content=f"Current fuel is {fuel:.1f}. I do not see a low-fuel event in the recent buffer.")

    def _tyre_answer(self, snapshot: TelemetrySnapshot | None, events: list[TelemetryEvent]) -> CoachMessage:
        temps = snapshot.condition.tyre_temp_c if snapshot else None
        related = [event.id for event in events if event.type == EventType.tyre_overheating]
        if not temps:
            return CoachMessage(role="coach", content="I do not have tyre temperature data in the current telemetry.", uncertainty="Tyre data is missing from the latest snapshot.")
        max_temp = max(temp for temp in temps if temp is not None) if any(temp is not None for temp in temps) else None
        if max_temp is None:
            return CoachMessage(role="coach", content="Tyre temperatures are present but empty or unknown.", uncertainty="Tyre temperature values are null.")
        if related:
            return CoachMessage(role="coach", content=f"Max tyre temperature is {max_temp:.1f} C. Reduce sliding on entry.", grounded_event_ids=related)
        return CoachMessage(role="coach", content=f"Max tyre temperature is {max_temp:.1f} C. No overheating event is active in the recent buffer.")

    def _brake_answer(self, snapshot: TelemetrySnapshot | None, events: list[TelemetryEvent]) -> CoachMessage:
        related = [event.id for event in events if event.type in {EventType.unstable_braking, EventType.abrupt_brake_release, EventType.brake_overheating}]
        if related:
            latest = next(event for event in reversed(events) if event.id in related)
            return CoachMessage(role="coach", content=latest.suggested_action, grounded_event_ids=[latest.id])
        brake = snapshot.inputs.brake if snapshot else None
        if brake is None:
            return CoachMessage(role="coach", content="I do not have brake input in the current telemetry.", uncertainty="Brake input is missing from the latest snapshot.")
        return CoachMessage(role="coach", content=f"Current brake input is {brake:.2f}. No recent braking issue is in the event buffer.")

    def _general_answer(self, snapshot: TelemetrySnapshot | None, events: list[TelemetryEvent]) -> CoachMessage:
        if events:
            latest = events[-1]
            return CoachMessage(role="coach", content=f"Most recent grounded observation: {latest.suggested_action}", grounded_event_ids=[latest.id])
        if snapshot is None:
            return CoachMessage(role="coach", content="I am waiting for telemetry before making coaching claims.", uncertainty="No live snapshot is available.")
        return CoachMessage(role="coach", content="Telemetry is live. I will call out only grounded events as they appear.")

