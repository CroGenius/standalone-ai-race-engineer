from __future__ import annotations

from pathlib import Path
from typing import Any

import uvicorn
from fastapi import FastAPI, WebSocket, WebSocketDisconnect
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel

from race_engineer_app.coach import CoachService
from race_engineer_app.events import EventEngine
from race_engineer_app.models import CoachMessage, DriverProfile, TelemetryEvent, TelemetrySnapshot
from race_engineer_app.provider import SimHubUdpReceiver, normalize_simhub_payload
from race_engineer_app.storage import SessionStore


APP_DIR = Path(__file__).resolve().parent
STATIC_DIR = APP_DIR / "static"


class AppState:
    def __init__(self) -> None:
        data_dir = Path.cwd() / "data"
        data_dir.mkdir(exist_ok=True)
        self.store = SessionStore(data_dir / "race_engineer.sqlite3")
        self.coach = CoachService(self.store)
        self.events = EventEngine()
        self.receiver = SimHubUdpReceiver()
        self.session_id = self.store.create_session(mode="live")
        self.latest_snapshot: TelemetrySnapshot | None = None
        self.recent_events: list[TelemetryEvent] = []
        self.last_callout: CoachMessage | None = None
        self.clients: set[WebSocket] = set()
        self.telemetry_online = False


class ChatRequest(BaseModel):
    message: str


class NoteRequest(BaseModel):
    kind: str
    content: str
    track: str | None = None
    car: str | None = None


state = AppState()
app = FastAPI(title="Standalone AI Race Engineer")
app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")


@app.on_event("startup")
async def startup() -> None:
    await state.receiver.start(handle_snapshot)


@app.on_event("shutdown")
async def shutdown() -> None:
    state.receiver.stop()


@app.get("/")
async def index() -> FileResponse:
    return FileResponse(STATIC_DIR / "index.html")


@app.get("/api/status")
async def status() -> dict[str, Any]:
    return _status_payload()


@app.post("/api/chat")
async def chat(request: ChatRequest) -> CoachMessage:
    return state.coach.answer_chat(state.session_id, request.message)


@app.post("/api/notes")
async def add_note(request: NoteRequest) -> dict[str, str]:
    note_id = state.store.add_note(request.kind, request.content, request.track, request.car)
    return {"id": note_id}


@app.get("/api/notes")
async def list_notes(kind: str | None = None) -> list[dict[str, Any]]:
    return state.store.list_notes(kind)


@app.get("/api/summary")
async def summary() -> dict[str, Any]:
    return state.coach.build_session_summary(state.session_id).model_dump(mode="json")


@app.get("/api/profile")
async def get_profile() -> DriverProfile:
    return state.store.get_driver_profile()


@app.post("/api/profile")
async def save_profile(profile: DriverProfile) -> DriverProfile:
    state.store.save_driver_profile(profile)
    return profile


@app.post("/api/dev/telemetry")
async def dev_telemetry(payload: dict[str, Any]) -> dict[str, Any]:
    snapshot = normalize_simhub_payload(payload)
    handle_snapshot(snapshot)
    return _status_payload()


@app.websocket("/ws")
async def websocket_endpoint(websocket: WebSocket) -> None:
    await websocket.accept()
    state.clients.add(websocket)
    await websocket.send_json(_status_payload())
    try:
        while True:
            await websocket.receive_text()
    except WebSocketDisconnect:
        state.clients.remove(websocket)


def handle_snapshot(snapshot: TelemetrySnapshot) -> None:
    state.telemetry_online = True
    state.latest_snapshot = snapshot
    state.store.save_snapshot(state.session_id, snapshot)
    events = state.events.process(snapshot)
    state.store.save_events(state.session_id, events)
    if events:
        state.recent_events = (state.recent_events + events)[-50:]
        callout = state.coach.choose_live_callout(events)
        if callout is not None:
            state.last_callout = callout
    # WebSocket push is handled opportunistically from the next API poll in this sync callback.


def _status_payload() -> dict[str, Any]:
    return {
        "session_id": state.session_id,
        "telemetry_online": state.telemetry_online,
        "latest_snapshot": state.latest_snapshot.model_dump(mode="json", by_alias=True) if state.latest_snapshot else None,
        "recent_events": [event.model_dump(mode="json") for event in state.recent_events[-20:]],
        "last_callout": state.last_callout.model_dump(mode="json") if state.last_callout else None,
    }


def run() -> None:
    uvicorn.run("race_engineer_app.app:app", host="127.0.0.1", port=8765, reload=False)
