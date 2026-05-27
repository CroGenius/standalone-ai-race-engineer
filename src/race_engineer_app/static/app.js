const statusEl = document.querySelector("#telemetryStatus");
const sessionLabel = document.querySelector("#sessionLabel");
const telemetryGrid = document.querySelector("#telemetryGrid");
const eventLog = document.querySelector("#eventLog");
const callout = document.querySelector("#callout");
const chatForm = document.querySelector("#chatForm");
const chatInput = document.querySelector("#chatInput");
const chatLog = document.querySelector("#chatLog");
const noteForm = document.querySelector("#noteForm");
const noteKind = document.querySelector("#noteKind");
const noteContent = document.querySelector("#noteContent");
const notesEl = document.querySelector("#notes");
const summaryButton = document.querySelector("#summaryButton");
const summaryEl = document.querySelector("#summary");
const muteButton = document.querySelector("#muteButton");
const pttButton = document.querySelector("#pttButton");

let ttsMuted = true;

function valueOrDash(value, digits = null) {
  if (value === null || value === undefined) return "-";
  if (typeof value === "number" && digits !== null) return value.toFixed(digits);
  return String(value);
}

function renderStatus(data) {
  sessionLabel.textContent = `Session ${data.session_id}`;
  statusEl.textContent = data.telemetry_online ? "Telemetry online" : "Telemetry offline";
  statusEl.classList.toggle("online", data.telemetry_online);

  const snapshot = data.latest_snapshot;
  const rows = snapshot
    ? [
        ["Speed", `${valueOrDash(snapshot.car.speed_kmh, 1)} km/h`],
        ["RPM", valueOrDash(snapshot.car.rpm, 0)],
        ["Gear", valueOrDash(snapshot.car.gear)],
        ["Throttle", valueOrDash(snapshot.inputs.throttle, 2)],
        ["Brake", valueOrDash(snapshot.inputs.brake, 2)],
        ["Steering", valueOrDash(snapshot.inputs.steering, 2)],
        ["Lap time", `${valueOrDash(snapshot.lap.lap_time_s, 3)} s`],
        ["Lap progress", valueOrDash(snapshot.lap.lap_progress, 3)],
        ["Fuel", valueOrDash(snapshot.condition.fuel, 1)],
        ["Position", valueOrDash(snapshot.race.position)],
      ]
    : [["State", "Waiting for UDP telemetry"]];

  telemetryGrid.innerHTML = rows.map(([label, value]) => `<dt>${label}</dt><dd>${value}</dd>`).join("");

  eventLog.innerHTML = data.recent_events
    .slice()
    .reverse()
    .map(
      (event) =>
        `<li><strong class="${event.severity}">${event.type}</strong><br>${event.suggested_action}<br><small>Lap ${valueOrDash(event.lap_number)} / ${valueOrDash(event.lap_progress, 3)}</small></li>`,
    )
    .join("");

  if (data.last_callout) {
    callout.textContent = data.last_callout.content;
    speak(data.last_callout.content);
  }
}

async function refreshStatus() {
  const response = await fetch("/api/status");
  renderStatus(await response.json());
}

function addMessage(role, content, uncertainty = null) {
  const item = document.createElement("div");
  item.className = `message ${role}`;
  item.textContent = uncertainty ? `${content} (${uncertainty})` : content;
  chatLog.appendChild(item);
  chatLog.scrollTop = chatLog.scrollHeight;
}

function speak(text) {
  if (ttsMuted || !("speechSynthesis" in window)) return;
  const utterance = new SpeechSynthesisUtterance(text);
  utterance.rate = 1.05;
  window.speechSynthesis.cancel();
  window.speechSynthesis.speak(utterance);
}

chatForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const message = chatInput.value.trim();
  if (!message) return;
  chatInput.value = "";
  addMessage("user", message);
  const response = await fetch("/api/chat", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ message }),
  });
  const answer = await response.json();
  addMessage("coach", answer.content, answer.uncertainty);
  speak(answer.content);
});

noteForm.addEventListener("submit", async (event) => {
  event.preventDefault();
  const content = noteContent.value.trim();
  if (!content) return;
  await fetch("/api/notes", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ kind: noteKind.value, content }),
  });
  noteContent.value = "";
  await refreshNotes();
});

async function refreshNotes() {
  const response = await fetch("/api/notes");
  const notes = await response.json();
  notesEl.innerHTML = notes
    .slice(0, 5)
    .map((note) => `<div class="note"><strong>${note.kind}</strong><br>${note.content}</div>`)
    .join("");
}

summaryButton.addEventListener("click", refreshSummary);

async function refreshSummary() {
  const response = await fetch("/api/summary");
  const summary = await response.json();
  summaryEl.innerHTML = [
    `Snapshots: ${summary.snapshot_count}`,
    `Events: ${summary.event_count}`,
    ...summary.recurring_mistakes,
    ...summary.tyre_brake_fuel_summary,
    ...summary.improvement_plan,
  ]
    .map((line) => `<div>${line}</div>`)
    .join("");
}

muteButton.addEventListener("click", () => {
  ttsMuted = !ttsMuted;
  muteButton.textContent = ttsMuted ? "TTS Muted" : "TTS On";
});

pttButton.addEventListener("click", () => {
  addMessage("coach", "Push-to-talk is reserved in this Phase 1 shell. Chat input is active.");
});

refreshStatus();
refreshNotes();
refreshSummary();
setInterval(refreshStatus, 1000);

