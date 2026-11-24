using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public class LobbyScreen : MonoBehaviour
{
    public Text codeText;
    public RectTransform listRoot;
    public Text readyLabel;
    public Button readyButton;
    public Button startButton;
    public Button leaveButton;
    public Text statusText;

    private void Awake()
    {
        readyButton.onClick.AddListener(() => GameManager.Instance.ToggleReady());
        startButton.onClick.AddListener(() => GameManager.Instance.StartGame());
        leaveButton.onClick.AddListener(() => GameManager.Instance.LeaveGame());
    }

    private void OnEnable()
    {
        statusText.text = "";
        if (GameManager.Instance != null) GameManager.Instance.OnMessage += Handle;
        Render();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnMessage -= Handle;
    }

    private void Handle(string type, JObject data)
    {
        if (type == "lobby") Render();
        else if (type == "error") statusText.text = (string)data["message"];
    }

    private void Render()
    {
        var gm = GameManager.Instance;
        var data = gm.Lobby;
        if (data == null) return;

        codeText.text = "Join code: " + (string)data["code"];

        Ui.Clear(listRoot);
        var players = data["players"] as JArray;
        if (players != null)
        {
            foreach (var p in players)
            {
                var line = ((bool)p["isHost"] ? "[HOST] " : "") + (string)p["name"] +
                           ((bool)p["ready"] ? "  — ready" : "  — not ready");
                Ui.Row(listRoot, line, 28);
            }
        }

        readyLabel.text = gm.MyReady ? "Unready" : "Ready";
        startButton.interactable = gm.IsHost;
    }
}
