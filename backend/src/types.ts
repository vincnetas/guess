import type { WebSocket } from "ws";

export type Phase = "lobby" | "sentence" | "guessing" | "review";

export interface Player {
  id: string;
  name: string;
  ready: boolean;
  isHost: boolean;
  ws: WebSocket;
  sentence: string | null;
}

export interface GuessEntry {
  playerId: string;
  playerName: string;
  text: string;
}

export interface Sentence {
  id: string;
  authorId: string;
  authorName: string;
  original: string;
  distorted: string;
  guesses: GuessEntry[];
}

export interface RosterPlayer {
  id: string;
  name: string;
  ready: boolean;
  isHost: boolean;
}
