using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public class SentenceScreen : MonoBehaviour
{
    public InputField sentenceInput;
    public Button submitButton;
    public Text statusText;

    private void Awake()
    {
        submitButton.onClick.AddListener(Submit);
    }

    private void OnEnable()
    {
        sentenceInput.text = "";
        statusText.text = "";
        if (GameManager.Instance != null) GameManager.Instance.OnMessage += Handle;
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnMessage -= Handle;
    }

    private void Submit()
    {
        var text = sentenceInput.text.Trim();
        if (text.Length == 0) { statusText.text = "Enter a sentence"; return; }
        statusText.text = "";
        GameManager.Instance.SubmitSentence(text);
    }

    private void Handle(string type, JObject data)
    {
        if (type == "error") statusText.text = (string)data["message"];
    }
}
