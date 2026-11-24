const DISTORTION_MODE = process.env.DISTORTION_MODE ?? "mock";
const OLLAMA_URL = process.env.OLLAMA_URL ?? "http://host.docker.internal:11434";
const OLLAMA_MODEL = process.env.OLLAMA_MODEL ?? "deepseek-r1:1.5b";
const OPENAI_BASE_URL = process.env.DISTORTION_BASE_URL ?? "https://api.deepseek.com";
const OPENAI_MODEL = process.env.DISTORTION_MODEL ?? "deepseek-chat";
const MOCK_DISTORTED =
  process.env.MOCK_DISTORTED ?? "A completely different sentence with swapped words.";

export async function distort(text: string): Promise<string> {
  switch (DISTORTION_MODE) {
    case "ollama":
      return distortViaOllama(text);
    case "openai":
      return distortViaOpenAI(text);
    default:
      return MOCK_DISTORTED;
  }
}

function buildPrompt(text: string): string {
  return [
    "You are a word-distortion engine for a party game.",
    "Given a sentence, rewrite it by replacing a few words with words of similar",
    "or related meaning, so the result is a slightly different but still natural",
    "sentence. Keep the same meaning, roughly the same length and structure.",
    "Write ONLY the rewritten sentence, with no parentheses, no quotes, no",
    "explanations, and no other text.",
    "",
    `Sentence: "${text}"`,
    "",
    "Rewritten sentence:",
  ].join("\n");
}

function sanitize(raw: string): string {
  let s = raw;
  // Strip the deepseek-r1 reasoning block entirely, if present.
  s = s.replace(/<think>[\s\S]*?<\/think>/gi, " ");
  // Strip any other markup tags.
  s = s.replace(/<[^>]+>/g, " ");
  s = s.replace(/^["'`]+|["'`]+$/g, "");
  const lines = s
    .split("\n")
    .map((l) => l.trim())
    .filter(Boolean);
  return lines.length ? lines[lines.length - 1] : "";
}

async function distortViaOllama(text: string): Promise<string> {
  const url = `${OLLAMA_URL.replace(/\/+$/, "")}/api/generate`;
  try {
    const res = await fetch(url, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        model: OLLAMA_MODEL,
        prompt: buildPrompt(text),
        stream: false,
        options: { temperature: 0.7 },
      }),
      signal: AbortSignal.timeout(120_000),
    });
    if (!res.ok) {
      throw new Error(`ollama returned ${res.status}`);
    }
    const json = (await res.json()) as { response?: string };
    const raw = typeof json.response === "string" ? json.response : "";
    const cleaned = sanitize(raw);
    return cleaned || text;
  } catch (err) {
    console.error("[distortion] failed, falling back to original:", err);
    return text;
  }
}

async function distortViaOpenAI(text: string): Promise<string> {
  const apiKey = process.env.DISTORTION_API_KEY;
  if (!apiKey) {
    console.error("[distortion] DISTORTION_API_KEY is not set");
    return text;
  }
  const url = `${OPENAI_BASE_URL.replace(/\/+$/, "")}/chat/completions`;
  try {
    const res = await fetch(url, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${apiKey}`,
      },
      body: JSON.stringify({
        model: OPENAI_MODEL,
        messages: [
          {
            role: "system",
            content:
              "You are a word-distortion engine for a party game. Given a " +
              "sentence, rewrite it by replacing a few words with words of " +
              "similar or related meaning, keeping the same meaning, length, " +
              "and structure. Output ONLY the rewritten sentence — no quotes, " +
              "no parentheses, no explanations, no extra text.",
          },
          { role: "user", content: buildPrompt(text) },
        ],
        temperature: 0.7,
        max_tokens: 100,
        stream: false,
      }),
      signal: AbortSignal.timeout(30_000),
    });
    if (!res.ok) {
      const body = await res.text().catch(() => "");
      throw new Error(`openai returned ${res.status}: ${body.slice(0, 200)}`);
    }
    const json = (await res.json()) as {
      choices?: { message?: { content?: string } }[];
    };
    const raw = json.choices?.[0]?.message?.content ?? "";
    const cleaned = sanitize(raw);
    return cleaned || text;
  } catch (err) {
    console.error("[distortion] openai failed, falling back to original:", err);
    return text;
  }
}
