from __future__ import annotations

import copy
import uuid
from datetime import datetime, timezone
from pathlib import Path

from fastapi import Depends, FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles
from pydantic import BaseModel, EmailStr, Field

from .auth import create_session, current_user, hash_password, verify_password
from .db import connect, init_db, list_games, load_game, row_to_dict, save_game
from .game_engine import advance_world, apply_action, generate_game

STATIC_DIR = Path(__file__).resolve().parent / "static"

app = FastAPI(title="DeepDetect Game Platform")
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_methods=["*"],
    allow_headers=["*"],
)
app.mount("/static", StaticFiles(directory=STATIC_DIR), name="static")


class RegisterIn(BaseModel):
    name: str = Field(min_length=2, max_length=80)
    email: EmailStr
    password: str = Field(min_length=6, max_length=120)


class LoginIn(BaseModel):
    email: EmailStr
    password: str


class ActionIn(BaseModel):
    surface: str
    item_id: str
    choice: str
    custom_text: str | None = Field(default=None, max_length=900)


@app.on_event("startup")
def startup() -> None:
    init_db()


@app.get("/")
def index() -> FileResponse:
    return FileResponse(STATIC_DIR / "index.html")


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok", "app": "DeepDetect Game Platform"}


@app.post("/api/auth/register")
def register(payload: RegisterIn) -> dict:
    with connect() as con:
        existing = con.execute("SELECT id FROM users WHERE email = ?", (payload.email.lower(),)).fetchone()
        if existing:
            raise HTTPException(status_code=409, detail="Email is already registered")
        cur = con.execute(
            "INSERT INTO users (name, email, password_hash) VALUES (?, ?, ?)",
            (payload.name.strip(), payload.email.lower(), hash_password(payload.password)),
        )
        user_id = int(cur.lastrowid)
    token = create_session(user_id)
    return {"token": token, "user": {"id": user_id, "name": payload.name.strip(), "email": payload.email.lower()}}


@app.post("/api/auth/login")
def login(payload: LoginIn) -> dict:
    with connect() as con:
        row = con.execute("SELECT id, name, email, password_hash FROM users WHERE email = ?", (payload.email.lower(),)).fetchone()
    user = row_to_dict(row)
    if not user or not verify_password(payload.password, user["password_hash"]):
        raise HTTPException(status_code=401, detail="Invalid email or password")
    token = create_session(user["id"])
    return {"token": token, "user": {"id": user["id"], "name": user["name"], "email": user["email"]}}


@app.get("/api/me")
def me(user: dict = Depends(current_user)) -> dict:
    return {"user": user}


@app.get("/api/games")
def games(user: dict = Depends(current_user)) -> dict:
    return {"games": list_games(user["id"])}


@app.post("/api/game/generate")
def generate(user: dict = Depends(current_user)) -> dict:
    try:
        state = generate_game(user)
    except RuntimeError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    save_game(state["id"], user["id"], state)
    return {"game": state}


@app.get("/api/game/{game_id}")
def get_game(game_id: str, user: dict = Depends(current_user)) -> dict:
    state = load_game(game_id, user["id"])
    if not state:
        raise HTTPException(status_code=404, detail="Game not found")
    return {"game": state}


@app.post("/api/game/{game_id}/backup")
def backup_game(game_id: str, user: dict = Depends(current_user)) -> dict:
    state = load_game(game_id, user["id"])
    if not state:
        raise HTTPException(status_code=404, detail="Game not found")
    backup = copy.deepcopy(state)
    backup_id = str(uuid.uuid4())
    backup["id"] = backup_id
    backup["backup_of"] = game_id
    backup["backup_created_at"] = datetime.now(timezone.utc).isoformat()
    backup["title"] = f"Backup: {state.get('title', 'Untitled shift')}"
    backup.setdefault("action_log", []).append("Session backup created.")
    save_game(backup_id, user["id"], backup)
    return {"game": backup, "games": list_games(user["id"])}


@app.get("/api/game/{game_id}/debug")
def debug_game(game_id: str, user: dict = Depends(current_user)) -> dict:
    state = load_game(game_id, user["id"])
    if not state:
        raise HTTPException(status_code=404, detail="Game not found")
    return {
        "agent_mode": state.get("agent_mode"),
        "agent_model": state.get("agent_model"),
        "last_world_agent_mode": state.get("last_world_agent_mode"),
        "world_tick": state.get("world_tick", 0),
        "values": state.get("values", {}),
        "quests": state.get("quests", []),
        "news_truth": [
            {
                "id": item["id"],
                "truth_label": item.get("truth_label"),
                "decision": item.get("decision"),
                "correct": item.get("correct"),
            }
            for item in state.get("news_items", [])
        ],
        "email_modes": [
            {
                "id": item["id"],
                "selected": item.get("selected"),
                "resolved": item.get("resolved"),
                "chat_turns": item.get("chat_turns", 0),
                "min_turns": item.get("min_turns", 3),
                "max_turns": item.get("max_turns", 3),
                "correct_option": item.get("correct_option"),
                "reply_agent_mode": item.get("reply_agent_mode"),
            }
            for item in state.get("emails", [])
        ],
        "telegram_modes": [
            {
                "id": item["id"],
                "selected": item.get("selected"),
                "resolved": item.get("resolved"),
                "chat_turns": item.get("chat_turns", 0),
                "min_turns": item.get("min_turns", 3),
                "max_turns": item.get("max_turns", 3),
                "correct_option": item.get("correct_option"),
                "reply_agent_mode": item.get("reply_agent_mode"),
            }
            for item in state.get("telegram_threads", [])
        ],
    }


@app.post("/api/game/{game_id}/action")
def action(game_id: str, payload: ActionIn, user: dict = Depends(current_user)) -> dict:
    state = load_game(game_id, user["id"])
    if not state:
        raise HTTPException(status_code=404, detail="Game not found")
    try:
        state = apply_action(state, payload.surface, payload.item_id, payload.choice, payload.custom_text)
    except ValueError as exc:
        raise HTTPException(status_code=400, detail=str(exc)) from exc
    except RuntimeError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    save_game(game_id, user["id"], state)
    return {"game": state}


@app.post("/api/game/{game_id}/tick")
def tick(game_id: str, user: dict = Depends(current_user)) -> dict:
    state = load_game(game_id, user["id"])
    if not state:
        raise HTTPException(status_code=404, detail="Game not found")
    try:
        state = advance_world(state)
    except RuntimeError as exc:
        raise HTTPException(status_code=503, detail=str(exc)) from exc
    save_game(game_id, user["id"], state)
    return {"game": state}
