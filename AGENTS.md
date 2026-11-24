# Guess — project conventions

## Layout

```
guess/
├── AGENTS.md           # this file
├── docs/               # specs and design notes
│   └── GAME_SPEC.md
├── backend/            # Node.js + TypeScript WebSocket game server
├── unity/              # Unity 6 client project
├── web/                # web client (static HTML/JS, served by the backend)
├── docker-compose.yml  # single-service stack (builds ./backend)
└── .env.example        # env template for the server
```

## Stack (decided)

- **Client:** Unity 6 (`6000.3.23f1`), C#, UGUI. WebSocket via
  `System.Net.WebSockets.ClientWebSocket`.
- **Web client:** static HTML/CSS/JS (`web/`), no build step, uses the browser
  WebSocket API. Served by the backend on the same port as the WebSocket
  (see `WEB_DIR` / `static.ts`), so it's same-origin.
- **Backend:** Node.js + TypeScript, `ws` for WebSocket, in-memory state.
- **LLM:** sentence distortion is pluggable via `DISTORTION_MODE` —
  `mock` (instant fixed string, dev default), `openai` (DeepSeek
  `deepseek-chat`, fast, used in the live deploy), or `ollama` (local
  `deepseek-r1:1.5b`, CPU-only, slow). See `.env.example`.
- **Deploy:** local `docker compose up -d --build` in `/opt/guess/` on
  `root@padirbtuves.lt` (no CI, no registry). Exposed publicly at
  `https://guess.vincentas.xyz` via nginx + Let's Encrypt (WebSocket proxied).
  Compose + `.env` sit at the project root, alongside a copy of `backend/` and
  `web/`.

## Commands

### Backend

```sh
cd backend
npm install
npm run dev        # tsx watch (dev)
npm run build      # tsc -> dist
npm run typecheck  # tsc --noEmit
npm start          # node dist/index.js
```

### Unity (command line)

```sh
/Applications/Unity/Hub/Editor/6000.3.23f1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -quit -logFile - \
  -projectPath /Users/vincentas/dev/guess/unity \
  -executeMethod <static-method>
```

### Server access

```sh
ssh root@padirbtuves.lt
# deploy in /opt/guess/
```

### Run the game

- **Web:** open `https://guess.vincentas.xyz/` in a browser.
- **Unity:** open `unity/` in Unity Hub and press Play in the `Bootstrap`
  scene (or build a player). It connects to `GameManager.serverUrl`.

Either client can join the same game; open several windows / instances to
simulate multiple players.

## Conventions

- Backend source in `backend/src`, compiled to `backend/dist`.
- Message protocol: JSON envelope `{ "type", "data" }` over WebSocket. See
  `docs/GAME_SPEC.md` §5.
- Game state lives only in the backend; the client renders state and sends
  intents.
- No DB, no persistent state.
- Message types and field names are **camelCase** on the wire. The Unity
  client parses envelopes with Newtonsoft.Json `JObject`/`JArray` (package
  `com.unity.nuget.newtonsoft-json`); the web client uses plain `JSON.parse`.
- Client is **scene-based**: one scene per screen (`Bootstrap`, `Home`,
  `Lobby`, `Sentence`, `Waiting`, `Guessing`, `Review`) with UI placed and
  editable in the editor. A persistent `GameManager` (`DontDestroyOnLoad`) in
  `Bootstrap` owns the `NetworkClient`, state, and scene transitions; each
  screen has a controller (`HomeScreen`, `LobbyScreen`, …). Regenerate scenes
  via `Guess → Build All Scenes` (`SceneBuilder.Build`). Server URL is
  `GameManager.serverUrl` (default `wss://guess.vincentas.xyz`).
- The Unity project is **2D** (`EditorSettings.defaultBehaviorMode =
  Mode2D`); `SceneBuilder.Build` enforces it. The Scene view then opens
  orthographic/2D instead of 3D.

## Deploy checklist

1. `rsync`/copy `backend/`, `web/`, `docker-compose.yml`, and `.env` to
   `/opt/guess/` on `root@padirbtuves.lt`.
2. On the server: `cd /opt/guess && docker compose up -d --build`.
3. The `web/` folder is bind-mounted into the container (`./web:/app/web:ro`),
   so edits to the web client are live without a rebuild.
4. nginx serves `https://guess.vincentas.xyz` and proxies to `127.0.0.1:8091`
   with WebSocket upgrade headers (see
   `/etc/nginx/sites-enabled/guess.vincentas.xyz.conf` and
   `/etc/nginx/conf.d/guess-websocket-map.conf`). Certbot manages the TLS cert.
5. Distortion runs in `openai` mode (DeepSeek API); Ollama is not required on
   this host.
