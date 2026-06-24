using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class DeepDetectComputerController : MonoBehaviour
{
    [Header("Backend")]
    [SerializeField] private string backendUrl = "http://127.0.0.1:8765";
    [SerializeField] private bool tryStartBackendInEditor = true;
    [SerializeField] private string backendRelativePath = "DeepDetectGamePlatform";

    [Header("Demo Account")]
    [SerializeField] private string demoName = "Unity Editor";
    [SerializeField] private string demoEmail = "unity.tester@deepdetectgame.com";
    [SerializeField] private string demoPassword = "deepdetect-demo";

    private DeepDetectApiClient api;
    private DeepDetectGameState game;
    private RectTransform content;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private TMP_Text bodyText;
    private TMP_InputField customReplyInput;
    private string currentTab = "news";
    private Process backendProcess;

    private void Awake()
    {
        api = gameObject.GetComponent<DeepDetectApiClient>();
        if (api == null)
            api = gameObject.AddComponent<DeepDetectApiClient>();
        api.BaseUrl = backendUrl;
        BuildUi();
    }

    private void Start()
    {
        if (tryStartBackendInEditor)
            TryStartBackend();
        RefreshHealth();
    }

    private void OnDestroy()
    {
        if (backendProcess != null && !backendProcess.HasExited)
            backendProcess.Dispose();
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("DeepDetectComputerCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform root = canvasGo.GetComponent<RectTransform>();
        Stretch(root);

        var bg = CreatePanel(root, "Background", new Color(0.05f, 0.055f, 0.065f, 0.98f));
        Stretch(bg);

        titleText = CreateText(bg, "Title", "DeepDetect Newsroom", 30, FontStyles.Bold, TextAlignmentOptions.Left);
        Anchor(titleText.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(32, -72), new Vector2(-32, -20));

        statusText = CreateText(bg, "Status", "Connecting to backend...", 16, FontStyles.Normal, TextAlignmentOptions.Right);
        Anchor(statusText.rectTransform, new Vector2(0.55f, 1), new Vector2(1, 1), new Vector2(0, -72), new Vector2(-32, -20));

        var toolbar = CreatePanel(bg, "Toolbar", new Color(0.09f, 0.1f, 0.12f, 1f));
        Anchor(toolbar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -132), new Vector2(-24, -82));
        CreateToolbar(toolbar);

        var viewportPanel = CreatePanel(bg, "ViewportPanel", new Color(0.025f, 0.027f, 0.032f, 1f));
        Anchor(viewportPanel, new Vector2(0, 0), new Vector2(1, 1), new Vector2(24, 24), new Vector2(-24, -150));

        var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(Mask));
        scrollGo.transform.SetParent(viewportPanel, false);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        Stretch(scrollRect);
        scrollGo.GetComponent<Image>().color = new Color(0, 0, 0, 0);
        scrollGo.GetComponent<Mask>().showMaskGraphic = false;

        var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        contentGo.transform.SetParent(scrollRect, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.offsetMin = new Vector2(22, 0);
        content.offsetMax = new Vector2(-22, 0);
        var layout = contentGo.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(4, 4, 18, 18);
        layout.spacing = 12;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;
        contentGo.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = scrollRect;
        scroll.horizontal = false;

        bodyText = CreateText(content, "Body", "", 18, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        bodyText.textWrappingMode = TextWrappingModes.Normal;
        bodyText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        bodyText.rectTransform.sizeDelta = new Vector2(0, 500);

        var inputGo = new GameObject("CustomReplyInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        inputGo.transform.SetParent(content, false);
        inputGo.GetComponent<Image>().color = new Color(0.12f, 0.13f, 0.15f, 1f);
        customReplyInput = inputGo.GetComponent<TMP_InputField>();
        var inputText = CreateText(inputGo.GetComponent<RectTransform>(), "Text", "", 16, FontStyles.Normal, TextAlignmentOptions.Left);
        Anchor(inputText.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        var placeholder = CreateText(inputGo.GetComponent<RectTransform>(), "Placeholder", "Optional custom reply for Inbox/Telegram...", 16, FontStyles.Italic, TextAlignmentOptions.Left);
        placeholder.color = new Color(0.6f, 0.62f, 0.66f, 1f);
        Anchor(placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 4), new Vector2(-12, -4));
        customReplyInput.textComponent = inputText;
        customReplyInput.placeholder = placeholder;
        inputGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0, 46);

        Render();
    }

    private void CreateToolbar(RectTransform toolbar)
    {
        var layout = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 6, 6);
        layout.spacing = 8;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        AddButton(toolbar, "Health", RefreshHealth);
        AddButton(toolbar, "Login", LoginOrRegister);
        AddButton(toolbar, "Sessions", LoadSessions);
        AddButton(toolbar, "New Shift", GenerateShift);
        AddButton(toolbar, "Advance", AdvanceWorld);
        AddButton(toolbar, "News", () => SwitchTab("news"));
        AddButton(toolbar, "Inbox", () => SwitchTab("email"));
        AddButton(toolbar, "Telegram", () => SwitchTab("telegram"));
        AddButton(toolbar, "Briefing", () => SwitchTab("briefing"));
        AddButton(toolbar, "Back", () => SceneManager.LoadScene("Apartment"));
    }

    private void RefreshHealth()
    {
        SetStatus("Checking backend...");
        StartCoroutine(api.Health(_ => SetStatus("Backend online at " + api.BaseUrl), SetStatus));
    }

    private void LoginOrRegister()
    {
        SetStatus("Logging in demo account...");
        StartCoroutine(api.Login(demoEmail, demoPassword, _ =>
        {
            SetStatus("Logged in as " + demoEmail);
            LoadSessions();
        }, error =>
        {
            SetStatus("Login failed, registering demo account...");
            StartCoroutine(api.Register(demoName, demoEmail, demoPassword, _ =>
            {
                SetStatus("Registered and logged in as " + demoEmail);
                LoadSessions();
            }, SetStatus));
        }));
    }

    private void LoadSessions()
    {
        SetStatus("Loading sessions...");
        StartCoroutine(api.LoadGames(response =>
        {
            if (response.games != null && response.games.Length > 0)
            {
                StartCoroutine(api.LoadGame(response.games[0].id, gameResponse =>
                {
                    game = gameResponse.game;
                    SetStatus("Loaded session: " + game.title);
                    Render();
                }, SetStatus));
            }
            else
            {
                SetStatus("No saved sessions. Use New Shift.");
                Render();
            }
        }, SetStatus));
    }

    private void GenerateShift()
    {
        SetStatus("Generating AI shift. This can take a while...");
        StartCoroutine(api.GenerateGame(response =>
        {
            game = response.game;
            SetStatus("Generated shift: " + game.title);
            Render();
        }, SetStatus));
    }

    private void AdvanceWorld()
    {
        if (game == null || string.IsNullOrEmpty(game.id))
        {
            SetStatus("No active shift.");
            return;
        }
        SetStatus("Advancing live world...");
        StartCoroutine(api.AdvanceWorld(game.id, response =>
        {
            game = response.game;
            SetStatus("World tick " + game.world_tick);
            Render();
        }, SetStatus));
    }

    private void SwitchTab(string tab)
    {
        currentTab = tab;
        Render();
    }

    private void Submit(string surface, string itemId, string choice)
    {
        if (game == null)
            return;
        string custom = choice == "__custom__" ? customReplyInput.text : "";
        SetStatus("Submitting " + surface + " action...");
        StartCoroutine(api.SubmitAction(game.id, surface, itemId, choice, custom, response =>
        {
            game = response.game;
            customReplyInput.text = "";
            SetStatus("Action applied.");
            Render();
        }, SetStatus));
    }

    private void Render()
    {
        ClearDynamicButtons();
        if (game == null)
        {
            titleText.text = "DeepDetect Newsroom";
            SetBodyText("Backend URL: " + api.BaseUrl + "\n\nUse Health, Login, then New Shift. The Unity computer frontend calls the same FastAPI endpoints as the browser app.");
            return;
        }

        titleText.text = DisplaySafe(game.title + "  |  Score " + game.score + "  |  Tick " + game.world_tick);

        if (currentTab == "news")
            RenderNews();
        else if (currentTab == "email")
            RenderThreads("email", game.emails);
        else if (currentTab == "telegram")
            RenderThreads("telegram", game.telegram_threads);
        else
            RenderBriefing();
    }

    private void RenderNews()
    {
        string text = "NEWSDESK\n\n";
        if (game.news_items == null || game.news_items.Length == 0)
        {
            SetBodyText(text + "No news items.");
            return;
        }

        foreach (var item in game.news_items)
        {
            text += item.title + "\n";
            text += item.source + " | " + item.public_pressure + "\n";
            text += item.summary + "\n";
            if (!string.IsNullOrEmpty(item.decision))
                text += "Decision: " + item.decision + " | " + (item.correct ? "correct" : "wrong") + " | " + item.agent_response + "\n";
            text += "\n";

            if (string.IsNullOrEmpty(item.decision))
            {
                AddActionButton("Publish: " + Short(item.title), () => Submit("news", item.id, "publish"));
                AddActionButton("Reject: " + Short(item.title), () => Submit("news", item.id, "reject"));
            }
        }
        SetBodyText(text);
    }

    private void RenderThreads(string surface, DeepDetectThreadItem[] items)
    {
        string heading = surface == "email" ? "INBOX" : "TELEGRAM";
        string text = heading + "\n\n";
        if (items == null || items.Length == 0)
        {
            SetBodyText(text + "No active threads.");
            return;
        }

        foreach (var item in items)
        {
            string name = surface == "email" ? item.from_name + " - " + item.subject : item.contact + " (" + item.relationship + ")";
            text += name + "\n";
            if (item.messages != null)
            {
                foreach (var message in item.messages)
                    text += message.sender + ": " + message.text + "\n";
            }
            text += item.resolved ? "Resolved: " + (item.correct ? "responsible" : "risky") + "\n" : "Open thread: turn " + item.chat_turns + "/" + item.max_turns + "\n";
            text += "\n";

            if (!item.resolved)
            {
                if (item.options != null)
                {
                    foreach (var option in item.options)
                    {
                        DeepDetectOption captured = option;
                        DeepDetectThreadItem capturedItem = item;
                        AddActionButton(Short(name) + ": " + Short(captured.label), () => Submit(surface, capturedItem.id, captured.id));
                    }
                }
                DeepDetectThreadItem customItem = item;
                AddActionButton("Send custom reply to " + Short(name), () => Submit(surface, customItem.id, "__custom__"));
            }
        }
        SetBodyText(text);
    }

    private void RenderBriefing()
    {
        string text = "BRIEFING\n\nGoals\n";
        if (game.goals != null)
        {
            foreach (var goal in game.goals)
                text += "- " + goal.title + ": " + goal.current + "/" + goal.target + (goal.complete ? " complete" : "") + "\n";
        }

        text += "\nQuests\n";
        if (game.quests != null)
        {
            foreach (var quest in game.quests)
                text += "- " + quest.title + ": " + quest.current + "/" + quest.target + " " + quest.reward + "\n";
        }

        text += "\nLive feed\n";
        if (game.world_feed != null)
        {
            foreach (string line in game.world_feed)
                text += "- " + line + "\n";
        }

        text += "\nAction log\n";
        if (game.action_log != null)
        {
            foreach (string line in game.action_log)
                text += "- " + line + "\n";
        }
        SetBodyText(text);
    }

    private void SetBodyText(string text)
    {
        bodyText.text = DisplaySafe(text);
        bodyText.ForceMeshUpdate();
        float height = Mathf.Max(500f, bodyText.preferredHeight + 24f);
        bodyText.rectTransform.sizeDelta = new Vector2(0, height);
    }

    private void TryStartBackend()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string backendPath = Path.Combine(projectRoot, backendRelativePath);
        if (!Directory.Exists(backendPath))
            return;

        string python = FindExecutable("python.exe");
        if (string.IsNullOrEmpty(python))
            python = FindExecutable("py.exe");
        if (string.IsNullOrEmpty(python))
        {
            SetStatus("Python is not on PATH. Start backend manually before using AI shift.");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = python,
                Arguments = python.EndsWith("py.exe", StringComparison.OrdinalIgnoreCase)
                    ? "-m uvicorn backend.app.main:app --host 127.0.0.1 --port 8765"
                    : "-m uvicorn backend.app.main:app --host 127.0.0.1 --port 8765",
                WorkingDirectory = backendPath,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            backendProcess = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            SetStatus("Could not start backend: " + ex.Message);
        }
#endif
    }

    private static string FindExecutable(string fileName)
    {
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string part in path.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(part))
                continue;
            string candidate = Path.Combine(part.Trim(), fileName);
            if (File.Exists(candidate))
                return candidate;
        }
        return "";
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = DisplaySafe(message);
        UnityEngine.Debug.Log("[DeepDetect] " + message);
    }

    private static string DisplaySafe(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            bool supportedLatin = c == '\n' || c == '\r' || c == '\t' || (c >= ' ' && c <= '\u024F');
            if (!supportedLatin)
                chars[i] = '?';
        }
        return new string(chars);
    }

    private void ClearDynamicButtons()
    {
        if (content == null)
            return;
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            Transform child = content.GetChild(i);
            if (child.name.StartsWith("ActionButton", StringComparison.Ordinal))
                Destroy(child.gameObject);
        }
    }

    private void AddActionButton(string label, UnityEngine.Events.UnityAction action)
    {
        AddButton(content, "ActionButton_" + label, action, label);
    }

    private Button AddButton(RectTransform parent, string label, UnityEngine.Events.UnityAction action, string displayText = null)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(0, 42);
        go.GetComponent<Image>().color = new Color(0.18f, 0.29f, 0.42f, 1f);
        var button = go.GetComponent<Button>();
        button.onClick.AddListener(action);

        var text = CreateText(rect, "Label", DisplaySafe(displayText ?? label), 15, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    private static RectTransform CreatePanel(RectTransform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return go.GetComponent<RectTransform>();
    }

    private static TMP_Text CreateText(RectTransform parent, string name, string text, int size, FontStyles style, TextAlignmentOptions alignment)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = alignment;
        tmp.color = Color.white;
        return tmp;
    }

    private static void Stretch(RectTransform rect)
    {
        Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
    }

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static string Short(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        return value.Length <= 34 ? value : value.Substring(0, 31) + "...";
    }
}
