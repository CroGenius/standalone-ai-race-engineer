from race_engineer_app.events import EventEngine
from race_engineer_app.models import EventType
from race_engineer_app.provider import normalize_simhub_payload


def test_abrupt_brake_release_event_has_evidence() -> None:
    engine = EventEngine()
    first = normalize_simhub_payload({"brake": 0.9, "speed_kmh": 140, "lap_progress": 0.2})
    second = normalize_simhub_payload({"brake": 0.2, "speed_kmh": 138, "lap_progress": 0.21})

    engine.process(first)
    events = engine.process(second)

    abrupt = [event for event in events if event.type == EventType.abrupt_brake_release]
    assert abrupt
    assert abrupt[0].evidence["previous_brake"] == 0.9
    assert abrupt[0].suggested_action


def test_temperature_and_fuel_events() -> None:
    engine = EventEngine()
    snapshot = normalize_simhub_payload(
        {
            "fuel": 2.5,
            "tyre_temp_c": [101, 106, 99, 98],
            "brake_temp_c": [700, 860, 720, 730],
            "lap_progress": 0.5,
        }
    )

    event_types = {event.type for event in engine.process(snapshot)}

    assert EventType.low_fuel in event_types
    assert EventType.tyre_overheating in event_types
    assert EventType.brake_overheating in event_types

