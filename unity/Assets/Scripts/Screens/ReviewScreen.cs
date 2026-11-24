using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public class ReviewScreen : MonoBehaviour
{
    public RectTransform listRoot;
    public Button playAgainButton;
    public Button leaveButton;

    private void Awake()
    {
        playAgainButton.onClick.AddListener(() => GameManager.Instance.PlayAgain());
        leaveButton.onClick.AddListener(() => GameManager.Instance.LeaveGame());
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnMessage += Handle;
        Render();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnMessage -= Handle;
    }

    private void Handle(string type, JObject data)
    {
        if (type == "review") Render();
    }

    private void Render()
    {
        var gm = GameManager.Instance;
        var data = gm.Review;
        if (data == null) return;

        Ui.Clear(listRoot);
        var sentences = data["sentences"] as JArray;
        if (sentences == null) return;

        foreach (var s in sentences)
        {
            Ui.Row(listRoot, (string)s["authorName"] + " wrote:", 24, TextAnchor.MiddleLeft);
            Ui.Row(listRoot, "\"" + (string)s["original"] + "\"", 26, TextAnchor.MiddleLeft);

            var d = Ui.Row(listRoot, "Distorted: \"" + (string)s["distorted"] + "\"", 22, TextAnchor.MiddleLeft);
            d.color = new Color(1f, 0.85f, 0.5f);

            var guesses = s["guesses"] as JArray;
            if (guesses != null)
            {
                foreach (var g in guesses)
                {
                    Ui.Row(listRoot, "    " + (string)g["playerName"] + " guessed: " + (string)g["text"], 20, TextAnchor.MiddleLeft);
                }
            }

            Ui.Space(listRoot, 14);
        }

        playAgainButton.gameObject.SetActive(gm.IsHost);
    }
}
