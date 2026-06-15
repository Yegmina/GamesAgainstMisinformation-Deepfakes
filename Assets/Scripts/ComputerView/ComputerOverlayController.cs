using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class ComputerOverlayController : MonoBehaviour
{
    private const float TabPanelHeight = 820f;
    private const float InboxBodyHeight = 680f;
    private const float TelegramRowHeight = 600f;
    private const float BriefingSystemsHeight = 360f;
    private const float CanvasReferenceWidth = 1920f;
    private const float CanvasReferenceHeight = 1080f;
    private const float MonitorScreenWidthRatio = 0.86f;
    private const float MonitorFallbackWorldWidth = 4.8f;
    private const float MonitorSurfaceOffset = 0.06f;
    private const float FocusDistance = 6.2f;
    private const float FocusHeightOffset = 0.1f;
    private const float FocusFov = 34f;
    private const float DesktopSummaryHeight = 148f;
    private const float DesktopDockHeight = 76f;
    private const string PrimaryMonitorName = "monitor";
    private const string FallbackMonitorName = "Monitor_27__Curved";
    private const string BackendUrlKey = "DeepDetect.BackendUrl";
    private const string TokenKey = "DeepDetect.UnityToken";
    private const string UserKey = "DeepDetect.UnityUser";
    private const string DefaultBackendUrl = "http://127.0.0.1:8765";
    private const string DefaultName = "Unity Player";
    private const string DefaultEmail = "unity.player@deepdetectgame.dev";
    private const string DefaultPassword = "unity-local-player-2026";

    private static readonly Color Ink = Html("#17212b");
    private static readonly Color Muted = Html("#657383");
    private static readonly Color Line = Html("#d8e0e8");
    private static readonly Color Paper = Html("#f7f9fb");
    private static readonly Color Panel = Color.white;
    private static readonly Color Blue = Html("#1e5aa8");
    private static readonly Color BlueDark = Html("#16447f");
    private static readonly Color Green = Html("#19745b");
    private static readonly Color Red = Html("#b9383f");
    private static readonly Color Amber = Html("#a66300");

    private static ComputerOverlayController instance;

    private ComputerApiClient api;
    private ComputerUser user;
    private ComputerGameState currentGame;
    private string activeTab = "home";
    private string activeEmailId = string.Empty;
    private string activeTelegramId = string.Empty;
    private bool initialized;
    private bool initializing;
    private bool busy;
    private bool usingWorldMonitor;
    private bool focusActive;
    private Vector3 savedCameraPosition;
    private Quaternion savedCameraRotation;
    private float savedCameraFov;

    private GameObject canvasObject;
    private Canvas canvas;
    private CanvasScaler canvasScaler;
    private CanvasGroup canvasGroup;
    private TMP_Text titleText;
    private TMP_Text statusText;
    private TMP_Text scoreText;
    private Button advanceButton;
    private Button refreshButton;
    private GameObject bootStateObject;
    private TMP_Text bootTitleText;
    private TMP_Text bootBodyText;
    private GameObject activeGameObject;
    private RectTransform missionStrip;
    private RectTransform tabButtons;
    private RectTransform tabHost;

    public static void OpenComputer()
    {
        EnsureInstance().Open();
    }

    public static void CloseComputer()
    {
        if (instance != null)
        {
            instance.Close();
        }
    }

    public static void PreloadComputer()
    {
        EnsureInstance().Preload();
    }

    private static ComputerOverlayController EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        GameObject host = new GameObject("ComputerOverlayRuntime");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ComputerOverlayController>();
        return instance;
    }

    private void OnDestroy()
    {
        ExitFocusMode();

        if (instance == this)
        {
            instance = null;
        }

        if (canvasObject != null)
        {
            Destroy(canvasObject);
            canvasObject = null;
        }
    }

    private void Open()
    {
        Debug.Log("[ComputerOverlay] Open requested.");
        if (canvasObject == null)
        {
            BuildUi();
        }

        AttachCanvasToComputerSurface();
        canvasObject.SetActive(true);
        EnterFocusMode();
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        RenderAll();

        if (!initialized && !initializing)
        {
            _ = InitializeAsync();
        }
        else
        {
            RenderAll();
        }
    }

    private void Preload()
    {
        if (canvasObject == null)
        {
            BuildUi();
        }

        AttachCanvasToComputerSurface();
        canvasObject.SetActive(false);
        if (!initialized && !initializing)
        {
            _ = InitializeAsync();
        }
    }

    private void Close()
    {
        ExitFocusMode();

        if (canvasObject != null)
        {
            canvasObject.SetActive(false);
        }
    }

    private async Task InitializeAsync()
    {
        initializing = true;
        SetBusy(true, "Preparing DeepDetect runtime shift...");

        string backendUrl = PlayerPrefs.GetString(BackendUrlKey, DefaultBackendUrl);
        string savedToken = PlayerPrefs.GetString(TokenKey, string.Empty);
        api = new ComputerApiClient(backendUrl, savedToken);

        try
        {
            bool healthy = await api.HealthAsync();
            if (!healthy)
            {
                initialized = false;
                user = null;
                currentGame = null;
                SetBusy(false, $"Backend offline at {backendUrl}. Start the FastAPI server, then press Refresh.");
                RenderAll();
                return;
            }

            await EnsureAuthenticatedAsync();
            initialized = true;
            await EnsureRuntimeGameAsync();
            SetBusy(false, "Ready.");
            RenderAll();
        }
        catch (Exception ex)
        {
            initialized = user != null;
            SetBusy(false, ex.Message);
            RenderAll();
        }
        finally
        {
            initializing = false;
        }
    }

    private async Task EnsureAuthenticatedAsync()
    {
        if (!string.IsNullOrWhiteSpace(api.Token))
        {
            try
            {
                ComputerMeResponse me = await api.MeAsync();
                user = me.user;
                PlayerPrefs.SetString(UserKey, JsonConvert.SerializeObject(user));
                return;
            }
            catch (ComputerApiException ex)
            {
                if (ex.StatusCode != 401)
                {
                    throw;
                }
            }
        }

        ComputerAuthResponse auth = null;
        try
        {
            auth = await api.RegisterAsync(DefaultName, DefaultEmail, DefaultPassword);
        }
        catch (ComputerApiException ex)
        {
            if (ex.StatusCode != 409)
            {
                throw;
            }

            auth = await api.LoginAsync(DefaultEmail, DefaultPassword);
        }

        api.Token = auth.token;
        user = auth.user;
        PlayerPrefs.SetString(TokenKey, auth.token);
        PlayerPrefs.SetString(UserKey, JsonConvert.SerializeObject(auth.user));
    }

    private async Task EnsureRuntimeGameAsync()
    {
        if (currentGame != null)
        {
            return;
        }

        ComputerGameResponse response = await api.GenerateGameAsync();
        SetCurrentGame(response.game, false);
    }

    private async void RefreshClicked()
    {
        if (busy)
        {
            return;
        }

        if (!initialized)
        {
            await InitializeAsync();
            return;
        }

        await RunRequestAsync("Refreshing runtime shift...", async () =>
        {
            if (currentGame == null)
            {
                await EnsureRuntimeGameAsync();
                return;
            }

            try
            {
                ComputerGameResponse response = await api.GetGameAsync(currentGame.id);
                SetCurrentGame(response.game, false);
            }
            catch (ComputerApiException ex)
            {
                if (ex.StatusCode != 404)
                {
                    throw;
                }

                currentGame = null;
                await EnsureRuntimeGameAsync();
            }
        });
    }

    private async void AdvanceWorldClicked()
    {
        if (!CanUseGame())
        {
            return;
        }

        await RunRequestAsync("Simulating world update...", async () =>
        {
            ComputerGameResponse response = await api.TickAsync(currentGame.id);
            SetCurrentGame(response.game, true);
        });
    }

    private async void SendActionClicked(string surface, string itemId, string choice)
    {
        if (!CanUseGame())
        {
            return;
        }

        await RunRequestAsync("Sending decision...", async () =>
        {
            ComputerGameResponse response = await api.SendActionAsync(currentGame.id, surface, itemId, choice);
            SetCurrentGame(response.game, true);
        });
    }

    private async void SendCustomReplyClicked(string surface, string itemId, TMP_InputField input)
    {
        if (!CanUseGame() || input == null)
        {
            return;
        }

        string customText = (input.text ?? string.Empty).Trim();
        if (customText.Length == 0)
        {
            SetStatus("Write a reply before sending.");
            return;
        }

        await RunRequestAsync("Sending reply...", async () =>
        {
            ComputerGameResponse response = await api.SendActionAsync(currentGame.id, surface, itemId, "__custom__", customText);
            SetCurrentGame(response.game, true);
        });
    }

    private async Task RunRequestAsync(string busyMessage, Func<Task> action, bool renderAfter = true)
    {
        if (busy)
        {
            return;
        }

        SetBusy(true, busyMessage);
        try
        {
            await action();
            if (statusText != null && statusText.text == busyMessage)
            {
                SetStatus("Ready.");
            }
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message);
        }
        finally
        {
            SetBusy(false, statusText != null ? statusText.text : "Ready.");
            if (renderAfter)
            {
                RenderAll();
            }
        }
    }

    private bool CanUseBackend()
    {
        if (busy)
        {
            return false;
        }

        if (!initialized || api == null || user == null)
        {
            SetStatus("Backend is not connected. Press Refresh.");
            return false;
        }

        return true;
    }

    private bool CanUseGame()
    {
        if (!CanUseBackend())
        {
            return false;
        }

        if (currentGame == null || string.IsNullOrWhiteSpace(currentGame.id))
        {
            SetStatus("Runtime shift is not ready yet. Press Refresh.");
            return false;
        }

        return true;
    }

    private void SetCurrentGame(ComputerGameState nextGame, bool evaluateParanoia)
    {
        ComputerGameState previousGame = currentGame;
        currentGame = NormalizeGame(nextGame);
        if (currentGame == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(activeEmailId) || !EmailExists(activeEmailId))
        {
            activeEmailId = FirstOpenEmailId();
        }

        if (string.IsNullOrWhiteSpace(activeTelegramId) || !TelegramExists(activeTelegramId))
        {
            activeTelegramId = FirstOpenTelegramId();
        }

        if (GlobalCanvasPersistent.Instance != null)
        {
            GlobalCanvasPersistent.Instance.SetPoints(Mathf.Max(0, currentGame.score));
        }

        if (evaluateParanoia)
        {
            ApplyParanoiaDelta(previousGame, currentGame);
        }
    }

    private void ApplyParanoiaDelta(ComputerGameState previousGame, ComputerGameState nextGame)
    {
        if (previousGame == null || nextGame == null || GlobalCanvasPersistent.Instance == null)
        {
            return;
        }

        int delta = 0;
        Dictionary<string, ComputerNewsItem> oldNews = new Dictionary<string, ComputerNewsItem>();
        foreach (ComputerNewsItem item in previousGame.newsItems ?? new List<ComputerNewsItem>())
        {
            if (!string.IsNullOrWhiteSpace(item.id))
            {
                oldNews[item.id] = item;
            }
        }

        foreach (ComputerNewsItem item in nextGame.newsItems ?? new List<ComputerNewsItem>())
        {
            if (item == null || string.IsNullOrWhiteSpace(item.id) || item.correct != false || string.IsNullOrWhiteSpace(item.decision))
            {
                continue;
            }

            ComputerNewsItem old;
            bool wasResolved = oldNews.TryGetValue(item.id, out old) && !string.IsNullOrWhiteSpace(old.decision);
            if (!wasResolved)
            {
                delta += 10;
            }
        }

        delta += CountNewWrongThreadResolutions(previousGame.emails, nextGame.emails) * 6;
        delta += CountNewWrongThreadResolutions(previousGame.telegramThreads, nextGame.telegramThreads) * 6;

        if (delta > 0)
        {
            GlobalCanvasPersistent.Instance.AddParanoia(delta);
        }
    }

    private static int CountNewWrongThreadResolutions<T>(List<T> previous, List<T> next)
    {
        Dictionary<string, bool> oldResolved = new Dictionary<string, bool>();
        foreach (T item in previous ?? new List<T>())
        {
            string id = ThreadId(item);
            if (!string.IsNullOrWhiteSpace(id))
            {
                oldResolved[id] = ThreadResolved(item);
            }
        }

        int count = 0;
        foreach (T item in next ?? new List<T>())
        {
            string id = ThreadId(item);
            if (string.IsNullOrWhiteSpace(id) || ThreadCorrect(item) != false || !ThreadResolved(item))
            {
                continue;
            }

            bool wasResolved;
            if (!oldResolved.TryGetValue(id, out wasResolved) || !wasResolved)
            {
                count++;
            }
        }

        return count;
    }

    private static string ThreadId<T>(T item)
    {
        ComputerEmailItem email = item as ComputerEmailItem;
        if (email != null)
        {
            return email.id;
        }

        ComputerTelegramThread telegram = item as ComputerTelegramThread;
        return telegram != null ? telegram.id : string.Empty;
    }

    private static bool ThreadResolved<T>(T item)
    {
        ComputerEmailItem email = item as ComputerEmailItem;
        if (email != null)
        {
            return email.resolved || !string.IsNullOrWhiteSpace(email.selected);
        }

        ComputerTelegramThread telegram = item as ComputerTelegramThread;
        return telegram != null && (telegram.resolved || !string.IsNullOrWhiteSpace(telegram.selected));
    }

    private static bool? ThreadCorrect<T>(T item)
    {
        ComputerEmailItem email = item as ComputerEmailItem;
        if (email != null)
        {
            return email.correct;
        }

        ComputerTelegramThread telegram = item as ComputerTelegramThread;
        return telegram != null ? telegram.correct : null;
    }

    private void BuildUi()
    {
        canvasObject = new GameObject("ComputerCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(null, false);
        DontDestroyOnLoad(canvasObject);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.sizeDelta = new Vector2(CanvasReferenceWidth, CanvasReferenceHeight);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 6000;

        canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasScaler.referencePixelsPerUnit = 100f;
        canvasScaler.dynamicPixelsPerUnit = 3f;

        canvasObject.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        GameObject shell = PanelObject(canvasObject.transform, "ComputerShell", Paper);
        Stretch(shell.GetComponent<RectTransform>());
        VerticalLayoutGroup shellLayout = shell.AddComponent<VerticalLayoutGroup>();
        shellLayout.padding = new RectOffset(22, 22, 16, 18);
        shellLayout.spacing = 12;
        shellLayout.childControlWidth = true;
        shellLayout.childForceExpandWidth = true;
        shellLayout.childControlHeight = true;
        shellLayout.childForceExpandHeight = false;

        BuildTopbar(shell.transform);
        BuildBootState(shell.transform);
        BuildActiveGame(shell.transform);

        canvasObject.SetActive(false);
        Debug.Log("[ComputerOverlay] UI built.");
    }

    private void AttachCanvasToComputerSurface()
    {
        if (canvasObject == null || canvas == null || canvasScaler == null)
        {
            return;
        }

        Transform monitor = FindMonitorTransform();
        if (monitor == null)
        {
            ConfigureScreenFallback();
            Debug.LogWarning("[ComputerOverlay] Monitor anchor not found; using screen-space fallback.");
            return;
        }

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasScaler.referencePixelsPerUnit = 100f;
        canvasScaler.dynamicPixelsPerUnit = 3f;

        Bounds bounds;
        bool hasBounds = TryGetRendererBounds(monitor, out bounds);
        Vector3 surfaceCenter = hasBounds ? bounds.center : monitor.position;
        Vector3 surfaceForward = monitor.forward.sqrMagnitude > 0.001f ? monitor.forward.normalized : Vector3.forward;
        Vector3 surfaceUp = Vector3.Dot(monitor.up, Vector3.up) > 0.1f ? monitor.up.normalized : Vector3.up;
        float worldWidth = hasBounds ? Mathf.Clamp(bounds.size.x * MonitorScreenWidthRatio, 3.2f, 8.2f) : MonitorFallbackWorldWidth;
        float worldScale = worldWidth / CanvasReferenceWidth;
        float forwardHalfDepth = hasBounds ? ProjectBoundsExtent(bounds.extents, surfaceForward) : 0f;

        canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
        canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        canvasRect.sizeDelta = new Vector2(CanvasReferenceWidth, CanvasReferenceHeight);
        canvasRect.anchoredPosition = Vector2.zero;

        canvasObject.transform.SetParent(null, false);
        canvasObject.transform.position = surfaceCenter - surfaceForward * (forwardHalfDepth + MonitorSurfaceOffset);
        canvasObject.transform.rotation = Quaternion.LookRotation(surfaceForward, surfaceUp);
        canvasObject.transform.localScale = Vector3.one * worldScale;
        usingWorldMonitor = true;
    }

    private void ConfigureScreenFallback()
    {
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(CanvasReferenceWidth, CanvasReferenceHeight);
        canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight = 0.5f;

        canvasObject.transform.SetParent(null, false);
        canvasObject.transform.localPosition = Vector3.zero;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale = Vector3.one;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        canvasRect.pivot = new Vector2(0.5f, 0.5f);
        usingWorldMonitor = false;
    }

    private void EnterFocusMode()
    {
        if (!usingWorldMonitor || canvasObject == null || focusActive)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        savedCameraPosition = camera.transform.position;
        savedCameraRotation = camera.transform.rotation;
        savedCameraFov = camera.fieldOfView;
        focusActive = true;

        canvas.worldCamera = camera;
        Vector3 target = canvasObject.transform.position + Vector3.up * FocusHeightOffset;
        Vector3 cameraPosition = canvasObject.transform.position - canvasObject.transform.forward * FocusDistance + Vector3.up * FocusHeightOffset;
        camera.transform.position = cameraPosition;
        camera.transform.rotation = Quaternion.LookRotation((target - cameraPosition).normalized, Vector3.up);
        camera.fieldOfView = FocusFov;
    }

    private void ExitFocusMode()
    {
        if (!focusActive)
        {
            return;
        }

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = savedCameraPosition;
            camera.transform.rotation = savedCameraRotation;
            camera.fieldOfView = savedCameraFov;
        }

        focusActive = false;
    }

    private static Transform FindMonitorTransform()
    {
        GameObject monitor = GameObject.Find(PrimaryMonitorName);
        if (monitor != null)
        {
            return monitor.transform;
        }

        monitor = GameObject.Find(FallbackMonitorName);
        return monitor != null ? monitor.transform : null;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
        {
            return false;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool initializedBounds = false;
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null || renderer.bounds.size.sqrMagnitude < 0.001f)
            {
                continue;
            }

            if (!initializedBounds)
            {
                bounds = renderer.bounds;
                initializedBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initializedBounds;
    }

    private static float ProjectBoundsExtent(Vector3 extents, Vector3 direction)
    {
        Vector3 absoluteDirection = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
        return extents.x * absoluteDirection.x + extents.y * absoluteDirection.y + extents.z * absoluteDirection.z;
    }

    private void BuildTopbar(Transform parent)
    {
        GameObject topbar = PanelObject(parent, "Topbar", Panel);
        Layout(topbar, -1f, 104f, 1f, 0f);
        HorizontalLayoutGroup layout = topbar.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(22, 22, 14, 14);
        layout.spacing = 16;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        GameObject copy = Element(parent: topbar.transform, name: "TitleCopy");
        Layout(copy, -1f, -1f, 1f, 1f);
        VerticalLayoutGroup copyLayout = copy.AddComponent<VerticalLayoutGroup>();
        copyLayout.spacing = 2;
        copyLayout.childAlignment = TextAnchor.MiddleLeft;
        Text(copy.transform, "Kicker", "New Media Desk", 18, Blue, FontStyles.Bold);
        titleText = Text(copy.transform, "Title", "DeepDetect", 32, Ink, FontStyles.Bold);
        statusText = Text(copy.transform, "Status", "Press Refresh if the backend is offline.", 16, Muted);

        GameObject actions = Element(parent: topbar.transform, name: "Actions");
        Layout(actions, 400f, -1f, 0f, 1f);
        HorizontalLayoutGroup actionLayout = actions.AddComponent<HorizontalLayoutGroup>();
        actionLayout.spacing = 10;
        actionLayout.childAlignment = TextAnchor.MiddleRight;
        actionLayout.childControlHeight = true;
        actionLayout.childForceExpandHeight = false;
        actionLayout.childForceExpandWidth = false;

        GameObject scoreBox = PanelObject(actions.transform, "ScoreBox", Html("#eef4fb"));
        Layout(scoreBox, 116f, 72f, 0f, 0f);
        VerticalLayoutGroup scoreLayout = scoreBox.AddComponent<VerticalLayoutGroup>();
        scoreLayout.padding = new RectOffset(10, 10, 8, 8);
        scoreLayout.childAlignment = TextAnchor.MiddleCenter;
        Text(scoreBox.transform, "ScoreLabel", "Score", 14, Muted, FontStyles.Bold).alignment = TextAlignmentOptions.Center;
        scoreText = Text(scoreBox.transform, "Score", "0", 28, BlueDark, FontStyles.Bold);
        scoreText.alignment = TextAlignmentOptions.Center;

        advanceButton = Button(actions.transform, "Advance World", BlueDark, Color.white, AdvanceWorldClicked, 152f, 48f);
        refreshButton = Button(actions.transform, "Refresh", Panel, BlueDark, RefreshClicked, 104f, 48f);
    }

    private void BuildBootState(Transform parent)
    {
        bootStateObject = PanelObject(parent, "RuntimeBootState", Panel);
        Layout(bootStateObject, -1f, -1f, 1f, 1f);
        VerticalLayoutGroup layout = bootStateObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(42, 42, 80, 80);
        layout.spacing = 14;
        layout.childAlignment = TextAnchor.UpperCenter;
        bootTitleText = Text(bootStateObject.transform, "Title", "Preparing runtime shift", 42, Ink, FontStyles.Bold);
        bootTitleText.alignment = TextAlignmentOptions.Center;
        bootBodyText = Text(bootStateObject.transform, "Copy", "The computer starts one DeepDetect shift for this Unity run.", 20, Muted);
        bootBodyText.alignment = TextAlignmentOptions.Center;
    }

    private void BuildActiveGame(Transform parent)
    {
        activeGameObject = Element(parent: parent, name: "ActiveGame");
        Layout(activeGameObject, -1f, -1f, 1f, 1f);
        VerticalLayoutGroup layout = activeGameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        GameObject mission = Element(parent: activeGameObject.transform, name: "MissionStrip");
        missionStrip = mission.GetComponent<RectTransform>();
        Layout(mission, -1f, DesktopSummaryHeight, 1f, 0f);
        HorizontalLayoutGroup missionLayout = mission.AddComponent<HorizontalLayoutGroup>();
        missionLayout.spacing = 12;
        missionLayout.childControlWidth = true;
        missionLayout.childForceExpandWidth = true;
        missionLayout.childControlHeight = true;
        missionLayout.childForceExpandHeight = true;

        GameObject tabs = Element(parent: activeGameObject.transform, name: "Tabs");
        tabButtons = tabs.GetComponent<RectTransform>();
        Layout(tabs, -1f, DesktopDockHeight, 1f, 0f);
        HorizontalLayoutGroup tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
        tabLayout.padding = new RectOffset(0, 0, 8, 8);
        tabLayout.spacing = 10;
        tabLayout.childControlWidth = true;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandHeight = true;

        GameObject host = PanelObject(activeGameObject.transform, "TabHost", Panel);
        tabHost = host.GetComponent<RectTransform>();
        Layout(host, -1f, -1f, 1f, 1f);
    }

    private void RenderAll()
    {
        if (canvasObject == null)
        {
            return;
        }

        RenderTopbar();

        bool hasGame = currentGame != null;
        bootStateObject.SetActive(!hasGame);
        activeGameObject.SetActive(hasGame);

        if (hasGame)
        {
            RenderMissionStrip();
            RenderTabs();
        }
    }

    private void RenderTopbar()
    {
        string userName = user != null && !string.IsNullOrWhiteSpace(user.name) ? user.name : DefaultName;
        titleText.text = DisplayText(currentGame != null && !string.IsNullOrWhiteSpace(currentGame.title)
            ? currentGame.title
            : $"DeepDetect / {userName}");

        scoreText.text = currentGame != null ? currentGame.score.ToString() : "0";
        advanceButton.gameObject.SetActive(currentGame != null);
        advanceButton.interactable = currentGame != null && !busy && initialized;
        refreshButton.interactable = !busy;

        if (currentGame != null && !busy && ShouldShowAgentStatus())
        {
            string agent = $"Agent runtime: {Fallback(currentGame.agentMode, "local")} / {Fallback(currentGame.agentModel, "unknown")}";
            if (!string.IsNullOrWhiteSpace(currentGame.lastWorldAgentMode))
            {
                agent += $" / last world: {currentGame.lastWorldAgentMode}";
            }
            statusText.text = DisplayText(agent);
        }
    }

    private bool ShouldShowAgentStatus()
    {
        if (statusText == null || string.IsNullOrWhiteSpace(statusText.text))
        {
            return true;
        }

        return statusText.text == "Ready." || statusText.text.StartsWith("Agent runtime:", StringComparison.Ordinal);
    }
    private void RenderMissionStrip()
    {
        Clear(missionStrip);

        GameObject shiftPanel = Card(missionStrip, "ShiftStatus");
        Layout(shiftPanel, -1f, -1f, 1f, 1f);
        Text(shiftPanel.transform, "Heading", currentGame.complete ? "Shift complete" : "Shift active", 22, currentGame.complete ? Green : BlueDark, FontStyles.Bold);
        Text(shiftPanel.transform, "Subline", $"{Fallback(currentGame.title, "DeepDetect shift")} / Tick {currentGame.worldTick}", 17, Muted);
        Text(shiftPanel.transform, "OpenItems", $"Open work: {OpenNewsCount(currentGame.newsItems)} news / {OpenThreadCount(currentGame.emails)} inbox / {OpenThreadCount(currentGame.telegramThreads)} Telegram", 18, Ink, FontStyles.Bold);

        GameObject valuesPanel = Card(missionStrip, "ValueSnapshot");
        Layout(valuesPanel, -1f, -1f, 1f, 1f);
        Text(valuesPanel.transform, "Heading", "Values", 22, Ink, FontStyles.Bold);
        foreach (ComputerValue value in ValuesList())
        {
            AddMeter(valuesPanel.transform, Fallback(value.label, "Value"), value.value, value.description);
        }

        GameObject feedPanel = Card(missionStrip, "LatestFeed");
        Layout(feedPanel, -1f, -1f, 1f, 1f);
        Text(feedPanel.transform, "Heading", "Latest", 22, Ink, FontStyles.Bold);
        string latest = LatestLine(currentGame.actionLog, currentGame.questLog, currentGame.worldFeed, currentGame.generationLog);
        Text(feedPanel.transform, "LatestLine", latest, 17, Muted);
    }

    private void RenderTabs()
    {
        Clear(tabButtons);
        AddTabButton("home", "Home");
        AddTabButton("news", "Newsdesk");
        AddTabButton("email", "Inbox");
        AddTabButton("telegram", "Telegram");
        AddTabButton("briefing", "Briefing");

        Clear(tabHost);
        RectTransform content;
        CreateScroll(tabHost, "TabScroll", out content, false);
        Stretch(content.parent.parent.GetComponent<RectTransform>());

        switch (activeTab)
        {
            case "home":
                RenderDesktop(content);
                break;
            case "email":
                RenderInbox(content);
                break;
            case "telegram":
                RenderTelegram(content);
                break;
            case "briefing":
                RenderBriefing(content);
                break;
            default:
                RenderNewsdesk(content);
                break;
        }
    }

    private void AddTabButton(string tab, string label)
    {
        bool selected = activeTab == tab;
        Button button = Button(tabButtons, label, selected ? BlueDark : Panel, selected ? Color.white : BlueDark, () =>
        {
            activeTab = tab;
            RenderTabs();
        }, tab == "home" ? 124f : 166f, 56f);
        button.interactable = !selected && !busy;
    }

    private void RenderDesktop(Transform parent)
    {
        GameObject desktop = Element(parent: parent, name: "Desktop");
        Layout(desktop, -1f, TabPanelHeight, 1f, 0f);
        VerticalLayoutGroup desktopLayout = desktop.AddComponent<VerticalLayoutGroup>();
        desktopLayout.padding = new RectOffset(24, 24, 24, 24);
        desktopLayout.spacing = 18;
        desktopLayout.childControlWidth = true;
        desktopLayout.childForceExpandWidth = true;
        desktopLayout.childControlHeight = true;
        desktopLayout.childForceExpandHeight = false;

        Text(desktop.transform, "Heading", "DeepDetect workstation", 36, Ink, FontStyles.Bold);
        Text(desktop.transform, "Subheading", "Choose one workspace. Each app uses the same live runtime shift.", 20, Muted);

        GameObject appGrid = Element(parent: desktop.transform, name: "AppGrid");
        Layout(appGrid, -1f, 540f, 1f, 0f);
        GridLayoutGroup grid = appGrid.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(430f, 238f);
        grid.spacing = new Vector2(18f, 18f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;

        AddDesktopApp(appGrid.transform, "news", "Newsdesk", $"{OpenNewsCount(currentGame.newsItems)} decisions open", "Publish credible stories and reject manipulated claims.", BlueDark);
        AddDesktopApp(appGrid.transform, "email", "Inbox", ThreadSummary(currentGame.emails), "Handle newsroom pressure with evidence-first replies.", Green);
        AddDesktopApp(appGrid.transform, "telegram", "Telegram", ThreadSummary(currentGame.telegramThreads), "Slow down private-message rumors without escalating.", Html("#2b6cb0"));
        AddDesktopApp(appGrid.transform, "briefing", "Briefing", currentGame.complete ? "Review complete shift" : "Rules and action log", "Check values, quests, rules, and the live action log.", Amber);
    }

    private void AddDesktopApp(Transform parent, string tab, string title, string meta, string description, Color accent)
    {
        GameObject card = Card(parent, $"App-{tab}");
        Layout(card, 430f, 238f, 0f, 0f);
        Image image = card.GetComponent<Image>();
        image.color = Color.Lerp(Panel, accent, 0.08f);

        Text(card.transform, "Title", title, 30, Ink, FontStyles.Bold);
        Text(card.transform, "Meta", meta, 18, accent, FontStyles.Bold);
        Text(card.transform, "Description", description, 18, Ink);
        Button(card.transform, $"Open {title}", accent, Color.white, () =>
        {
            activeTab = tab;
            RenderTabs();
        }, 190f, 52f);
    }

    private void RenderNewsdesk(Transform parent)
    {
        List<ComputerNewsItem> items = currentGame.newsItems ?? new List<ComputerNewsItem>();
        if (items.Count == 0)
        {
            Text(parent, "EmptyNews", "No newsroom items are available in this runtime shift.", 22, Muted);
            return;
        }

        GameObject shell = PanelObject(parent, "NewsdeskApp", Html("#f5f6f8"));
        Layout(shell, -1f, TabPanelHeight, 1f, 0f);
        VerticalLayoutGroup shellLayout = shell.AddComponent<VerticalLayoutGroup>();
        shellLayout.padding = new RectOffset(20, 20, 18, 20);
        shellLayout.spacing = 14;
        shellLayout.childControlWidth = true;
        shellLayout.childForceExpandWidth = true;
        shellLayout.childControlHeight = true;
        shellLayout.childForceExpandHeight = false;

        Text(shell.transform, "Header", "Newsdesk", 34, Ink, FontStyles.Bold);
        Text(shell.transform, "Status", $"{items.Count} incoming wires / {OpenNewsCount(items)} open decisions", 20, Muted, FontStyles.Bold);

        for (int i = 0; i < items.Count; i++)
        {
            AddNewsCard(shell.transform, items[i], i == 0);
        }
    }

    private void AddNewsCard(Transform parent, ComputerNewsItem item, bool lead)
    {
        GameObject card = Card(parent, lead ? "LeadStory" : "WireRow");
        Layout(card, -1f, lead ? 306f : 246f, 1f, 0f);

        Text(card.transform, "Meta", $"{(lead ? "Lead slot" : "Wire")} / {SourceHost(item.url, item.source)} / {NewsStatus(item)}", 18, BlueDark, FontStyles.Bold);
        Text(card.transform, "Title", Fallback(item.title, "Untitled story"), lead ? 30 : 24, Ink, FontStyles.Bold);
        Text(card.transform, "Summary", Fallback(item.summary, "No summary available."), lead ? 20 : 18, Ink);
        Text(card.transform, "Evidence", $"Source: {Fallback(item.source, "pending")} / Pressure: {Fallback(item.publicPressure, "pending")} / Desk note: {Fallback(item.editorNote, "No note")}", 17, Muted);
        AddResult(card.transform, item.correct);

        GameObject choices = Element(parent: card.transform, name: "Choices");
        Layout(choices, -1f, 54f, 1f, 0f);
        HorizontalLayoutGroup choiceLayout = choices.AddComponent<HorizontalLayoutGroup>();
        choiceLayout.spacing = 10;
        Button publish = Button(choices.transform, "Publish", Green, Color.white, () => SendActionClicked("news", item.id, "publish"), 142f, 50f);
        Button reject = Button(choices.transform, "Reject", Red, Color.white, () => SendActionClicked("news", item.id, "reject"), 142f, 50f);
        bool decided = !string.IsNullOrWhiteSpace(item.decision);
        publish.interactable = !decided && !busy;
        reject.interactable = !decided && !busy;
    }

    private void RenderInbox(Transform parent)
    {
        List<ComputerEmailItem> emails = currentGame.emails ?? new List<ComputerEmailItem>();
        if (emails.Count == 0)
        {
            Text(parent, "EmptyInbox", "No inbox messages are available in this runtime shift.", 22, Muted);
            return;
        }

        if (string.IsNullOrWhiteSpace(activeEmailId) || !EmailExists(activeEmailId))
        {
            activeEmailId = FirstOpenEmailId();
        }

        ComputerEmailItem active = FindEmail(activeEmailId) ?? emails[0];

        GameObject shell = PanelObject(parent, "InboxApp", Html("#f2f6fb"));
        Layout(shell, -1f, TabPanelHeight, 1f, 0f);
        VerticalLayoutGroup shellLayout = shell.AddComponent<VerticalLayoutGroup>();
        shellLayout.padding = new RectOffset(20, 20, 18, 20);
        shellLayout.spacing = 14;
        shellLayout.childControlWidth = true;
        shellLayout.childForceExpandWidth = true;
        shellLayout.childControlHeight = true;
        shellLayout.childForceExpandHeight = false;

        Text(shell.transform, "Header", "Inbox", 34, Ink, FontStyles.Bold);
        Text(shell.transform, "Subheader", $"{emails.Count} newsroom threads / {OpenThreadCount(emails)} need a reply", 20, Muted, FontStyles.Bold);

        GameObject body = Element(parent: shell.transform, name: "GmailBody");
        Layout(body, -1f, 690f, 1f, 0f);
        HorizontalLayoutGroup bodyLayout = body.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = 14;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = false;
        bodyLayout.childForceExpandHeight = true;

        GameObject left = Card(body.transform, "InboxList");
        Layout(left, 450f, -1f, 0f, 1f);
        Text(left.transform, "Mailbox", "Threads", 24, BlueDark, FontStyles.Bold);
        foreach (ComputerEmailItem item in emails)
        {
            AddEmailRow(left.transform, item, item.id == active.id);
        }

        GameObject reader = Card(body.transform, "Reader");
        Layout(reader, -1f, -1f, 1f, 1f);
        Text(reader.transform, "Subject", Fallback(active.subject, "No subject"), 30, Ink, FontStyles.Bold);
        Text(reader.transform, "Sender", $"{Fallback(active.fromName, "Sender")} <{Fallback(active.fromEmail, "unknown")}> / {ThreadProgress(active)}", 18, Muted);
        AddThread(reader.transform, EmailMessages(active), active.fromName);
        AddResult(reader.transform, active.correct);
        AddOptionButtons(reader.transform, "email", active.id, active.options, ThreadResolved(active));
        if (!ThreadResolved(active))
        {
            AddCustomReply(reader.transform, "email", active.id, "Write your own newsroom reply...");
        }
    }

    private void AddEmailRow(Transform parent, ComputerEmailItem item, bool selected)
    {
        GameObject row = PanelObject(parent, "EmailRow", selected ? Html("#e7f0ff") : Panel);
        Layout(row, -1f, 104f, 1f, 0f);
        Button button = row.AddComponent<Button>();
        button.targetGraphic = row.GetComponent<Image>();
        button.onClick.AddListener(() =>
        {
            activeEmailId = item.id;
            RenderTabs();
        });

        VerticalLayoutGroup layout = row.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 4;

        Text(row.transform, "From", Fallback(item.fromName, "Sender"), 18, Ink, FontStyles.Bold);
        Text(row.transform, "Subject", Fallback(item.subject, "No subject"), 17, Ink);
        Text(row.transform, "Preview", ThreadProgress(item), 16, Muted);
    }

    private void RenderTelegram(Transform parent)
    {
        List<ComputerTelegramThread> threads = currentGame.telegramThreads ?? new List<ComputerTelegramThread>();
        if (threads.Count == 0)
        {
            Text(parent, "EmptyTelegram", "No Telegram sidequests are available in this runtime shift.", 22, Muted);
            return;
        }

        if (string.IsNullOrWhiteSpace(activeTelegramId) || !TelegramExists(activeTelegramId))
        {
            activeTelegramId = FirstOpenTelegramId();
        }

        ComputerTelegramThread active = FindTelegram(activeTelegramId) ?? threads[0];

        GameObject shell = PanelObject(parent, "TelegramApp", Html("#eef3f8"));
        Layout(shell, -1f, TabPanelHeight, 1f, 0f);
        VerticalLayoutGroup shellLayout = shell.AddComponent<VerticalLayoutGroup>();
        shellLayout.padding = new RectOffset(20, 20, 18, 20);
        shellLayout.spacing = 14;
        shellLayout.childControlWidth = true;
        shellLayout.childForceExpandWidth = true;
        shellLayout.childControlHeight = true;
        shellLayout.childForceExpandHeight = false;

        Text(shell.transform, "Header", "Telegram", 34, Ink, FontStyles.Bold);
        Text(shell.transform, "Subheader", $"{threads.Count} private threads / {OpenThreadCount(threads)} need a reply", 20, Muted, FontStyles.Bold);

        GameObject body = Element(parent: shell.transform, name: "TelegramBody");
        Layout(body, -1f, 690f, 1f, 0f);
        HorizontalLayoutGroup bodyLayout = body.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = 14;
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = true;
        bodyLayout.childForceExpandWidth = false;
        bodyLayout.childForceExpandHeight = true;

        GameObject left = Card(body.transform, "ThreadList");
        Layout(left, 430f, -1f, 0f, 1f);
        Text(left.transform, "Threads", "Chats", 24, BlueDark, FontStyles.Bold);
        foreach (ComputerTelegramThread thread in threads)
        {
            AddTelegramRow(left.transform, thread, thread.id == active.id);
        }

        GameObject conversation = Card(body.transform, "Conversation");
        Layout(conversation, -1f, -1f, 1f, 1f);
        Text(conversation.transform, "Contact", Fallback(active.contact, "Contact"), 30, Ink, FontStyles.Bold);
        Text(conversation.transform, "Meta", $"{Fallback(active.relationship, "relationship")} / {ThreadProgress(active)}", 18, Muted);
        AddThread(conversation.transform, active.messages ?? new List<JToken>(), active.contact);
        AddResult(conversation.transform, active.correct);
        AddOptionButtons(conversation.transform, "telegram", active.id, active.options, ThreadResolved(active));
        if (!ThreadResolved(active))
        {
            AddCustomReply(conversation.transform, "telegram", active.id, "Write your own message...");
        }
    }

    private void AddTelegramRow(Transform parent, ComputerTelegramThread thread, bool selected)
    {
        GameObject row = PanelObject(parent, "TelegramRow", selected ? Html("#e2f0ff") : Panel);
        Layout(row, -1f, 104f, 1f, 0f);
        Button button = row.AddComponent<Button>();
        button.targetGraphic = row.GetComponent<Image>();
        button.onClick.AddListener(() =>
        {
            activeTelegramId = thread.id;
            RenderTabs();
        });

        VerticalLayoutGroup layout = row.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 10, 10);
        layout.spacing = 4;

        Text(row.transform, "Contact", Fallback(thread.contact, "Contact"), 18, Ink, FontStyles.Bold);
        Text(row.transform, "Relationship", Fallback(thread.relationship, "relationship"), 17, Ink);
        Text(row.transform, "Progress", ThreadProgress(thread), 16, Muted);
    }

    private void AddTelegramCard(Transform parent, ComputerTelegramThread thread)
    {
        GameObject card = Card(parent, "TelegramCard");
        Layout(card, -1f, -1f, 1f, 1f);
        Text(card.transform, "Meta", $"{Fallback(thread.contact, "Contact")} / {Fallback(thread.relationship, "relationship")} / {ThreadProgress(thread)}", 16, BlueDark, FontStyles.Bold);
        AddThread(card.transform, thread.messages ?? new List<JToken>(), thread.contact);
        AddResult(card.transform, thread.correct);
        AddOptionButtons(card.transform, "telegram", thread.id, thread.options, ThreadResolved(thread));
        if (!ThreadResolved(thread))
        {
            AddCustomReply(card.transform, "telegram", thread.id, "Write your own message...");
        }
    }

    private void RenderBriefing(Transform parent)
    {
        GameObject shell = PanelObject(parent, "BriefingApp", Html("#f7f9fb"));
        Layout(shell, -1f, TabPanelHeight, 1f, 0f);
        VerticalLayoutGroup shellLayout = shell.AddComponent<VerticalLayoutGroup>();
        shellLayout.padding = new RectOffset(20, 20, 18, 20);
        shellLayout.spacing = 14;
        shellLayout.childControlWidth = true;
        shellLayout.childForceExpandWidth = true;
        shellLayout.childControlHeight = true;
        shellLayout.childForceExpandHeight = false;

        Text(shell.transform, "Heading", currentGame.complete
            ? "Shift complete"
            : "Shift active", 34, Ink, FontStyles.Bold);
        Text(shell.transform, "Subheading", currentGame.complete
            ? "Review your calls and replay with a new generated day."
            : "Finish every workspace to complete the day.", 20, Muted, FontStyles.Bold);

        GameObject systems = Element(parent: shell.transform, name: "BriefingSystems");
        Layout(systems, -1f, 330f, 1f, 0f);
        HorizontalLayoutGroup systemsLayout = systems.AddComponent<HorizontalLayoutGroup>();
        systemsLayout.spacing = 14;
        systemsLayout.childControlWidth = true;
        systemsLayout.childControlHeight = true;
        systemsLayout.childForceExpandWidth = true;
        systemsLayout.childForceExpandHeight = true;

        GameObject values = Card(systems.transform, "Values");
        Layout(values, -1f, -1f, 1f, 1f);
        Text(values.transform, "Title", "Values", 26, Ink, FontStyles.Bold);
        foreach (ComputerValue value in ValuesList())
        {
            Text(values.transform, "Value", $"{Fallback(value.label, "Value")}: {value.value}/100 - {Fallback(value.description, "No description")}", 18, Ink);
        }

        GameObject quests = Card(systems.transform, "Quests");
        Layout(quests, -1f, -1f, 1f, 1f);
        Text(quests.transform, "Title", "Quests", 26, Ink, FontStyles.Bold);
        foreach (ComputerQuest quest in currentGame.quests ?? new List<ComputerQuest>())
        {
            Text(quests.transform, "Quest", $"{Fallback(quest.title, "Quest")}: {quest.current}/{quest.target} {(quest.complete ? "complete" : "active")} - {Fallback(quest.reward, "reward pending")}", 18, Ink);
        }

        Text(shell.transform, "Rules", "1. You are responsible for what appears on the new-media front page.\n2. Real stories should be published only when the source and framing are credible.\n3. Manipulated stories often contain pressure, unsupported certainty, or emotional wording.\n4. Email and Telegram sidequests affect your trust score just like newsdesk calls.", 20, Ink);
        Text(shell.transform, "LogTitle", "Action log", 26, Ink, FontStyles.Bold);

        List<string> lines = currentGame.actionLog ?? new List<string>();
        if (lines.Count == 0)
        {
            Text(shell.transform, "EmptyLog", "No actions yet.", 18, Muted);
        }
        else
        {
            foreach (string line in lines)
            {
                Text(shell.transform, "ActionLog", line, 18, Muted);
            }
        }
    }

    private void AddOptionButtons(Transform parent, string surface, string itemId, List<ComputerOption> options, bool resolved)
    {
        if (options == null || options.Count == 0)
        {
            return;
        }

        GameObject row = Element(parent: parent, name: "Options");
        Layout(row, -1f, -1f, 1f, 0f);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;

        foreach (ComputerOption option in options)
        {
            if (option == null)
            {
                continue;
            }

            Button button = Button(row.transform, Fallback(option.label, option.id), Panel, BlueDark, () => SendActionClicked(surface, itemId, option.id), 270f, 54f);
            button.interactable = !resolved && !busy;
        }
    }

    private void AddCustomReply(Transform parent, string surface, string itemId, string placeholder)
    {
        GameObject box = PanelObject(parent, "CustomReply", Html("#f7f9fb"));
        Layout(box, -1f, 136f, 1f, 0f);
        HorizontalLayoutGroup layout = box.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 12;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;

        TMP_InputField input = InputField(box.transform, placeholder);
        Layout(input.gameObject, -1f, 112f, 1f, 0f);
        Button(box.transform, "Send reply", Blue, Color.white, () => SendCustomReplyClicked(surface, itemId, input), 168f, 56f);
    }

    private void AddThread(Transform parent, List<JToken> messages, string fallbackSender)
    {
        GameObject thread = PanelObject(parent, "Thread", Html("#f8fafc"));
        Layout(thread, -1f, -1f, 1f, 0f);
        VerticalLayoutGroup layout = thread.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(14, 14, 14, 14);
        layout.spacing = 10;

        if (messages == null || messages.Count == 0)
        {
            Text(thread.transform, "EmptyMessage", "No messages yet.", 18, Muted);
            return;
        }

        foreach (JToken message in messages)
        {
            bool player = MessageRole(message) == "player" || MessageSender(message) == "You";
            GameObject bubble = PanelObject(thread.transform, "Bubble", player ? Html("#dbeafe") : Color.white);
            Layout(bubble, -1f, -1f, 1f, 0f);
            VerticalLayoutGroup bubbleLayout = bubble.AddComponent<VerticalLayoutGroup>();
            bubbleLayout.padding = new RectOffset(14, 14, 12, 12);
            bubbleLayout.spacing = 4;
            string sender = Fallback(MessageSender(message), player ? "You" : fallbackSender);
            Text(bubble.transform, "Sender", sender, 16, player ? BlueDark : Muted, FontStyles.Bold);
            Text(bubble.transform, "Text", MessageText(message), 19, Ink);
        }
    }

    private void AddResult(Transform parent, bool? correct)
    {
        if (!correct.HasValue)
        {
            return;
        }

        TMP_Text result = Text(parent, "Result", correct.Value ? "Correct call" : "Risky call", 18, correct.Value ? Green : Red, FontStyles.Bold);
        result.alignment = TextAlignmentOptions.Left;
    }

    private void AddMeter(Transform parent, string label, int value, string description)
    {
        GameObject block = Element(parent: parent, name: "ValueMeter");
        Layout(block, -1f, 34f, 1f, 0f);
        VerticalLayoutGroup layout = block.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 2;

        Text(block.transform, "Label", $"{label}: {value}/100", 12, Ink, FontStyles.Bold);
        GameObject track = PanelObject(block.transform, "Track", Html("#e5ecf3"));
        Layout(track, -1f, 8f, 1f, 0f);
        GameObject fill = PanelObject(track.transform, "Fill", value >= 55 ? Green : Amber);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(value / 100f), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
    }

    private GameObject Card(Transform parent, string name)
    {
        GameObject card = PanelObject(parent, name, Panel);
        VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 16, 16);
        layout.spacing = 10;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        return card;
    }

    private static Button Button(Transform parent, string label, Color background, Color foreground, UnityAction onClick, float width, float height)
    {
        GameObject go = PanelObject(parent, $"Button-{label}", background);
        Layout(go, width, height, 0f, 0f);
        Button button = go.AddComponent<Button>();
        button.targetGraphic = go.GetComponent<Image>();
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = background;
        colors.highlightedColor = Color.Lerp(background, Color.white, 0.18f);
        colors.pressedColor = Color.Lerp(background, Color.black, 0.12f);
        colors.disabledColor = Html("#d3dbe4");
        colors.colorMultiplier = 1f;
        button.colors = colors;

        TMP_Text text = Text(go.transform, "Label", label, 16, foreground, FontStyles.Bold);
        text.alignment = TextAlignmentOptions.Center;
        Stretch(text.rectTransform);
        return button;
    }

    private static TMP_InputField InputField(Transform parent, string placeholder)
    {
        GameObject root = PanelObject(parent, "InputField", Color.white);
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        Image image = root.GetComponent<Image>();
        input.targetGraphic = image;
        RectTransform rootRect = root.GetComponent<RectTransform>();

        GameObject viewport = Element(parent: root.transform, name: "TextViewport");
        Stretch(viewport.GetComponent<RectTransform>(), 10f, 8f, 10f, 8f);
        RectMask2D mask = viewport.AddComponent<RectMask2D>();
        mask.padding = Vector4.zero;

        TMP_Text text = Text(viewport.transform, "Text", string.Empty, 18, Ink);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.TopLeft;

        TMP_Text placeholderText = Text(viewport.transform, "Placeholder", placeholder, 18, Muted);
        Stretch(placeholderText.rectTransform);
        placeholderText.alignment = TextAlignmentOptions.TopLeft;

        input.textViewport = viewport.GetComponent<RectTransform>();
        input.textComponent = text;
        input.placeholder = placeholderText;
        input.lineType = TMP_InputField.LineType.MultiLineNewline;
        input.characterLimit = 900;
        rootRect.sizeDelta = new Vector2(rootRect.sizeDelta.x, 92f);
        return input;
    }

    private static TMP_Text Text(Transform parent, string name, string value, int size, Color color, FontStyles style = FontStyles.Normal)
    {
        GameObject go = Element(parent: parent, name: name);
        TMP_Text text = go.AddComponent<TextMeshProUGUI>();
        text.text = DisplayText(value);
        text.fontSize = ReadableFontSize(size);
        text.color = color;
        text.fontStyle = style;
        text.richText = false;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        Layout(go, -1f, -1f, 1f, 0f);
        return text;
    }

    private static int ReadableFontSize(int size)
    {
        if (size <= 12)
        {
            return 16;
        }

        if (size <= 14)
        {
            return 18;
        }

        if (size <= 16)
        {
            return 20;
        }

        if (size <= 20)
        {
            return 24;
        }

        return Mathf.RoundToInt(size * 1.08f);
    }

    private static RectTransform CreateScroll(Transform parent, string name, out RectTransform content, bool horizontal)
    {
        GameObject root = Element(parent: parent, name: name);
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal = horizontal;
        scroll.vertical = !horizontal;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        Image rootImage = root.AddComponent<Image>();
        rootImage.color = new Color(1f, 1f, 1f, 0f);

        GameObject viewport = PanelObject(root.transform, "Viewport", new Color(1f, 1f, 1f, 0f));
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();

        GameObject contentObject = Element(parent: viewport.transform, name: "Content");
        content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = horizontal ? new Vector2(0f, 0f) : new Vector2(0f, 1f);
        content.anchorMax = horizontal ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        if (horizontal)
        {
            HorizontalLayoutGroup layout = contentObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        else
        {
            VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = content;
        return root.GetComponent<RectTransform>();
    }

    private static GameObject PanelObject(Transform parent, string name, Color color)
    {
        GameObject go = Element(parent: parent, name: name);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return go;
    }

    private static GameObject Element(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Layout(GameObject go, float preferredWidth, float preferredHeight, float flexibleWidth, float flexibleHeight)
    {
        LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (preferredWidth >= 0f)
        {
            element.preferredWidth = preferredWidth;
        }
        if (preferredHeight >= 0f)
        {
            element.preferredHeight = preferredHeight;
        }
        element.flexibleWidth = flexibleWidth;
        element.flexibleHeight = flexibleHeight;
    }

    private static void Stretch(RectTransform rect)
    {
        Stretch(rect, 0f, 0f, 0f, 0f);
    }

    private static void Stretch(RectTransform rect, float left, float top, float right, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private void SetBusy(bool value, string message)
    {
        busy = value;
        if (canvasGroup != null)
        {
            canvasGroup.interactable = !value;
        }
        SetStatus(message);
        RenderTopbar();
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = DisplayText(message);
        }

        if (bootBodyText != null && currentGame == null)
        {
            bootBodyText.text = DisplayText(message);
        }

        if (bootTitleText != null && currentGame == null)
        {
            bootTitleText.text = DisplayText(busy ? "Preparing runtime shift" : "Runtime shift");
        }
    }

    private static ComputerGameState NormalizeGame(ComputerGameState game)
    {
        if (game == null)
        {
            return null;
        }

        game.values = game.values ?? new Dictionary<string, ComputerValue>();
        game.quests = game.quests ?? new List<ComputerQuest>();
        game.questLog = game.questLog ?? new List<string>();
        game.generationLog = game.generationLog ?? new List<string>();
        game.worldFeed = game.worldFeed ?? new List<string>();
        game.goals = game.goals ?? new List<ComputerGoal>();
        game.newsItems = game.newsItems ?? new List<ComputerNewsItem>();
        game.emails = game.emails ?? new List<ComputerEmailItem>();
        game.telegramThreads = game.telegramThreads ?? new List<ComputerTelegramThread>();
        game.actionLog = game.actionLog ?? new List<string>();

        foreach (ComputerEmailItem email in game.emails)
        {
            if (email == null)
            {
                continue;
            }
            email.messages = email.messages ?? new List<JToken>();
            email.options = email.options ?? new List<ComputerOption>();
        }

        foreach (ComputerTelegramThread thread in game.telegramThreads)
        {
            if (thread == null)
            {
                continue;
            }
            thread.messages = thread.messages ?? new List<JToken>();
            thread.options = thread.options ?? new List<ComputerOption>();
        }

        return game;
    }

    private List<ComputerValue> ValuesList()
    {
        List<ComputerValue> values = new List<ComputerValue>();
        if (currentGame != null && currentGame.values != null)
        {
            foreach (KeyValuePair<string, ComputerValue> pair in currentGame.values)
            {
                if (pair.Value != null)
                {
                    values.Add(pair.Value);
                }
            }
        }
        return values;
    }

    private bool EmailExists(string id)
    {
        return FindEmail(id) != null;
    }

    private ComputerEmailItem FindEmail(string id)
    {
        if (currentGame == null || currentGame.emails == null)
        {
            return null;
        }

        foreach (ComputerEmailItem email in currentGame.emails)
        {
            if (email != null && email.id == id)
            {
                return email;
            }
        }
        return null;
    }

    private string FirstOpenEmailId()
    {
        if (currentGame == null || currentGame.emails == null || currentGame.emails.Count == 0)
        {
            return string.Empty;
        }

        foreach (ComputerEmailItem email in currentGame.emails)
        {
            if (email != null && !ThreadResolved(email))
            {
                return email.id;
            }
        }

        return currentGame.emails[0].id;
    }

    private bool TelegramExists(string id)
    {
        return FindTelegram(id) != null;
    }

    private ComputerTelegramThread FindTelegram(string id)
    {
        if (currentGame == null || currentGame.telegramThreads == null)
        {
            return null;
        }

        foreach (ComputerTelegramThread thread in currentGame.telegramThreads)
        {
            if (thread != null && thread.id == id)
            {
                return thread;
            }
        }
        return null;
    }

    private string FirstOpenTelegramId()
    {
        if (currentGame == null || currentGame.telegramThreads == null || currentGame.telegramThreads.Count == 0)
        {
            return string.Empty;
        }

        foreach (ComputerTelegramThread thread in currentGame.telegramThreads)
        {
            if (thread != null && !ThreadResolved(thread))
            {
                return thread.id;
            }
        }

        return currentGame.telegramThreads[0].id;
    }

    private static List<JToken> EmailMessages(ComputerEmailItem email)
    {
        if (email == null)
        {
            return new List<JToken>();
        }

        if (email.messages != null && email.messages.Count > 0)
        {
            return email.messages;
        }

        return new List<JToken> { new JValue(Fallback(email.body, "No message body.")) };
    }

    private static bool ThreadResolved(ComputerEmailItem email)
    {
        return email != null && (email.resolved || !string.IsNullOrWhiteSpace(email.selected));
    }

    private static bool ThreadResolved(ComputerTelegramThread thread)
    {
        return thread != null && (thread.resolved || !string.IsNullOrWhiteSpace(thread.selected));
    }

    private static string ThreadProgress(ComputerEmailItem item)
    {
        if (ThreadResolved(item))
        {
            return "Resolved";
        }
        int maxTurns = item != null ? Mathf.Max(item.maxTurns, item.minTurns, 3) : 3;
        int turns = item != null ? item.chatTurns : 0;
        return $"Thread {Mathf.Min(turns, maxTurns)}/{maxTurns}";
    }

    private static string ThreadProgress(ComputerTelegramThread item)
    {
        if (ThreadResolved(item))
        {
            return "Resolved";
        }
        int maxTurns = item != null ? Mathf.Max(item.maxTurns, item.minTurns, 3) : 3;
        int turns = item != null ? item.chatTurns : 0;
        return $"Thread {Mathf.Min(turns, maxTurns)}/{maxTurns}";
    }

    private static string MessageText(JToken message)
    {
        if (message == null)
        {
            return string.Empty;
        }

        if (message.Type == JTokenType.String)
        {
            return message.Value<string>();
        }

        JToken text = message["text"];
        return text != null ? text.Value<string>() : message.ToString(Formatting.None);
    }

    private static string MessageSender(JToken message)
    {
        if (message == null || message.Type != JTokenType.Object)
        {
            return string.Empty;
        }

        JToken sender = message["sender"];
        return sender != null ? sender.Value<string>() : string.Empty;
    }

    private static string MessageRole(JToken message)
    {
        if (message == null || message.Type != JTokenType.Object)
        {
            return string.Empty;
        }

        JToken role = message["role"];
        return role != null ? role.Value<string>() : string.Empty;
    }

    private static string LastMessagePreview(List<JToken> messages, string fallback)
    {
        if (messages != null && messages.Count > 0)
        {
            string text = MessageText(messages[messages.Count - 1]);
            return text.Length > 80 ? text.Substring(0, 80) + "..." : text;
        }

        return Fallback(fallback, "No preview.");
    }

    private static int OpenNewsCount(List<ComputerNewsItem> items)
    {
        int count = 0;
        foreach (ComputerNewsItem item in items)
        {
            if (item != null && string.IsNullOrWhiteSpace(item.decision))
            {
                count++;
            }
        }
        return count;
    }

    private static int OpenThreadCount<T>(List<T> items)
    {
        int count = 0;
        foreach (T item in items ?? new List<T>())
        {
            if (!ThreadResolved(item))
            {
                count++;
            }
        }
        return count;
    }

    private static string ThreadSummary<T>(List<T> items)
    {
        int total = items != null ? items.Count : 0;
        int open = OpenThreadCount(items);
        return $"{Mathf.Max(0, total - open)}/{total} resolved";
    }

    private static string LatestLine(params List<string>[] sources)
    {
        foreach (List<string> source in sources)
        {
            if (source == null)
            {
                continue;
            }

            for (int i = source.Count - 1; i >= 0; i--)
            {
                if (!string.IsNullOrWhiteSpace(source[i]))
                {
                    return source[i];
                }
            }
        }

        return "No workstation events yet.";
    }

    private static string NewsStatus(ComputerNewsItem item)
    {
        if (item == null)
        {
            return "Pending";
        }

        if (!string.IsNullOrWhiteSpace(item.decision))
        {
            return item.correct == true ? "Cleared" : "Flagged";
        }

        return item.truthLabel == "manipulated" ? "Needs checks" : "Ready check";
    }

    private static string SourceHost(string url, string fallback)
    {
        Uri uri;
        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out uri))
        {
            return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host.Substring(4) : uri.Host;
        }

        return Fallback(fallback, "source pending");
    }

    private static void AddRange(List<string> target, List<string> source, int limit, bool fromEnd = false)
    {
        if (target == null || source == null || source.Count == 0 || limit <= 0)
        {
            return;
        }

        if (fromEnd)
        {
            for (int i = Mathf.Max(0, source.Count - limit); i < source.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(source[i]) && !target.Contains(source[i]))
                {
                    target.Add(source[i]);
                }
            }
            return;
        }

        for (int i = 0; i < source.Count && i < limit; i++)
        {
            if (!string.IsNullOrWhiteSpace(source[i]) && !target.Contains(source[i]))
            {
                target.Add(source[i]);
            }
        }
    }

    private static string Fallback(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static string DisplayText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string normalized = value.Normalize(NormalizationForm.FormD);
        StringBuilder builder = new StringBuilder(normalized.Length);
        bool sawUnsupported = false;

        foreach (char ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (ch == '\r')
            {
                continue;
            }

            if (ch == '\n')
            {
                builder.Append('\n');
                continue;
            }

            string replacement = AsciiReplacement(ch);
            if (replacement != null)
            {
                builder.Append(replacement);
                continue;
            }

            if (ch >= 32 && ch <= 126)
            {
                builder.Append(ch);
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                builder.Append(' ');
                continue;
            }

            sawUnsupported = true;
            builder.Append(' ');
        }

        string compact = CompactSpaces(builder.ToString());
        if (compact.Length == 0 && sawUnsupported)
        {
            return "[source text uses unsupported characters]";
        }

        return compact;
    }

    private static string AsciiReplacement(char ch)
    {
        switch ((int)ch)
        {
            case 0x00A0:
                return " ";
            case 0x00A9:
                return "(C)";
            case 0x00AE:
                return "(R)";
            case 0x2018:
            case 0x2019:
            case 0x201A:
            case 0x201B:
                return "'";
            case 0x201C:
            case 0x201D:
            case 0x201E:
            case 0x201F:
                return "\"";
            case 0x2013:
            case 0x2014:
            case 0x2212:
                return "-";
            case 0x2022:
            case 0x00B7:
                return "-";
            case 0x2026:
                return "...";
            case 0x2122:
                return "TM";
            default:
                return null;
        }
    }

    private static string CompactSpaces(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string[] lines = value.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            StringBuilder line = new StringBuilder(lines[i].Length);
            bool previousSpace = false;
            foreach (char ch in lines[i])
            {
                if (ch == ' ')
                {
                    if (!previousSpace)
                    {
                        line.Append(ch);
                    }
                    previousSpace = true;
                    continue;
                }

                line.Append(ch);
                previousSpace = false;
            }

            lines[i] = line.ToString().Trim();
        }

        return string.Join("\n", lines).Trim();
    }

    private static Color Html(string value)
    {
        Color color;
        return ColorUtility.TryParseHtmlString(value, out color) ? color : Color.white;
    }
}
