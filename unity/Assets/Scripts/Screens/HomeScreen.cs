using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public class HomeScreen : MonoBehaviour
{
    public InputField nameInput;
    public InputField codeInput;
    public Button hostButton;
    public Button joinButton;
    public Text statusText;

    private void Awake()
    {
        hostButton.onClick.AddListener(Host);
        joinButton.onClick.AddListener(Join);
    }

    private void OnEnable()
    {
        statusText.text = "";
        if (GameManager.Instance != null) GameManager.Instance.OnMessage += Handle;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnMessage -= Handle;
    }

    private void Host()
    {
        var name = nameInput.text.Trim();
        if (name.Length == 0) { statusText.text = "Enter a name"; return; }
        statusText.text = "";
        GameManager.Instance.HostGame(name);
    }

    private void Join()
    {
        var name = nameInput.text.Trim();
        var code = codeInput.text.Trim();
        if (name.Length == 0) { statusText.text = "Enter a name"; return; }
        if (code.Length == 0) { statusText.text = "Enter a join code"; return; }
        statusText.text = "";
        GameManager.Instance.JoinGame(name, code);
    }

    private void Handle(string type, JObject data)
    {
        if (type == "error") statusText.text = (string)data["message"];
    }
}
