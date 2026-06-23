from __future__ import annotations

import os
import time
import urllib.error
import urllib.request
import json


BASE_URL = os.environ.get("DEEPDETECT_URL", "http://127.0.0.1:8765").rstrip("/")


def request(path: str, method: str = "GET", body: dict | None = None, token: str | None = None) -> tuple[int, dict | str]:
    data = None
    headers = {}
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    if token:
        headers["Authorization"] = f"Bearer {token}"

    req = urllib.request.Request(f"{BASE_URL}{path}", data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=180) as response:
            text = response.read().decode("utf-8")
            try:
                return response.status, json.loads(text)
            except json.JSONDecodeError:
                return response.status, text
    except urllib.error.HTTPError as error:
        text = error.read().decode("utf-8")
        try:
            return error.code, json.loads(text)
        except json.JSONDecodeError:
            return error.code, text


def assert_true(condition: bool, message: str) -> None:
    if not condition:
        raise AssertionError(message)


def main() -> None:
    status, health = request("/health")
    assert_true(status == 200, f"health expected 200, got {status}: {health}")
    assert_true(isinstance(health, dict) and health.get("status") == "ok", f"unexpected health body: {health}")

    status, homepage = request("/")
    assert_true(status == 200, f"homepage expected 200, got {status}")
    assert_true(isinstance(homepage, str) and "DeepDetect" in homepage, "homepage did not look like DeepDetect UI")

    stamp = int(time.time() * 1000)
    email = f"smoke.{stamp}@example.com"
    password = "secret123"
    status, registered = request(
        "/api/auth/register",
        "POST",
        {"name": "Smoke Tester", "email": email, "password": password},
    )
    assert_true(status == 200, f"register expected 200, got {status}: {registered}")
    token = registered.get("token") if isinstance(registered, dict) else None
    assert_true(isinstance(token, str) and token, "register did not return a token")

    status, login = request("/api/auth/login", "POST", {"email": email, "password": password})
    assert_true(status == 200, f"login expected 200, got {status}: {login}")
    login_token = login.get("token") if isinstance(login, dict) else None
    assert_true(isinstance(login_token, str) and login_token, "login did not return a token")

    status, generated = request("/api/game/generate", "POST", {}, login_token)
    assert_true(status == 200, f"generate expected 200, got {status}: {generated}")
    game = generated.get("game") if isinstance(generated, dict) else None
    assert_true(isinstance(game, dict), "generate did not return a game object")
    game_id = game.get("id")
    assert_true(isinstance(game_id, str) and game_id, "game id missing")
    assert_true(len(game.get("news_items", [])) >= 1, "generated game has no news items")
    assert_true(len(game.get("emails", [])) >= 1, "generated game has no email sidequests")
    assert_true(len(game.get("telegram_threads", [])) >= 1, "generated game has no telegram sidequests")
    assert_true(game.get("agent_mode") == "openai", f"expected openai agent mode, got {game.get('agent_mode')}")

    status, games = request("/api/games", token=login_token)
    assert_true(status == 200, f"games expected 200, got {status}: {games}")
    saved_games = games.get("games") if isinstance(games, dict) else None
    assert_true(isinstance(saved_games, list), "games response did not contain a list")
    assert_true(any(item.get("id") == game_id for item in saved_games), "generated game was not persisted")

    print(json.dumps({"ok": True, "base_url": BASE_URL, "game_id": game_id}, indent=2))


if __name__ == "__main__":
    main()
