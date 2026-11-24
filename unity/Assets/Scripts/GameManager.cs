using System;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private string serverUrl = "wss://guess.vincentas.xyz";

    public NetworkClient Net { get; private set; }
    public event Action<string, JObject> OnMessage;

    public string PlayerId { get; private set; }
    public bool IsHost { get; private set; }
    public bool MyReady { get; private set; }
    public JObject Lobby { get; private set; }
    public JObject CurrentGuess { get; private set; }
    public JObject Review { get; private set; }

    private string currentSentenceId;
    private string currentScene = "";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        Net = GetComponent<NetworkClient>();
        Net.OnMessage += HandleMessage;
        Net.Connect(serverUrl);

        Load("Home");
    }

    private void OnDestroy()
    {
        if (Net != null) Net.OnMessage -= HandleMessage;
    }

    private void Load(string scene)
    {
        if (currentScene == scene) return;
        currentScene = scene;
        SceneManager.LoadScene(scene);
    }

    // --- Intents sent by the screen controllers ---
    public void HostGame(string name) { Net.Send("createGame", new JObject { ["name"] = name }); }
    public void JoinGame(string name, string code) { Net.Send("joinGame", new JObject { ["name"] = name, ["code"] = code }); }
    public void ToggleReady() { Net.Send("ready", new JObject { ["ready"] = !MyReady }); }
    public void StartGame() { Net.Send("startGame", new JObject()); }
    public void SubmitSentence(string text) { Net.Send("submitSentence", new JObject { ["text"] = text }); }
    public void SubmitGuess(string text) { Net.Send("submitGuess", new JObject { ["sentenceId"] = currentSentenceId, ["text"] = text }); }
    public void PlayAgain() { Net.Send("playAgain", new JObject()); }
    public void LeaveGame() { Net.Send("leaveGame", new JObject()); }

    private void HandleMessage(string type, JObject data)
    {
        switch (type)
        {
            case "gameCreated":
            case "gameJoined":
                PlayerId = (string)data["playerId"];
                IsHost = (bool)data["isHost"];
                Load("Lobby");
                break;
            case "lobby":
                Lobby = data;
                UpdateMyReady(data);
                Load("Lobby");
                break;
            case "phase":
                var phase = (string)data["phase"];
                if (phase == "sentence") Load("Sentence");
                else if (phase == "lobby") Load("Lobby");
                break;
            case "waiting":
                Load("Waiting");
                break;
            case "guess":
                currentSentenceId = (string)data["sentenceId"];
                CurrentGuess = data;
                Load("Guessing");
                break;
            case "review":
                Review = data;
                Load("Review");
                break;
            case "left":
                Reset();
                Load("Home");
                break;
        }
        OnMessage?.Invoke(type, data);
    }

    private void UpdateMyReady(JObject lobby)
    {
        MyReady = false;
        var players = lobby["players"] as JArray;
        if (players == null) return;
        foreach (var p in players)
        {
            if ((string)p["id"] == PlayerId)
            {
                MyReady = (bool)p["ready"];
                return;
            }
        }
    }

    private void Reset()
    {
        PlayerId = null;
        IsHost = false;
        MyReady = false;
        Lobby = null;
        CurrentGuess = null;
        Review = null;
        currentSentenceId = null;
    }
}
