(function () {
  "use strict";

  var state = {
    ws: null,
    playerId: null,
    isHost: false,
    myReady: false,
    currentSentenceId: null,
    retryDelay: 1000,
  };

  function $(id) { return document.getElementById(id); }

  function wsUrl() {
    var proto = location.protocol === "https:" ? "wss" : "ws";
    return proto + "://" + location.host;
  }

  function connect() {
    var ws = new WebSocket(wsUrl());
    state.ws = ws;

    ws.onopen = function () {
      state.retryDelay = 1000;
      hideBanner();
    };
    ws.onmessage = function (ev) {
      var msg;
      try { msg = JSON.parse(ev.data); } catch (e) { return; }
      handle(msg.type, msg.data || {});
    };
    ws.onclose = function () {
      state.ws = null;
      showBanner("Disconnected — retrying…");
      setTimeout(connect, state.retryDelay);
      state.retryDelay = Math.min(state.retryDelay * 2, 10000);
    };
    ws.onerror = function () { try { ws.close(); } catch (e) {} };
  }

  function send(type, data) {
    if (!state.ws || state.ws.readyState !== WebSocket.OPEN) return;
    state.ws.send(JSON.stringify({ type: type, data: data || {} }));
  }

  function show(id) {
    var screens = document.querySelectorAll(".screen");
    for (var i = 0; i < screens.length; i++) screens[i].classList.add("hidden");
    $(id).classList.remove("hidden");
  }

  function showBanner(text) {
    var b = $("banner");
    b.textContent = text;
    b.classList.remove("hidden");
  }
  function hideBanner() { $("banner").classList.add("hidden"); }

  function status(id, text) {
    var el = $(id);
    if (el) el.textContent = text || "";
  }

  function handle(type, data) {
    switch (type) {
      case "gameCreated":
      case "gameJoined":
        state.playerId = data.playerId;
        state.isHost = data.isHost;
        status("lobby-status", "");
        show("screen-lobby");
        break;
      case "lobby":
        renderLobby(data);
        show("screen-lobby");
        break;
      case "phase":
        if (data.phase === "sentence") {
          $("sentence-input").value = "";
          status("sentence-status", "");
          show("screen-sentence");
        } else if (data.phase === "lobby") {
          show("screen-lobby");
        }
        break;
      case "waiting":
        show("screen-waiting");
        break;
      case "guess":
        showGuess(data);
        break;
      case "review":
        renderReview(data);
        show("screen-review");
        break;
      case "error":
        showError(data.message);
        break;
      case "left":
        resetToHome();
        break;
    }
  }

  function renderLobby(data) {
    $("lobby-code").textContent = "Join code: " + data.code;
    var list = $("lobby-list");
    list.textContent = "";
    state.myReady = false;
    (data.players || []).forEach(function (p) {
      var div = document.createElement("div");
      div.className = "row";
      div.textContent = (p.isHost ? "[HOST] " : "") + p.name + (p.ready ? "  — ready" : "  — not ready");
      list.appendChild(div);
      if (p.id === state.playerId) state.myReady = p.ready;
    });
    $("btn-ready").textContent = state.myReady ? "Unready" : "Ready";
    $("btn-start").disabled = !state.isHost;
  }

  function showGuess(data) {
    state.currentSentenceId = data.sentenceId;
    $("guess-progress").textContent = "Guess " + (data.index + 1) + " of " + data.total;
    $("guess-distorted").textContent = data.distorted;
    $("guess-input").value = "";
    status("guess-status", "");
    show("screen-guessing");
  }

  function renderReview(data) {
    var list = $("review-list");
    list.textContent = "";
    (data.sentences || []).forEach(function (s) {
      var block = document.createElement("div");
      block.className = "review-block";

      var author = document.createElement("div");
      author.className = "review-author";
      author.textContent = s.authorName + " wrote:";
      block.appendChild(author);

      var orig = document.createElement("div");
      orig.className = "review-original";
      orig.textContent = "\u201C" + s.original + "\u201D";
      block.appendChild(orig);

      var dist = document.createElement("div");
      dist.className = "review-distorted";
      dist.textContent = "Distorted: " + s.distorted;
      block.appendChild(dist);

      (s.guesses || []).forEach(function (g) {
        var line = document.createElement("div");
        line.className = "review-guess";
        line.textContent = g.playerName + " guessed: " + g.text;
        block.appendChild(line);
      });

      list.appendChild(block);
    });
    $("btn-play-again").style.display = state.isHost ? "" : "none";
  }

  function showError(msg) {
    if (!$("screen-home").classList.contains("hidden")) status("home-status", msg);
    else if (!$("screen-lobby").classList.contains("hidden")) status("lobby-status", msg);
    else if (!$("screen-sentence").classList.contains("hidden")) status("sentence-status", msg);
    else if (!$("screen-guessing").classList.contains("hidden")) status("guess-status", msg);
  }

  function resetToHome() {
    state.playerId = null;
    state.isHost = false;
    state.myReady = false;
    $("home-name").value = "";
    $("home-code").value = "";
    status("home-status", "");
    show("screen-home");
  }

  function createGame() {
    var name = $("home-name").value.trim();
    if (!name) { status("home-status", "Enter a name"); return; }
    status("home-status", "");
    send("createGame", { name: name });
  }

  function joinGame() {
    var name = $("home-name").value.trim();
    var code = $("home-code").value.trim();
    if (!name) { status("home-status", "Enter a name"); return; }
    if (!code) { status("home-status", "Enter a join code"); return; }
    status("home-status", "");
    send("joinGame", { name: name, code: code });
  }

  function toggleReady() { send("ready", { ready: !state.myReady }); }
  function startGame() { send("startGame", {}); }
  function leaveGame() { send("leaveGame", {}); }
  function playAgain() { send("playAgain", {}); }

  function submitSentence() {
    var text = $("sentence-input").value.trim();
    if (!text) { status("sentence-status", "Enter a sentence"); return; }
    status("sentence-status", "");
    send("submitSentence", { text: text });
  }

  function submitGuess() {
    var text = $("guess-input").value.trim();
    if (!text) { status("guess-status", "Enter a guess"); return; }
    status("guess-status", "");
    send("submitGuess", { sentenceId: state.currentSentenceId, text: text });
  }

  $("btn-host").addEventListener("click", createGame);
  $("btn-join").addEventListener("click", joinGame);
  $("btn-ready").addEventListener("click", toggleReady);
  $("btn-start").addEventListener("click", startGame);
  $("btn-leave-lobby").addEventListener("click", leaveGame);
  $("btn-leave-review").addEventListener("click", leaveGame);
  $("btn-play-again").addEventListener("click", playAgain);
  $("btn-sentence").addEventListener("click", submitSentence);
  $("btn-guess").addEventListener("click", submitGuess);

  function onEnter(id, fn) {
    $(id).addEventListener("keydown", function (e) {
      if (e.key === "Enter") { e.preventDefault(); fn(); }
    });
  }
  onEnter("sentence-input", submitSentence);
  onEnter("guess-input", submitGuess);

  show("screen-home");
  connect();
})();
