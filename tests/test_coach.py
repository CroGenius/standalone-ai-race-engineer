from race_engineer_app.coach import CoachService
from race_engineer_app.storage import SessionStore


def test_coach_states_uncertainty_without_telemetry(tmp_path) -> None:
    store = SessionStore(tmp_path / "test.sqlite3")
    session_id = store.create_session()
    coach = CoachService(store)

    answer = coach.answer_chat(session_id, "How is fuel?")

    assert "do not have a fuel value" in answer.content
    assert answer.uncertainty is not None

