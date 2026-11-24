using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public class GuessingScreen : MonoBehaviour
{
    public Text progressText;
    public Text distortedText;
    public InputField guessInput;
    public Button submitButton;
    public Text statusText;

    private void Awake()
    {
        submitButton.onClick.AddListener(Submit);
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

    private void Submit()
    {
        var text = guessInput.text.Trim();
        if (text.Length == 0) { statusText.text = "Enter a guess"; return; }
        statusText.text = "";
        GameManager.Instance.SubmitGuess(text);
    }

    private void Handle(string type, JObject data)
    {
        if (type == "guess") Render();
        else if (type == "error") statusText.text = (string)data["message"];
    }

    private void Render()
    {
        var gm = GameManager.Instance;
        var g = gm.CurrentGuess;
        if (g == null) return;
        progressText.text = "Guess " + ((int)g["index"] + 1) + " of " + (int)g["total"];
        distortedText.text = (string)g["distorted"];
        guessInput.text = "";
    }
}
