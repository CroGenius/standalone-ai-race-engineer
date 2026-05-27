from race_engineer_app.provider import normalize_simhub_payload


def test_normalize_missing_values_are_none() -> None:
    snapshot = normalize_simhub_payload(
        {
            "schema": "acevo_engineer.simhub_datacore",
            "schema_version": 1,
            "speed_kmh": "",
            "gear": "bad",
        }
    )

    assert snapshot.car.speed_kmh is None
    assert snapshot.car.gear is None
    assert snapshot.inputs.throttle is None
    assert snapshot.condition.tyre_temp_c is None


def test_normalize_expected_fields() -> None:
    snapshot = normalize_simhub_payload(
        {
            "schema": "acevo_engineer.simhub_datacore",
            "schema_version": 1,
            "speed_kmh": 180.5,
            "rpm": "7200",
            "gear": "4",
            "throttle": 0.7,
            "brake": 0.1,
            "steering": -0.2,
            "lap_progress": 0.45,
            "tyre_temp_c": [90, None, "91.5", "bad"],
        }
    )

    assert snapshot.source.provider == "simhub_datacore"
    assert snapshot.car.speed_kmh == 180.5
    assert snapshot.car.rpm == 7200
    assert snapshot.car.gear == 4
    assert snapshot.inputs.steering == -0.2
    assert snapshot.condition.tyre_temp_c == [90.0, None, 91.5, None]

