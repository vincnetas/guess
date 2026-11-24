# Guess — Game Specification

> Working title. A multiplayer "broken telephone" party game: players write
> sentences, an LLM distorts them, and everyone guesses the originals.
>
> This document describes the game **as implemented** in `backend/src` and
> `unity/Assets/Scripts`. Where behaviour is a known gap rather than a design
> decision, it is listed in §9.

## 1. Overview

Players join a shared room from their own device (Unity client or web
browser) and play rounds made of four phases:
**Lobby → Sentence entry → Guessing → Review**. After a round the host can
start another one with the same players.

The backend is a self-hosted Node.js WebSocket server. Sentence distortion is
done by an LLM (DeepSeek API or local Ollama) or a mock, selected by
`DISTORTION_MODE` (§4).

## 2. Players

- 2–8 players per game (`MIN_PLAYERS` / `MAX_PLAYERS`).
- Every player has a **display name**, entered on the Home screen. Names are
  trimmed and must be non-empty; they are not required to be unique.
- The player who creates the game is the **host**. If the host leaves or
  disconnects, the next player in join order becomes host.
- Each player gets a server-generated `playerId` (UUID).

## 3. Screens & flow

### 3.1 Home screen
One shared form with **Your name** and **Join code** fields and two buttons:

1. **Host Game** — sends `createGame` with the name. The server replies with
   a 4-character **join code** from the alphabet
   `ABCDEFGHJKMNPQRSTUVWXYZ23456789` (no `I`, `L`, `O`, `0`, `1`).
2. **Join Game** — sends `joinGame` with the name and code. Codes are matched
   case-insensitively.

Joining fails with an error if the game doesn't exist, is already past the
lobby, or is full.

### 3.2 Lobby
- Everyone sees the join code and the player list, each line showing
  `[HOST]` (if host), the name, and ready / not ready.
- Each player (including host) toggles **Ready** / **Unready**.
- **Start Game** is clickable only for the host. The server rejects the start
  unless there are at least `MIN_PLAYERS` players and every player is ready.
- **Leave** returns the player to Home.

### 3.3 Sentence entry
- Each player types **one sentence** and presses Submit.
- After submitting, the player sees **"Waiting for other players…"**.
- A sentence cannot be edited or resubmitted.
- When the last sentence arrives, the server distorts all sentences in
  parallel and then moves everyone to Guessing.

### 3.4 Guessing
- Each player receives, **one at a time**, the distorted versions of every
  **other** player's sentence, with a progress label ("Guess 2 of 3").
- For each, the player types what they think the original was and submits;
  the next one follows immediately.
- After their last guess the player sees **"Waiting for other players…"**
  until everyone is done.

### 3.5 Review
- Every player sees, for each sentence: the author, the original sentence,
  the distorted version, and each other player's guess.
- The host sees **Play again**, which resets the round (all sentences and
  ready flags cleared) and sends everyone back to the Lobby.
- Everyone sees **Leave**, which returns them to Home.

## 4. Distortion (LLM)

- Happens **once per sentence**, on the server, after all sentences are in.
- The original sentence is never sent to other players before Review.
- The prompt asks the model to replace a few words with similar or related
  words, keeping meaning, length and structure, and to output only the new
  sentence. Output is cleaned: `<think>…</think>` blocks and other tags are
  removed, surrounding quotes are stripped, and the last non-empty line is
  used.
- **On any failure** (HTTP error, timeout, empty output, missing API key) the
  original sentence is used unchanged and the error is logged.

Modes, selected by `DISTORTION_MODE`:

| mode             | behaviour                                                                 |
| ---------------- | ------------------------------------------------------------------------- |
| `mock` (default) | Returns `MOCK_DISTORTED` instantly, so every sentence becomes the same fixed string. For development. |
| `openai`         | `POST {DISTORTION_BASE_URL}/chat/completions` (OpenAI-compatible; DeepSeek `deepseek-chat` by default). ~1 s per sentence, 30 s timeout, needs `DISTORTION_API_KEY`. Used in the live deploy. |
| `ollama`         | `POST {OLLAMA_URL}/api/generate` (default `deepseek-r1:1.5b`). CPU-only, ~55 s per sentence, 120 s timeout. |

Unknown mode values fall back to `mock`.

## 5. Networking

- Transport: **WebSocket**, one connection per player.
- The backend is authoritative for all game state. Clients send intents and
  render what the server tells them.
- All message types and field names are **camelCase**.

### 5.1 Envelope

```json
{ "type": "<message-type>", "data": { } }
```

`data` may be omitted on client messages; the server treats it as `{}`.

### 5.2 Client → Server

| type             | data                    | who / when                         | success response                         |
| ---------------- | ----------------------- | ---------------------------------- | ---------------------------------------- |
| `createGame`     | `{ name }`              | not in a game                      | `gameCreated`, then `lobby` to all       |
| `joinGame`       | `{ name, code }`        | not in a game; game in lobby       | `gameJoined`, then `lobby` to all        |
| `ready`          | `{ ready: boolean }`    | in a game                          | `lobby` to all                           |
| `startGame`      | `{}`                    | host; all ready, ≥ `MIN_PLAYERS`   | `phase {sentence}` to all                |
| `submitSentence` | `{ text }`              | sentence phase, once per player    | `waiting {sentence}`, or (last one) guessing starts |
| `submitGuess`    | `{ sentenceId, text }`  | guessing phase, once per sentence  | next `guess`, `waiting {guessing}`, or (last one) review |
| `playAgain`      | `{}`                    | host                               | `phase {lobby}` + `lobby` to all         |
| `leaveGame`      | `{}`                    | in a game                          | `left` to sender, `lobby` to the rest    |

Closing the socket has the same effect as `leaveGame`.

### 5.3 Server → Client

| type          | data                                                                                  | sent when                                    |
| ------------- | ------------------------------------------------------------------------------------- | -------------------------------------------- |
| `gameCreated` | `{ code, playerId, name, isHost: true }`                                              | to the creator                               |
| `gameJoined`  | `{ code, playerId, name, isHost: false }`                                             | to the joiner                                |
| `lobby`       | `{ code, players: [{ id, name, ready, isHost }] }`                                    | to all, whenever the roster or ready state changes |
| `phase`       | `{ phase }` — one of `lobby`, `sentence`, `guessing`, `review`                        | to all, on every phase change                |
| `waiting`     | `{ reason }` — `sentence` or `guessing`                                               | to one player who is done and waiting        |
| `guess`       | `{ sentenceId, distorted, index, total }` (`index` is 0-based)                        | to one player, for their next sentence       |
| `review`      | `{ sentences: [{ id, authorName, original, distorted, guesses: [{ playerName, text }] }] }`      | to all, right after `phase {review}`         |
| `left`        | `{}`                                                                                  | to a player who sent `leaveGame`             |
| `error`       | `{ message }`                                                                         | to the sender, when an intent is rejected    |

Error messages: `Invalid JSON`, `Unknown type: <type>`, `Not in a game`,
`Name is required`, `Game not found`, `Game already in progress`,
`Game is full`, `Only the host can start`, `Not everyone is ready`,
`Not in sentence phase`, `Sentence already submitted`, `Sentence is required`,
`Not in guessing phase`, `Invalid guess`, `Guess already submitted`.

### 5.4 Phases

`lobby` → `sentence` → `guessing` → `review` → (`playAgain`) → `lobby`.

The client drives its Sentence and Lobby screens from `phase`; the Guessing,
Waiting and Review screens are driven by `guess`, `waiting` and `review`.

## 6. Backend (Node.js + TypeScript)

| file            | role                                                       |
| --------------- | ---------------------------------------------------------- |
| `index.ts`      | entry point, calls `start()`                               |
| `server.ts`     | `ws` server + HTTP server, message routing, join codes, broadcasting |
| `static.ts`     | minimal static file server for the web client              |
| `game.ts`       | `Game` class: players, host, ready state, sentences, guesses |
| `distortion.ts` | the three distortion modes                                 |
| `types.ts`      | shared types (`Phase`, `Player`, `Sentence`, …)            |

- In-memory state only. A game is deleted when its last player leaves.
- A single HTTP server hosts both the WebSocket upgrade and the static web
  client. Non-upgrade requests are served from `WEB_DIR`; the web client is
  therefore same-origin and connects to `ws://<host>/` with no path.
- Deployed with Docker Compose in `/opt/guess/`
  (`docker compose up -d --build`). The container listens on 8080 and is
  published on the host as `${PORT}` (default **8091**); `web/` is
  bind-mounted into the container at `/app/web` (`WEB_DIR`).

### Environment variables

| var                   | default (code)                                        | description                          |
| --------------------- | ----------------------------------------------------- | ------------------------------------ |
| `PORT`                | `8080`                                                | listen port inside the process. In compose, `PORT` in `.env` is the **host** port (default 8091); the container always gets 8080. |
| `DISTORTION_MODE`     | `mock`                                                | `mock`, `openai` or `ollama`         |
| `MOCK_DISTORTED`      | `A completely different sentence with swapped words.` | string returned in mock mode         |
| `DISTORTION_API_KEY`  | —                                                     | API key for `openai` mode            |
| `DISTORTION_BASE_URL` | `https://api.deepseek.com`                            | OpenAI-compatible base URL           |
| `DISTORTION_MODEL`    | `deepseek-chat`                                       | model for `openai` mode              |
| `OLLAMA_URL`          | `http://host.docker.internal:11434`                   | Ollama base URL                      |
| `OLLAMA_MODEL`        | `deepseek-r1:1.5b`                                    | model for `ollama` mode              |
| `MIN_PLAYERS`         | `2`                                                   | min players to start                 |
| `MAX_PLAYERS`         | `8`                                                   | max players per game                 |
| `WEB_DIR`             | `web` (relative to cwd)                               | directory served as the static web client |

## 7. Clients

### 7.1 Unity 6 / C#

- Unity 6 (`6000.3.23f1`), UGUI with the legacy `Text` / `InputField` / `Button`
  components and the built-in `LegacyRuntime.ttf` font.
- **One scene per screen.** `Assets/Scenes/` holds `Bootstrap`, `Home`,
  `Lobby`, `Sentence`, `Waiting`, `Guessing`, `Review`. Each screen scene
  contains a real `Canvas` with its buttons, inputs and text placed and
  editable in the editor. The scenes are generated by `Guess → Build All
  Scenes` (Editor script `SceneBuilder`), but are ordinary editable scenes
  afterwards.
- **Persistent manager.** The `Bootstrap` scene (index 0 in Build Settings)
  contains a `GameManager` GameObject (`DontDestroyOnLoad`) that also carries
  the `NetworkClient` and a shared `EventSystem`. It owns the WebSocket and
  game state and switches screens with `SceneManager.LoadScene(...)`.
- Each screen has a small controller (`HomeScreen`, `LobbyScreen`, …) that
  reads `GameManager.Instance`, wires its buttons in `Awake`, pulls the current
  state on enable, and subscribes to `GameManager.OnMessage` for live updates.
- Dynamic content (the roster and the review list) is built at runtime as
  children of empty `VerticalLayoutGroup` containers inside the scenes, via the
  `Ui` helpers; the containers themselves are scene objects.
- `NetworkClient` wraps `System.Net.WebSockets.ClientWebSocket` and parses
  envelopes with Newtonsoft.Json (`JObject` / `JArray`, package
  `com.unity.nuget.newtonsoft-json`). It raises `OnMessage(type, data)` on the
  main thread.
- Server URL is the serialized field `GameManager.serverUrl` (editable on the
  `GameManager` object in the `Bootstrap` scene), default
  `ws://192.168.7.162:8091`.
- Press Play in the `Bootstrap` scene to run the game.

### 7.2 Web (HTML / CSS / JS)

- Static files in `web/` (`index.html`, `style.css`, `app.js`); no build step.
- Uses the browser `WebSocket` API and connects to the same origin
  (`ws(s)://<location.host>`), so it works on the LAN and over any reverse
  proxy without extra configuration.
- Mirrors the Unity client: same screens and message handling, driven by the
  same `gameCreated` / `lobby` / `phase` / `guess` / `review` / `error` events.
- Reconnects automatically with exponential backoff if the socket drops.
- All user content (names, sentences, guesses) is rendered with `textContent`
  (never `innerHTML`) to avoid injection.

## 8. Non-goals (for now)

- No persistent accounts, history, or reconnect.
- No spectator mode.
- No localization.
- No anti-cheat (join codes are the only access control).
- No scoring — Review shows guesses side by side without judging them.

## 9. Known gaps

Behaviour of the current code that probably isn't intended:

- **Leaving mid-round.** A player who leaves during Sentence or Guessing is
  removed, and everyone else receives `lobby`, which switches their client to
  the Lobby screen while the server stays in the old phase. The server also
  doesn't re-check whether all sentences or guesses are now in, so the round
  can stall.
- **No "distorting" feedback.** The last player to submit a sentence gets no
  `waiting` message; their Sentence screen stays up until distortion finishes
  (up to minutes in `ollama` mode).
- **Start button.** The client enables Start for the host regardless of ready
  state; the server enforces the rule and replies with an error.
- **No input limits.** Names and sentences have no length limit.
- **`createGame` / `joinGame` while already in a game** isn't rejected; the
  connection is re-bound to the new game and the old one keeps a ghost player.
- **No connection status in the UI.** If the server is unreachable the Home
  screen still shows; the failure only appears in the Console.
