using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SceneBuilder
{
    private const string Dir = "Assets/Scenes";

    [MenuItem("Guess/Build All Scenes")]
    public static void Build()
    {
        EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        AssetDatabase.DeleteAsset(Dir + "/Main.unity");

        BuildBootstrapScene();
        BuildHomeScene();
        BuildLobbyScene();
        BuildSentenceScene();
        BuildWaitingScene();
        BuildGuessingScene();
        BuildReviewScene();

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(Dir + "/Bootstrap.unity", true),
            new EditorBuildSettingsScene(Dir + "/Home.unity", true),
            new EditorBuildSettingsScene(Dir + "/Lobby.unity", true),
            new EditorBuildSettingsScene(Dir + "/Sentence.unity", true),
            new EditorBuildSettingsScene(Dir + "/Waiting.unity", true),
            new EditorBuildSettingsScene(Dir + "/Guessing.unity", true),
            new EditorBuildSettingsScene(Dir + "/Review.unity", true),
        };

        AssetDatabase.SaveAssets();
        Debug.Log("Guess: all scenes built.");
    }

    private static void BuildBootstrapScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("GameManager");
        go.AddComponent<GameManager>();
        go.AddComponent<NetworkClient>();
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
        Save("Bootstrap");
    }

    private static void BuildHomeScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = CreateCanvas();
        var col = CreateColumn(canvas);

        CreateText(col, "Title", "GUESS", 64);
        CreateText(col, "Subtitle", "A multiplayer word-distortion game", 26);
        Space(col, 20);
        CreateText(col, "NameLabel", "Your name", 24);
        var nameInput = CreateInput(col, "NameInput", "Enter your name");
        Space(col, 10);
        CreateText(col, "CodeLabel", "Join code", 24);
        var codeInput = CreateInput(col, "CodeInput", "e.g. ABCD");
        Space(col, 24);
        var hostButton = CreateButton(col, "HostButton", "Host Game");
        var joinButton = CreateButton(col, "JoinButton", "Join Game");
        Space(col, 16);
        var status = CreateText(col, "Status", "", 24);
        status.color = new Color(1f, 0.6f, 0.4f);

        var screen = new GameObject("HomeScreen").AddComponent<HomeScreen>();
        screen.nameInput = nameInput;
        screen.codeInput = codeInput;
        screen.hostButton = hostButton;
        screen.joinButton = joinButton;
        screen.statusText = status;

        Save("Home");
    }

    private static void BuildLobbyScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = CreateCanvas();
        var col = CreateColumn(canvas);

        var code = CreateText(col, "Code", "Join code: —", 40);
        var list = CreateList(col, "PlayerList");
        Space(col, 8);
        var readyButton = CreateButton(col, "ReadyButton", "Ready");
        var readyLabel = readyButton.GetComponentInChildren<Text>();
        var startButton = CreateButton(col, "StartButton", "Start Game");
        var leaveButton = CreateButton(col, "LeaveButton", "Leave");
        var status = CreateText(col, "Status", "", 24);
        status.color = new Color(1f, 0.6f, 0.4f);

        var screen = new GameObject("LobbyScreen").AddComponent<LobbyScreen>();
        screen.codeText = code;
        screen.listRoot = list;
        screen.readyLabel = readyLabel;
        screen.readyButton = readyButton;
        screen.startButton = startButton;
        screen.leaveButton = leaveButton;
        screen.statusText = status;

        Save("Lobby");
    }

    private static void BuildSentenceScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = CreateCanvas();
        var col = CreateColumn(canvas);

        CreateText(col, "Title", "Write a sentence", 40);
        CreateText(col, "Subtitle", "Any sentence. Other players will see a distorted version of it.", 22);
        Space(col, 12);
        var input = CreateInput(col, "SentenceInput", "Type your sentence here", 64);
        Space(col, 12);
        var submitButton = CreateButton(col, "SubmitButton", "Submit");
        Space(col, 12);
        var status = CreateText(col, "Status", "", 24);
        status.color = new Color(1f, 0.6f, 0.4f);

        var screen = new GameObject("SentenceScreen").AddComponent<SentenceScreen>();
        screen.sentenceInput = input;
        screen.submitButton = submitButton;
        screen.statusText = status;

        Save("Sentence");
    }

    private static void BuildWaitingScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = CreateCanvas();
        var col = CreateColumn(canvas);
        Space(col, 200);
        CreateText(col, "Waiting", "Waiting for other players…", 40);
        Save("Waiting");
    }

    private static void BuildGuessingScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = CreateCanvas();
        var col = CreateColumn(canvas);

        var progress = CreateText(col, "Progress", "Guess 1 of 1", 32);
        Space(col, 12);
        CreateText(col, "Hint", "Someone wrote (distorted):", 22);
        var distorted = CreateText(col, "Distorted", "", 34);
        distorted.color = new Color(1f, 0.9f, 0.5f);
        Space(col, 16);
        CreateText(col, "Question", "What was the original?", 22);
        var input = CreateInput(col, "GuessInput", "Your guess", 64);
        Space(col, 12);
        var submitButton = CreateButton(col, "SubmitButton", "Submit");
        Space(col, 12);
        var status = CreateText(col, "Status", "", 24);
        status.color = new Color(1f, 0.6f, 0.4f);

        var screen = new GameObject("GuessingScreen").AddComponent<GuessingScreen>();
        screen.progressText = progress;
        screen.distortedText = distorted;
        screen.guessInput = input;
        screen.submitButton = submitButton;
        screen.statusText = status;

        Save("Guessing");
    }

    private static void BuildReviewScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var canvas = CreateCanvas();
        var col = CreateColumn(canvas);

        CreateText(col, "Title", "Round results", 44);
        var list = CreateList(col, "ReviewList");
        Space(col, 8);
        var playAgainButton = CreateButton(col, "PlayAgainButton", "Play again");
        var leaveButton = CreateButton(col, "LeaveButton", "Leave");

        var screen = new GameObject("ReviewScreen").AddComponent<ReviewScreen>();
        screen.listRoot = list;
        screen.playAgainButton = playAgainButton;
        screen.leaveButton = leaveButton;

        Save("Review");
    }

    // --- UI helpers (editor-time, produce inspectable scene objects) ---

    private static Canvas CreateCanvas()
    {
        var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static RectTransform CreateColumn(Canvas canvas)
    {
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvas.transform, false);
        Stretch((RectTransform)bg.transform);
        bg.GetComponent<Image>().color = new Color(0.12f, 0.12f, 0.16f, 1f);

        var col = new GameObject("Column", typeof(RectTransform));
        col.transform.SetParent(bg.transform, false);
        var crt = (RectTransform)col.transform;
        crt.anchorMin = new Vector2(0.5f, 0.5f);
        crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.sizeDelta = new Vector2(700, 1000);
        var vlg = col.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(20, 20, 20, 20);
        vlg.spacing = 10;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        return crt;
    }

    private static Text CreateText(Transform parent, string name, string text, int size, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = Ui.Font;
        t.fontSize = size;
        t.alignment = anchor;
        t.color = Color.white;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private static Button CreateButton(Transform parent, string name, string label, float height = 64)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.28f, 0.28f, 0.40f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        var labelGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(go.transform, false);
        var t = labelGo.GetComponent<Text>();
        t.text = label;
        t.font = Ui.Font;
        t.fontSize = 26;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        Stretch((RectTransform)labelGo.transform);
        return btn;
    }

    private static InputField CreateInput(Transform parent, string name, string placeholder, float height = 56)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(InputField));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = new Color(0.15f, 0.15f, 0.20f, 1f);
        var input = go.GetComponent<InputField>();
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.GetComponent<Text>();
        text.font = Ui.Font;
        text.fontSize = 22;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleLeft;
        text.supportRichText = false;
        Inset((RectTransform)textGo.transform, 12, 0, 12, 0);

        var phGo = new GameObject("Placeholder", typeof(RectTransform), typeof(Text));
        phGo.transform.SetParent(go.transform, false);
        var ph = phGo.GetComponent<Text>();
        ph.text = placeholder;
        ph.font = Ui.Font;
        ph.fontSize = 22;
        ph.fontStyle = FontStyle.Italic;
        ph.color = new Color(1, 1, 1, 0.4f);
        ph.alignment = TextAnchor.MiddleLeft;
        ph.supportRichText = false;
        ph.horizontalOverflow = HorizontalWrapMode.Wrap;
        ph.verticalOverflow = VerticalWrapMode.Truncate;
        Inset((RectTransform)phGo.transform, 12, 0, 12, 0);

        input.textComponent = text;
        input.placeholder = ph;
        input.targetGraphic = img;
        return input;
    }

    private static RectTransform CreateList(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 4;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        return (RectTransform)go.transform;
    }

    private static void Space(Transform parent, float height)
    {
        var go = new GameObject("Space", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredHeight = height;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Inset(RectTransform rt, float l, float b, float r, float t)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(l, b);
        rt.offsetMax = new Vector2(-r, -t);
    }

    private static void Save(string name)
    {
        if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets", "Scenes");
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), Dir + "/" + name + ".unity");
    }
}
