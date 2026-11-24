import { randomUUID } from "crypto";
import type { WebSocket } from "ws";
import type { GuessEntry, Phase, Player, RosterPlayer, Sentence } from "./types";

export class Game {
  readonly code: string;
  readonly minPlayers: number;
  readonly maxPlayers: number;
  phase: Phase = "lobby";
  players: Player[] = [];
  sentences: Sentence[] = [];

  constructor(code: string, minPlayers: number, maxPlayers: number) {
    this.code = code;
    this.minPlayers = minPlayers;
    this.maxPlayers = maxPlayers;
  }

  get host(): Player | undefined {
    return this.players.find((p) => p.isHost);
  }

  addPlayer(name: string, ws: WebSocket): Player {
    const player: Player = {
      id: randomUUID(),
      name: name.trim(),
      ready: false,
      isHost: this.players.length === 0,
      ws,
      sentence: null,
    };
    this.players.push(player);
    return player;
  }

  findPlayer(id: string): Player | undefined {
    return this.players.find((p) => p.id === id);
  }

  removePlayer(id: string): void {
    const idx = this.players.findIndex((p) => p.id === id);
    if (idx === -1) return;
    const [removed] = this.players.splice(idx, 1);
    if (removed.isHost && this.players.length > 0) {
      this.players[0].isHost = true;
    }
  }

  isFull(): boolean {
    return this.players.length >= this.maxPlayers;
  }

  allReady(): boolean {
    return (
      this.players.length >= this.minPlayers &&
      this.players.every((p) => p.ready)
    );
  }

  setReady(playerId: string, ready: boolean): void {
    const p = this.findPlayer(playerId);
    if (p) p.ready = ready;
  }

  resetRound(): void {
    this.phase = "lobby";
    this.sentences = [];
    for (const p of this.players) {
      p.ready = false;
      p.sentence = null;
    }
  }

  submitSentence(playerId: string, text: string): void {
    const p = this.findPlayer(playerId);
    if (p) p.sentence = text.trim();
  }

  allSentencesSubmitted(): boolean {
    return this.players.every((p) => p.sentence && p.sentence.length > 0);
  }

  buildSentences(distortedMap: Map<string, string>): void {
    this.sentences = this.players.map((p) => ({
      id: randomUUID(),
      authorId: p.id,
      authorName: p.name,
      original: p.sentence ?? "",
      distorted: distortedMap.get(p.id) ?? p.sentence ?? "",
      guesses: [],
    }));
  }

  toGuess(playerId: string): Sentence[] {
    return this.sentences.filter((s) => s.authorId !== playerId);
  }

  nextUnanswered(playerId: string): Sentence | undefined {
    return this.toGuess(playerId).find(
      (s) => !s.guesses.some((g) => g.playerId === playerId),
    );
  }

  submitGuess(playerId: string, sentenceId: string, text: string): boolean {
    const s = this.sentences.find((x) => x.id === sentenceId);
    const p = this.findPlayer(playerId);
    if (!s || !p) return false;
    if (s.guesses.some((g) => g.playerId === playerId)) return false;
    s.guesses.push({ playerId, playerName: p.name, text: text.trim() });
    return true;
  }

  allGuessesDone(): boolean {
    return this.sentences.every((s) => {
      const expected = this.players.filter((p) => p.id !== s.authorId);
      return expected.every((p) => s.guesses.some((g) => g.playerId === p.id));
    });
  }

  reviewPayload(): {
    sentences: {
      id: string;
      authorName: string;
      original: string;
      distorted: string;
      guesses: { playerName: string; text: string }[];
    }[];
  } {
    return {
      sentences: this.sentences.map((s) => ({
        id: s.id,
        authorName: s.authorName,
        original: s.original,
        distorted: s.distorted,
        guesses: s.guesses.map((g: GuessEntry) => ({
          playerName: g.playerName,
          text: g.text,
        })),
      })),
    };
  }

  roster(): RosterPlayer[] {
    return this.players.map((p) => ({
      id: p.id,
      name: p.name,
      ready: p.ready,
      isHost: p.isHost,
    }));
  }
}
