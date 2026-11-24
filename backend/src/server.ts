import http from "http";
import { WebSocket, WebSocketServer } from "ws";
import { distort } from "./distortion";
import { Game } from "./game";
import { createStaticHandler } from "./static";

const PORT = Number(process.env.PORT ?? 8080);
const WEB_DIR = process.env.WEB_DIR ?? "web";
const MIN_PLAYERS = Number(process.env.MIN_PLAYERS ?? 2);
const MAX_PLAYERS = Number(process.env.MAX_PLAYERS ?? 8);

const games = new Map<string, Game>();
const conns = new Map<WebSocket, { game: Game; playerId: string }>();

function send(ws: WebSocket, type: string, data: unknown = {}): void {
  if (ws.readyState === WebSocket.OPEN) {
    ws.send(JSON.stringify({ type, data }));
  }
}

function broadcast(game: Game, type: string, data: unknown = {}): void {
  for (const p of game.players) send(p.ws, type, data);
}

function sendLobby(game: Game): void {
  broadcast(game, "lobby", { code: game.code, players: game.roster() });
}

function generateCode(): string {
  const alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
  let code: string;
  do {
    code = Array.from(
      { length: 4 },
      () => alphabet[Math.floor(Math.random() * alphabet.length)],
    ).join("");
  } while (games.has(code));
  return code;
}

function removePlayer(game: Game, playerId: string): void {
  const p = game.findPlayer(playerId);
  if (!p) return;
  game.removePlayer(playerId);
  conns.delete(p.ws);
  if (game.players.length === 0) {
    games.delete(game.code);
  } else {
    sendLobby(game);
  }
}

function sendNextGuess(game: Game, playerId: string): void {
  const p = game.findPlayer(playerId);
  if (!p) return;
  const list = game.toGuess(playerId);
  const next = game.nextUnanswered(playerId);
  if (next) {
    const index = list.findIndex((s) => s.id === next.id);
    send(p.ws, "guess", {
      sentenceId: next.id,
      distorted: next.distorted,
      index,
      total: list.length,
    });
  } else {
    send(p.ws, "waiting", { reason: "guessing" });
  }
}

async function beginGuessing(game: Game): Promise<void> {
  const originals = new Map(game.players.map((p) => [p.id, p.sentence ?? ""]));
  const distorted = new Map<string, string>();
  await Promise.all(
    [...originals.entries()].map(async ([id, text]) => {
      distorted.set(id, await distort(text));
    }),
  );
  game.buildSentences(distorted);
  game.phase = "guessing";
  broadcast(game, "phase", { phase: "guessing" });
  for (const p of game.players) sendNextGuess(game, p.id);
}

function handleMessage(ws: WebSocket, raw: string): void {
  let msg: { type?: string; data?: Record<string, unknown> };
  try {
    msg = JSON.parse(raw);
  } catch {
    return send(ws, "error", { message: "Invalid JSON" });
  }
  const type = msg.type;
  const data = msg.data ?? {};

  const handleInGame = (fn: (ctx: {
    game: Game;
    playerId: string;
  }) => void) => {
    const ctx = conns.get(ws);
    if (!ctx) {
      send(ws, "error", { message: "Not in a game" });
      return;
    }
    fn(ctx);
  };

  switch (type) {
    case "createGame": {
      const name = String(data.name ?? "").trim();
      if (!name) return send(ws, "error", { message: "Name is required" });
      const game = new Game(generateCode(), MIN_PLAYERS, MAX_PLAYERS);
      const player = game.addPlayer(name, ws);
      games.set(game.code, game);
      conns.set(ws, { game, playerId: player.id });
      send(ws, "gameCreated", {
        code: game.code,
        playerId: player.id,
        name: player.name,
        isHost: true,
      });
      return sendLobby(game);
    }

    case "joinGame": {
      const name = String(data.name ?? "").trim();
      const code = String(data.code ?? "").trim().toUpperCase();
      const game = games.get(code);
      if (!name) return send(ws, "error", { message: "Name is required" });
      if (!game) return send(ws, "error", { message: "Game not found" });
      if (game.phase !== "lobby")
        return send(ws, "error", { message: "Game already in progress" });
      if (game.isFull()) return send(ws, "error", { message: "Game is full" });
      const player = game.addPlayer(name, ws);
      conns.set(ws, { game, playerId: player.id });
      send(ws, "gameJoined", {
        code: game.code,
        playerId: player.id,
        name: player.name,
        isHost: false,
      });
      return sendLobby(game);
    }

    case "ready":
      return handleInGame(({ game, playerId }) => {
        game.setReady(playerId, Boolean(data.ready));
        sendLobby(game);
      });

    case "startGame":
      return handleInGame(({ game, playerId }) => {
        const p = game.findPlayer(playerId);
        if (!p?.isHost)
          return send(ws, "error", { message: "Only the host can start" });
        if (!game.allReady())
          return send(ws, "error", { message: "Not everyone is ready" });
        game.phase = "sentence";
        broadcast(game, "phase", { phase: "sentence" });
      });

    case "submitSentence":
      return handleInGame(({ game, playerId }) => {
        if (game.phase !== "sentence")
          return send(ws, "error", { message: "Not in sentence phase" });
        const p = game.findPlayer(playerId);
        if (p?.sentence)
          return send(ws, "error", { message: "Sentence already submitted" });
        const text = String(data.text ?? "").trim();
        if (!text)
          return send(ws, "error", { message: "Sentence is required" });
        game.submitSentence(playerId, text);
        if (game.allSentencesSubmitted()) {
          void beginGuessing(game);
        } else {
          send(ws, "waiting", { reason: "sentence" });
        }
      });

    case "submitGuess":
      return handleInGame(({ game, playerId }) => {
        if (game.phase !== "guessing")
          return send(ws, "error", { message: "Not in guessing phase" });
        const sentenceId = String(data.sentenceId ?? "");
        const text = String(data.text ?? "").trim();
        if (!sentenceId || !text)
          return send(ws, "error", { message: "Invalid guess" });
        const ok = game.submitGuess(playerId, sentenceId, text);
        if (!ok)
          return send(ws, "error", { message: "Guess already submitted" });
        if (game.allGuessesDone()) {
          game.phase = "review";
          broadcast(game, "phase", { phase: "review" });
          broadcast(game, "review", game.reviewPayload());
        } else {
          sendNextGuess(game, playerId);
        }
      });

    case "playAgain":
      return handleInGame(({ game, playerId }) => {
        const p = game.findPlayer(playerId);
        if (!p?.isHost)
          return send(ws, "error", { message: "Only the host can start" });
        game.resetRound();
        broadcast(game, "phase", { phase: "lobby" });
        sendLobby(game);
      });

    case "leaveGame":
      return handleInGame(({ game, playerId }) => {
        removePlayer(game, playerId);
        send(ws, "left", {});
      });

    default:
      return send(ws, "error", { message: `Unknown type: ${String(type)}` });
  }
}

export function start(): void {
  const staticHandler = createStaticHandler(WEB_DIR);
  const server = http.createServer((req, res) => {
    void staticHandler(req, res);
  });
  const wss = new WebSocketServer({ server });
  console.log(`[guess] listening on :${PORT} (serving static from ${WEB_DIR})`);

  wss.on("connection", (ws) => {
    ws.on("message", (raw) => handleMessage(ws, raw.toString()));
    ws.on("close", () => {
      const ctx = conns.get(ws);
      if (ctx) removePlayer(ctx.game, ctx.playerId);
    });
    ws.on("error", (err) => console.error("[guess] socket error:", err));
  });

  server.listen(PORT);
}
