using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.UI;

public sealed class ComputerOverlayController : MonoBehaviour
{
    // ─── Layout constants ────────────────────────────────────────────────────
    private const float CanvasReferenceWidth        = 1920f;
    private const float CanvasReferenceHeight       = 1080f;
    private const float TaskbarHeight               = 80f;
    private const float TitlebarHeight              = 60f;
    private const float SidebarWidth                = 208f;
    private const float StatusbarHeight             = 28f;
    private const float TopbarHeight                = 54f;
    private const float WindowInsetH               = 80f;   // left margin for desktop icons
    private const float WindowInsetTop             = 12f;
    private const float WindowInsetBottom          = TaskbarHeight + 8f;
    private const float DesktopIconWidth           = 140f;
    private const float DesktopIconHeight          = 140f;
    private const float DesktopIconSpacing         = 16f;
    private const float NewsCardHeight             = 192f;
    private const float NewsLeadCardHeight         = 340f;
    private const float EmailRowHeight             = 100f;
    private const float TelegramRowHeight          = 100f;
    private const float ThreadViewportHeight       = 320f;
    private const float ChatBodyHeight             = 800f;
    private const float TaskbarAppWidth            = 140f;
    private const float NotificationWidth          = 320f;
    private const float NotificationHeight         = 80f;

    // ─── Monitor / focus ─────────────────────────────────────────────────────
    private const float MonitorScreenWidthRatio         = 0.94f;
    private const float MonitorScreenVerticalOffsetRatio = 0.08f;
    private const float MonitorFallbackWorldWidth        = 4.8f;
    private const float MonitorSurfaceOffset             = 0.025f;
    private const float FocusDistance                   = 6.2f;
    private const float FocusHeightOffset               = 0.1f;
    private const float FocusFov                        = 34f;
    private const float FocusEnterTransitionDuration    = 1f;
    private const float FocusExitTransitionDuration     = 0.65f;

    // ─── Backend ─────────────────────────────────────────────────────────────
    private const string BackendUrlKey    = "DeepDetect.BackendUrl";
    private const string TokenKey         = "DeepDetect.UnityToken";
    private const string UserKey          = "DeepDetect.UnityUser";
    private const string DefaultBackendUrl = "http://76.13.159.31:8104";
    private const string DefaultName      = "Unity Player";
    private const string DefaultEmail     = "unity.player@deepdetectgame.dev";
    private const string DefaultPassword  = "unity-local-player-2026";

    private const string PrimaryMonitorName  = "monitor";
    private const string FallbackMonitorName = "Monitor_27__Curved";

    // ─── Windows-style color palette ────────────────────────────────────────
    // Desktop / Wallpaper tones
    private static readonly Color WallpaperDark  = Html("#0d1b2a");
    private static readonly Color WallpaperMid   = Html("#1a2d44");

    // Window chrome
    private static readonly Color WinBg          = Html("#1e2535");
    private static readonly Color WinTitlebar    = Html("#161f2e");
    private static readonly Color WinSidebar     = Html("#111827");
    private static readonly Color WinTopbar      = Html("#1a2234");
    private static readonly Color WinStatusbar   = Html("#0f1623");
    private static readonly Color WinContent     = Html("#141c29");

    // Taskbar
    private static readonly Color TaskbarBg      = Html("#0a0f1a");
    private static readonly Color TaskbarHover   = Html("#ffffff1e");  // semi-transparent
    private static readonly Color TaskbarActive  = Html("#ffffff2e");

    // Cards / panels
    private static readonly Color CardBg         = Html("#1a2234");
    private static readonly Color CardBorder      = Html("#ffffff12");

    // Light (white) card palette — for readable news cards
    private static readonly Color LightCardBg     = Html("#f8fafc");
    private static readonly Color LightCardShadow  = Html("#00000026");
    private static readonly Color LightText        = Html("#0b1220");
    private static readonly Color LightTextSub     = Html("#1f2937");
    private static readonly Color LightTextMuted   = Html("#475569");
    private static readonly Color CardHover       = Html("#1e2a3e");
    private static readonly Color PanelRaised    = Html("#242f45");

    // Text
    private static readonly Color TextPrimary    = Html("#e2e8f0");
    private static readonly Color TextSecondary  = Html("#94a3b8");
    private static readonly Color TextMuted      = Html("#7587a0");
    private static readonly Color TextDim        = Html("#374151");

    // Accent colors
    private static readonly Color AccentBlue     = Html("#3b82f6");
    private static readonly Color AccentBlueDim  = Html("#1d4ed8");
    private static readonly Color AccentBlueSoft = Html("#60a5fa");
    private static readonly Color AccentGreen    = Html("#059669");
    private static readonly Color AccentGreenSoft= Html("#34d399");
    private static readonly Color AccentRed      = Html("#dc2626");
    private static readonly Color AccentRedSoft  = Html("#f87171");
    private static readonly Color AccentAmber    = Html("#d97706");
    private static readonly Color AccentAmberSoft= Html("#fbbf24");
    private static readonly Color AccentPurple   = Html("#7c3aed");

    // Window control buttons
    private static readonly Color WinBtnHover    = Html("#ffffff1a");
    private static readonly Color WinBtnClose    = Html("#c42b1c");

    // Sidebar nav
    private static readonly Color NavActive      = Html("#1e3a5f");
    private static readonly Color NavHover       = Html("#ffffff0d");
    private static readonly Color NavDot         = Html("#3b82f6");
    private static readonly Color NavDotAmber    = Html("#f59e0b");

    // ─── Singleton ──────────────────────────────────────────────────────────
    private static ComputerOverlayController instance;

    // ─── State ──────────────────────────────────────────────────────────────
    private ComputerApiClient    api;
    private ComputerUser         user;
    private ComputerGameState    currentGame;
    private string activeTab         = "home";
    private string activeEmailId     = string.Empty;
    private string activeTelegramId  = string.Empty;
    private string activeArticleId   = string.Empty;
    private bool   initialized;
    private bool   initializing;
    private bool   busy;
    private bool   usingWorldMonitor;
    private bool   computerOpen;
    private bool   focusActive;
    private bool   focusTransitioning;
    private bool   windowMinimized;
    private Vector3    savedCameraPosition;
    private Quaternion savedCameraRotation;
    private float      savedCameraFov;
    private Coroutine  focusTransitionRoutine;
    private Coroutine  articlePollRoutine;
    private readonly Dictionary<string, Sprite> articleImageCache = new Dictionary<string, Sprite>();
    private readonly HashSet<string> articleImageLoading = new HashSet<string>();
    private readonly HashSet<string> articleImageFailed = new HashSet<string>();
    private Transform  focusAnchor;

    // ─── UI references ──────────────────────────────────────────────────────
    private GameObject  canvasObject;
    private Canvas      canvas;
    private CanvasScaler canvasScaler;
    private CanvasGroup canvasGroup;

    // Desktop layer
    private GameObject  desktopLayer;
    private GameObject  windowLayer;
    private GameObject  taskbarObject;
    private TMP_Text    taskbarTimeText;
    private TMP_Text    taskbarDateText;
    private Transform   taskbarApps;

    // Window
    private GameObject  mainWindow;
    private TMP_Text    windowTitleText;
    private Transform   sidebarNav;
    private TMP_Text    sidebarScoreText;
    private TMP_Text    sidebarTickText;
    private TMP_Text    topbarTitleText;
    private Transform   topbarBadges;
    private Transform   topbarActions;
    private RectTransform contentArea;
    private TMP_Text    statusbarText;
    private TMP_Text    statusbarAgentText;
    private TMP_Text    statusbarRightText;

    // Boot
    private GameObject  bootStateObject;
    private TMP_Text    bootTitleText;
    private TMP_Text    bootBodyText;
    private Button      bootRetryButton;
    private string      lastStatusMessage = "Connecting to DeepDetect backend...";

    // Notification toast
    private GameObject  notificationToast;
    private TMP_Text    notifTitleText;
    private TMP_Text    notifBodyText;
    private Coroutine   notifRoutine;

    // ─── Public API ─────────────────────────────────────────────────────────
    public static event Action ReturnToApartmentRequested;
    public static bool IsTransitioning => instance != null && instance.focusTransitioning;

    public static void OpenComputer()              => EnsureInstance().Open(null);
    public static void OpenComputer(Transform a)   => EnsureInstance().Open(a);
    public static void CloseComputer()             { if (instance != null) instance.Close(); }
    public static void PreloadComputer()           => EnsureInstance().Preload(null);
    public static void PreloadComputer(Transform a)=> EnsureInstance().Preload(a);

    private static ComputerOverlayController EnsureInstance()
    {
        if (instance != null) return instance;
        GameObject host = new GameObject("ComputerOverlayRuntime");
        DontDestroyOnLoad(host);
        instance = host.AddComponent<ComputerOverlayController>();
        return instance;
    }

    public static void ResetComputerState()
    {
        if (instance != null)
        {
            Destroy(instance.gameObject);
            instance = null;
        }
    }

    private void OnDestroy()
    {
        ExitFocusModeImmediate();
        CancelFakeBrowserSequence();
        if (instance == this) instance = null;
        if (canvasObject != null) { Destroy(canvasObject); canvasObject = null; }
    }

    // ─── Open / Close / Preload ─────────────────────────────────────────────
    private void Open(Transform anchor)
    {
        Debug.Log("[ComputerOverlay] Open requested.");
        SetFocusAnchor(anchor);
        computerOpen = true;
        if (canvasObject == null) BuildUi();
        AttachCanvasToComputerSurface();
        canvasObject.SetActive(true);
        RefreshCanvasInteractivity();
        EnterFocusMode();
        Cursor.visible    = true;
        Cursor.lockState  = CursorLockMode.None;
        if (!initialized && !initializing) _ = InitializeAsync();
        else { UpdateArticlePolling(); RenderAll(); }
    }

    private void Preload(Transform anchor)
    {
        SetFocusAnchor(anchor);
        if (canvasObject == null) BuildUi();
        AttachCanvasToComputerSurface();
        canvasObject.SetActive(usingWorldMonitor);
        RefreshCanvasInteractivity();
        if (!initialized && !initializing) _ = InitializeAsync();
        else RenderAll();
    }

    private void Close()
    {
        CancelFakeBrowserSequence();
        StopArticlePolling();
        computerOpen = false;
        RefreshCanvasInteractivity();
        if (canvasObject == null) { ExitFocusModeImmediate(); return; }
        if (focusActive && usingWorldMonitor) ExitFocusMode();
        else
        {
            ExitFocusModeImmediate();
            canvasObject.SetActive(usingWorldMonitor);
            RefreshCanvasInteractivity();
        }
    }

    // ─── Initialization ─────────────────────────────────────────────────────
    private async Task InitializeAsync()
    {
        initializing = true;
        SetBusy(true, "Starting system network services...");
        string backendUrl  = PlayerPrefs.GetString(BackendUrlKey, DefaultBackendUrl);
        if (string.IsNullOrEmpty(backendUrl) || 
            backendUrl == "http://127.0.0.1:8765" || 
            backendUrl == "http://localhost:8765" || 
            backendUrl.Contains("127.0.0.1") || 
            backendUrl.Contains("localhost"))
        {
            backendUrl = DefaultBackendUrl;
            PlayerPrefs.SetString(BackendUrlKey, backendUrl);
        }
        string savedToken  = PlayerPrefs.GetString(TokenKey, string.Empty);
        api = new ComputerApiClient(backendUrl, savedToken);
        try
        {
            bool healthy = await api.HealthAsync();
            if (!healthy)
            {
                initialized = false; user = null; currentGame = null;
                SetBusy(false, $"Backend offline at {backendUrl}");
                RenderAll(); return;
            }
            await EnsureAuthenticatedAsync();
            initialized = true;
            await EnsureRuntimeGameAsync();
            SetBusy(false, "Ready");
            RenderAll();
        }
        catch (Exception ex)
        {
            initialized = user != null;
            SetBusy(false, ex.Message);
            RenderAll();
        }
        finally { initializing = false; }
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
            catch (ComputerApiException ex) { if (ex.StatusCode != 401) throw; }
        }
        ComputerAuthResponse auth = null;
        try { auth = await api.RegisterAsync(DefaultName, DefaultEmail, DefaultPassword); }
        catch (ComputerApiException ex)
        {
            if (ex.StatusCode != 409) throw;
            auth = await api.LoginAsync(DefaultEmail, DefaultPassword);
        }
        api.Token = auth.token;
        user      = auth.user;
        PlayerPrefs.SetString(TokenKey, auth.token);
        PlayerPrefs.SetString(UserKey, JsonConvert.SerializeObject(auth.user));
    }

    private async Task EnsureRuntimeGameAsync()
    {
        if (currentGame != null) return;
        ComputerGameResponse response = await api.GenerateGameAsync();
        SetCurrentGame(response.game, false);
    }

    // ─── Button handlers ────────────────────────────────────────────────────
    private async void RefreshClicked()
    {
        if (busy) return;
        if (!initialized) { await InitializeAsync(); return; }
        await RunRequestAsync("Refreshing...", async () =>
        {
            if (currentGame == null) { await EnsureRuntimeGameAsync(); return; }
            try
            {
                ComputerGameResponse r = await api.GetGameAsync(currentGame.id);
                SetCurrentGame(r.game, false);
            }
            catch (ComputerApiException ex)
            {
                if (ex.StatusCode != 404) throw;
                currentGame = null;
                await EnsureRuntimeGameAsync();
            }
        });
    }

    private async void AdvanceWorldClicked()
    {
        if (!CanUseGame()) return;
        await RunRequestAsync("Simulating world...", async () =>
        {
            ComputerGameResponse r = await api.TickAsync(currentGame.id);
            SetCurrentGame(r.game, true);
        });
    }

    private async void SendActionClicked(string surface, string itemId, string choice)
    {
        if (!CanUseGame()) return;
        await RunRequestAsync("Sending decision...", async () =>
        {
            ComputerGameResponse r = await api.SendActionAsync(currentGame.id, surface, itemId, choice);
            SetCurrentGame(r.game, true);
        });
    }

    private async void SendCustomReplyClicked(string surface, string itemId, TMP_InputField input)
    {
        if (!CanUseGame() || input == null) return;
        string text = (input.text ?? string.Empty).Trim();
        if (text.Length == 0) { SetStatus("Write a reply before sending."); return; }
        await RunRequestAsync("Sending reply...", async () =>
        {
            ComputerGameResponse r = await api.SendActionAsync(currentGame.id, surface, itemId, "__custom__", text);
            SetCurrentGame(r.game, true);
        });
    }

    private async Task RunRequestAsync(string busyMsg, Func<Task> action)
    {
        if (busy) return;
        SetBusy(true, busyMsg);
        try
        {
            await action();
            if (statusbarRightText != null && statusbarRightText.text == busyMsg)
                SetStatus("Ready");
        }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally { SetBusy(false, statusbarRightText != null ? statusbarRightText.text : "Ready"); RenderAll(); }
    }

    private void BackToApartmentClicked()
    {
        if (ReturnToApartmentRequested != null) { ReturnToApartmentRequested.Invoke(); return; }
        Close();
    }

    private bool CanUseBackend()
    {
        if (busy) return false;
        if (!initialized || api == null || user == null) { SetStatus("Backend offline. Click Refresh."); return false; }
        return true;
    }

    private bool CanUseGame()
    {
        if (!CanUseBackend()) return false;
        if (currentGame == null || string.IsNullOrWhiteSpace(currentGame.id)) { SetStatus("No active shift. Click Refresh."); return false; }
        return true;
    }

    // ─── Game state ─────────────────────────────────────────────────────────
    private void SetCurrentGame(ComputerGameState next, bool evalParanoia)
    {
        ComputerGameState prev = currentGame;
        currentGame = NormalizeGame(next);
        if (currentGame == null) return;
        if (string.IsNullOrWhiteSpace(activeEmailId) || !EmailExists(activeEmailId))
            activeEmailId = FirstOpenEmailId();
        if (string.IsNullOrWhiteSpace(activeTelegramId) || !TelegramExists(activeTelegramId))
            activeTelegramId = FirstOpenTelegramId();
        if (!string.IsNullOrWhiteSpace(activeArticleId) && FindNews(activeArticleId) == null)
            activeArticleId = string.Empty;
        if (GlobalCanvasPersistent.Instance != null)
            GlobalCanvasPersistent.Instance.SetPoints(Mathf.Max(0, currentGame.score));
        if (evalParanoia) ApplyParanoiaDelta(prev, currentGame);
        UpdateArticlePolling();
    }

    private void ApplyParanoiaDelta(ComputerGameState prev, ComputerGameState next)
{
    if (prev == null || next == null || GlobalCanvasPersistent.Instance == null) return;

    // ── WRONG decisions → add paranoia ──────────────────────────────────
    int delta = 0;
    int newWrong = 0;
    Dictionary<string, ComputerNewsItem> oldNews = new Dictionary<string, ComputerNewsItem>();
    foreach (ComputerNewsItem item in prev.newsItems ?? new List<ComputerNewsItem>())
        if (!string.IsNullOrWhiteSpace(item.id)) oldNews[item.id] = item;
    foreach (ComputerNewsItem item in next.newsItems ?? new List<ComputerNewsItem>())
    {
        if (item == null || string.IsNullOrWhiteSpace(item.id) || item.correct != false || string.IsNullOrWhiteSpace(item.decision)) continue;
        ComputerNewsItem old;
        bool wasResolved = oldNews.TryGetValue(item.id, out old) && !string.IsNullOrWhiteSpace(old.decision);
        if (!wasResolved)
        {
            delta += 10;
            newWrong++;
            GlobalCanvasPersistent.Instance.SubtractTime(30);
        }
    }
    int wrongEmails = CountNewWrongThreadResolutions(prev.emails, next.emails);
    int wrongTelegram = CountNewWrongThreadResolutions(prev.telegramThreads, next.telegramThreads);
    newWrong += wrongEmails + wrongTelegram;
    delta += wrongEmails * 6;
    delta += wrongTelegram * 6;
    if (wrongEmails > 0)
    {
        GlobalCanvasPersistent.Instance.SubtractTime(30 * wrongEmails);
    }
    if (wrongTelegram > 0)
    {
        GlobalCanvasPersistent.Instance.SubtractTime(30 * wrongTelegram);
    }
    if (delta > 0) GlobalCanvasPersistent.Instance.AddParanoia(delta);

    // Horror event: after every couple of wrong calls, the work window is
    // overrun by virus pop-ups the player must close before continuing.
    if (newWrong > 0) RegisterWrongDecisions(newWrong);

    // ── CORRECT decisions → add points, reduce paranoia, advance missions ─
    
    // 1. News
    foreach (ComputerNewsItem item in next.newsItems ?? new List<ComputerNewsItem>())
    {
        if (item == null || string.IsNullOrWhiteSpace(item.id) || string.IsNullOrWhiteSpace(item.decision)) continue;
        ComputerNewsItem old;
        bool wasResolved = oldNews.TryGetValue(item.id, out old) && !string.IsNullOrWhiteSpace(old.decision);
        if (!wasResolved && item.correct == true)
        {
            GlobalCanvasPersistent.Instance.AddPoints(50);
            GlobalCanvasPersistent.Instance.SubtractParanoia(5);

            // Mission progress for correctly publishing a genuine story.
            if (item.decision == "publish" && item.truthLabel != "manipulated" && MissionSidebarManager.Instance != null)
            {
                MissionSidebarManager.Instance.AddProgress(0);
            }
        }
    }

    // 2. Email threads
    Dictionary<string, ComputerEmailItem> oldEmails = new Dictionary<string, ComputerEmailItem>();
    foreach (ComputerEmailItem item in prev.emails ?? new List<ComputerEmailItem>())
        if (!string.IsNullOrWhiteSpace(item.id)) oldEmails[item.id] = item;
    foreach (ComputerEmailItem item in next.emails ?? new List<ComputerEmailItem>())
    {
        if (item == null || string.IsNullOrWhiteSpace(item.id) || !ThreadResolved(item)) continue;
        ComputerEmailItem old;
        bool wasResolved = oldEmails.TryGetValue(item.id, out old) && ThreadResolved(old);
        if (!wasResolved && item.correct == true)
        {
            GlobalCanvasPersistent.Instance.AddPoints(50);
            GlobalCanvasPersistent.Instance.SubtractParanoia(5);
        }
    }

    // 3. Telegram threads
    Dictionary<string, ComputerTelegramThread> oldTelegrams = new Dictionary<string, ComputerTelegramThread>();
    foreach (ComputerTelegramThread item in prev.telegramThreads ?? new List<ComputerTelegramThread>())
        if (!string.IsNullOrWhiteSpace(item.id)) oldTelegrams[item.id] = item;
    foreach (ComputerTelegramThread item in next.telegramThreads ?? new List<ComputerTelegramThread>())
    {
        if (item == null || string.IsNullOrWhiteSpace(item.id) || !ThreadResolved(item)) continue;
        ComputerTelegramThread old;
        bool wasResolved = oldTelegrams.TryGetValue(item.id, out old) && ThreadResolved(old);
        if (!wasResolved && item.correct == true)
        {
            GlobalCanvasPersistent.Instance.AddPoints(50);
            GlobalCanvasPersistent.Instance.SubtractParanoia(5);
        }
    }
}

    private static int CountNewWrongThreadResolutions<T>(List<T> previous, List<T> next)
    {
        Dictionary<string, bool> oldResolved = new Dictionary<string, bool>();
        foreach (T item in previous ?? new List<T>())
        {
            string id = ThreadId(item);
            if (!string.IsNullOrWhiteSpace(id)) oldResolved[id] = ThreadResolved(item);
        }
        int count = 0;
        foreach (T item in next ?? new List<T>())
        {
            string id = ThreadId(item);
            if (string.IsNullOrWhiteSpace(id) || ThreadCorrect(item) != false || !ThreadResolved(item)) continue;
            bool was; if (!oldResolved.TryGetValue(id, out was) || !was) count++;
        }
        return count;
    }

    private static string ThreadId<T>(T item)
    {
        ComputerEmailItem    e = item as ComputerEmailItem;    if (e != null) return e.id;
        ComputerTelegramThread t = item as ComputerTelegramThread; return t != null ? t.id : string.Empty;
    }
    private static bool ThreadResolved<T>(T item)
    {
        ComputerEmailItem    e = item as ComputerEmailItem;    if (e != null) return e.resolved || !string.IsNullOrWhiteSpace(e.selected);
        ComputerTelegramThread t = item as ComputerTelegramThread; return t != null && (t.resolved || !string.IsNullOrWhiteSpace(t.selected));
    }
    private static bool? ThreadCorrect<T>(T item)
    {
        ComputerEmailItem    e = item as ComputerEmailItem;    if (e != null) return e.correct;
        ComputerTelegramThread t = item as ComputerTelegramThread; return t != null ? t.correct : (bool?)null;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UI BUILD
    // ════════════════════════════════════════════════════════════════════════
    private void BuildUi()
    {
        // ── Root canvas ──────────────────────────────────────────────────────
        canvasObject = new GameObject("ComputerCanvas", typeof(RectTransform));
        canvasObject.transform.SetParent(null, false);
        DontDestroyOnLoad(canvasObject);

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasReferenceWidth, CanvasReferenceHeight);
        canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
        canvasRect.pivot     = new Vector2(0.5f, 0.5f);

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode     = RenderMode.WorldSpace;
        canvas.worldCamera    = Camera.main;
        canvas.overrideSorting = true;
        canvas.sortingOrder   = 6000;

        canvasScaler = canvasObject.AddComponent<CanvasScaler>();
        canvasScaler.uiScaleMode         = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasScaler.referencePixelsPerUnit = 100f;
        canvasScaler.dynamicPixelsPerUnit   = 3f;

        canvasObject.AddComponent<GraphicRaycaster>();
        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha          = 1f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable   = false;

        // ── Taskbar (Windows-style bottom bar) ───────────────────────────────
        BuildTaskbar(canvasObject.transform);

        // ── Wallpaper ────────────────────────────────────────────────────────
        GameObject wallpaper = PanelObject(canvasObject.transform, "Wallpaper", Color.white);
        Image wallImg = wallpaper.GetComponent<Image>();
        Sprite wallSprite = Resources.Load<Sprite>("UI/desktop/Desktop-background");
        if (wallSprite != null) wallImg.sprite = wallSprite;
        else wallImg.color = WallpaperDark;
        Stretch(wallpaper.GetComponent<RectTransform>());
        wallpaper.transform.SetAsFirstSibling();

        // ── Desktop layer (icons) ────────────────────────────────────────────
        desktopLayer = Element(canvasObject.transform, "DesktopLayer");
        Stretch(desktopLayer.GetComponent<RectTransform>(), 0, 12f, 0, TaskbarHeight); 
        BuildDesktopIcons(desktopLayer.transform);

        // ── Window layer ─────────────────────────────────────────────────────
        // Balanced margins: shift the frame left and add a right margin so the
        // blue wallpaper is visible on both sides, and lift it off the taskbar so
        // the wallpaper shows underneath too.
        windowLayer = Element(canvasObject.transform, "WindowLayer");
        Stretch(windowLayer.GetComponent<RectTransform>(), 44f, 16f, 44f, TaskbarHeight + 34f);
        BuildMainWindow(windowLayer.transform);

        // ── Boot overlay ─────────────────────────────────────────────────────
        BuildBootState(canvasObject.transform);

        // ── Notification toast ───────────────────────────────────────────────
        BuildNotificationToast(canvasObject.transform);

        canvasObject.SetActive(false);
        Debug.Log("[ComputerOverlay] Windows-style UI built.");
    }

    // ── Desktop icons ───────────────────────────────────────────────────────
    private void BuildDesktopIcons(Transform parent)
    {
        string[] tabs   = { "recycle",     "home",     "email",    "telegram", "briefing" };
        string[] labels = { "Recycle Bin", "Newsdesk", "Inbox",    "Telegram", "Briefing" };
        Color[]  colors = { TextMuted,   AccentBlue, AccentGreen, AccentBlueDim, AccentAmber };

        GameObject col = Element(parent, "IconColumn");
        RectTransform colRect = col.GetComponent<RectTransform>();
        colRect.anchorMin = new Vector2(0f, 1f);
        colRect.anchorMax = new Vector2(0f, 1f);
        colRect.pivot     = new Vector2(0f, 1f);
        colRect.anchoredPosition = new Vector2(30f, -30f); // clean padding from screen edges
        colRect.sizeDelta        = new Vector2(DesktopIconWidth, tabs.Length * (DesktopIconHeight + DesktopIconSpacing));

        VerticalLayoutGroup layout = col.AddComponent<VerticalLayoutGroup>();
        layout.spacing = DesktopIconSpacing;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true; // force uGUI to perfectly size RectTransform heights!
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < tabs.Length; i++)
        {
            string capturedTab = tabs[i];
            Color  capturedColor = colors[i];
            GameObject icon = BuildDesktopIcon(col.transform, labels[i], capturedTab, capturedColor);
            Button btn = icon.GetComponent<Button>() ?? icon.AddComponent<Button>();
            btn.targetGraphic = icon.GetComponent<Image>();
            btn.onClick.AddListener(() => { 
                if (capturedTab != "recycle") {
                    windowMinimized = false;
                    activeTab = capturedTab; RenderTabs(); UpdateTaskbarApps(); UpdateSidebarNav(); 
                    RenderAll();
                }
            });
        }
    }

    private GameObject BuildDesktopIcon(Transform parent, string label, string tab, Color fallbackColor)
    {
        GameObject go = PanelObject(parent, $"Icon-{label}", Color.clear);
        Layout(go, DesktopIconWidth, DesktopIconHeight, 0f, 0f);
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = Color.clear;
        cb.highlightedColor = Html("#ffffff14");
        cb.pressedColor     = Html("#ffffff24");
        btn.colors = cb;

        VerticalLayoutGroup vl = go.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(6, 6, 12, 6);
        vl.spacing = 8f;
        vl.childAlignment      = TextAnchor.UpperCenter; // align from top of cell so icons are perfectly leveled
        vl.childControlWidth   = true;
        vl.childForceExpandWidth = false;
        vl.childControlHeight  = true;
        vl.childForceExpandHeight = false;

        // Use the clean square app sprites (withoutText) for a completely uniform, elegant grid!
        Sprite iconSprite = GetDesktopIconSprite(tab);
        GameObject iconBox = PanelObject(go.transform, "IconBox", Color.clear);
        Layout(iconBox, 92f, 92f, 0f, 0f); // larger icon box

        Image img = iconBox.GetComponent<Image>();
        if (iconSprite != null)
        {
            img.sprite = iconSprite;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }
        else
        {
            img.color = fallbackColor;
            img.type = Image.Type.Sliced;
            TMP_Text iconLabel = WinText(iconBox.transform, "Icon", tab.Substring(0, Mathf.Min(tab.Length, 4)).ToUpper(), 11, Color.white, FontStyles.Bold);
            iconLabel.alignment = TextAlignmentOptions.Center;
            Stretch(iconLabel.rectTransform);
        }

        // Render clean, sharp text captions underneath the icon
        TMP_Text caption = WinText(go.transform, "Caption", label, 12, Color.white, FontStyles.Normal);
        caption.alignment = TextAlignmentOptions.Center;
        caption.textWrappingMode = TextWrappingModes.Normal;
        caption.overflowMode = TextOverflowModes.Ellipsis;
        caption.raycastTarget = false;
        Layout(caption.gameObject, DesktopIconWidth - 12f, 24f, 0f, 0f);

        return go;
    }

    private Sprite GetDesktopIconSprite(string tab)
    {
        string spriteName;
        switch (tab)
        {
            case "recycle":  spriteName = "recycle-bin"; break;
            case "home":     spriteName = "newsdesk-withoutText"; break;
            case "email":    spriteName = "inbox-withoutText";    break;
            case "telegram": spriteName = "telegram-withoutText"; break;
            case "briefing": spriteName = "briefing-withoutText"; break;
            default: return null;
        }
        return Resources.Load<Sprite>("UI/desktop/" + spriteName);
    }

    // ── Main window ─────────────────────────────────────────────────────────
    private void BuildMainWindow(Transform parent)
    {
        // Use the Figma frame image as the whole window chrome (titlebar +
        // sidebar + content are painted into it). Inner panels stay transparent.
        mainWindow = PanelObject(parent, "MainWindow", WinBg);
        Stretch(mainWindow.GetComponent<RectTransform>());
        Sprite frameSprite = Resources.Load<Sprite>("UI/desktop/frame");
        Image mainImg = mainWindow.GetComponent<Image>();
        if (frameSprite != null)
        {
            mainImg.sprite = frameSprite;
            mainImg.type   = Image.Type.Simple;
            mainImg.color  = Color.white;

            // The frame sprite is rounded on all four corners, but the opaque
            // Sidebar / RightSide child panels have square corners and reach the
            // very bottom edge, painting over the frame's rounded bottom corners
            // (so only the top looked rounded). Clip every child to the frame's
            // alpha shape with a Mask so the bottom corners are rounded as well.
            Mask mask = mainWindow.GetComponent<Mask>();
            if (mask == null) mask = mainWindow.AddComponent<Mask>();
            mask.showMaskGraphic = true;
        }

        VerticalLayoutGroup vl = mainWindow.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 0;
        vl.childControlWidth  = vl.childForceExpandWidth  = true;
        vl.childControlHeight = true;
        vl.childForceExpandHeight = false;

        BuildWindowTitlebar(mainWindow.transform);

        // Body row (sidebar + right side)
        GameObject body = Element(mainWindow.transform, "WindowBody");
        Layout(body, -1f, -1f, 1f, 1f);
        HorizontalLayoutGroup hl = body.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 0;
        hl.childControlWidth  = true;
        hl.childForceExpandWidth  = false;
        hl.childControlHeight = true;
        hl.childForceExpandHeight = true;

        BuildSidebar(body.transform);
        BuildRightSide(body.transform);
    }

    // ── Window titlebar ─────────────────────────────────────────────────────
    private void BuildWindowTitlebar(Transform parent)
    {
        GameObject tb = PanelObject(parent, "Titlebar", Color.clear);
        Layout(tb, -1f, TitlebarHeight, 1f, 0f);
        HorizontalLayoutGroup hl = tb.AddComponent<HorizontalLayoutGroup>();
        hl.padding  = new RectOffset(22, 6, 0, 0);
        hl.spacing  = 8;
        hl.childAlignment      = TextAnchor.MiddleLeft;
        hl.childControlWidth   = true;
        hl.childForceExpandWidth = false;
        hl.childControlHeight  = true;
        hl.childForceExpandHeight = true;

        // Window title — left aligned, vertically centred, larger.
        windowTitleText = WinText(tb.transform, "Title", "DeepDetect", 17, TextPrimary, FontStyles.Bold);
        windowTitleText.alignment = TextAlignmentOptions.Left;
        Layout(windowTitleText.gameObject, -1f, -1f, 1f, 1f);

        // Window control buttons (drawn as shapes, not glyphs).
        // Both minimize and close just hide the window/tabs and keep the player on
        // the computer. Leaving the PC is done with the ESC key (see exit hint).
        WinControlButton(tb.transform, "min",   WinBtnHover, TextSecondary, () => { windowMinimized = true; RenderAll(); }, 46f);
        WinControlButton(tb.transform, "max",   WinBtnHover, TextSecondary, () => { }, 46f);
        WinControlButton(tb.transform, "close", WinBtnClose, Color.white,   () => { windowMinimized = true; RenderAll(); }, 46f);
    }

    private void WinControlButton(Transform parent, string kind, Color hoverColor, Color fg, UnityAction onClick, float width)
    {
        GameObject go = PanelObject(parent, $"WinBtn-{kind}", Color.clear);
        Layout(go, width, TitlebarHeight, 0f, 1f);
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = Color.clear;
        cb.highlightedColor = hoverColor;
        cb.pressedColor     = Color.Lerp(hoverColor, Color.black, 0.2f);
        btn.colors = cb;
        btn.onClick.AddListener(onClick);

        // Glyph container centered in the button
        GameObject glyph = Element(go.transform, "Glyph");
        LayoutElement le = glyph.AddComponent<LayoutElement>(); le.ignoreLayout = true;
        RectTransform gr = glyph.GetComponent<RectTransform>();
        gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
        gr.pivot = new Vector2(0.5f, 0.5f);
        gr.sizeDelta = new Vector2(12f, 12f);
        gr.anchoredPosition = Vector2.zero;

        switch (kind)
        {
            case "min":
                MakeBar(glyph.transform, new Vector2(11f, 1.6f), Vector2.zero, 0f, fg);
                break;
            case "max":
                MakeBar(glyph.transform, new Vector2(11f, 1.5f), new Vector2(0f,  5f), 0f, fg); // top
                MakeBar(glyph.transform, new Vector2(11f, 1.5f), new Vector2(0f, -5f), 0f, fg); // bottom
                MakeBar(glyph.transform, new Vector2(1.5f, 11f), new Vector2(-5f, 0f), 0f, fg); // left
                MakeBar(glyph.transform, new Vector2(1.5f, 11f), new Vector2( 5f, 0f), 0f, fg); // right
                break;
            case "close":
                MakeBar(glyph.transform, new Vector2(15f, 1.7f), Vector2.zero,  45f, fg);
                MakeBar(glyph.transform, new Vector2(15f, 1.7f), Vector2.zero, -45f, fg);
                break;
        }
    }

    private static void MakeBar(Transform parent, Vector2 size, Vector2 pos, float rotZ, Color color)
    {
        GameObject bar = PanelObject(parent, "Bar", color);
        bar.GetComponent<Image>().raycastTarget = false;
        RectTransform r = bar.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = size;
        r.anchoredPosition = pos;
        if (Mathf.Abs(rotZ) > 0.01f) r.localRotation = Quaternion.Euler(0f, 0f, rotZ);
    }

    // ── Sidebar ─────────────────────────────────────────────────────────────
    private void BuildSidebar(Transform parent)
    {
        GameObject sidebar = PanelObject(parent, "Sidebar", Html("#131d31"));
        Layout(sidebar, SidebarWidth, -1f, 0f, 1f);

        VerticalLayoutGroup vl = sidebar.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(0, 0, 14, 0);
        vl.spacing = 0;
        vl.childControlWidth  = vl.childForceExpandWidth  = true;
        vl.childControlHeight = true;
        vl.childForceExpandHeight = false;

        // Nav section label
        TMP_Text sectionLabel = WinText(sidebar.transform, "SectionLabel", "WORKSPACES", 11, AccentBlueSoft, FontStyles.Bold);
        Layout(sectionLabel.gameObject, -1f, 28f, 1f, 0f);
        sectionLabel.margin = new Vector4(16, 6, 12, 6);
        sectionLabel.characterSpacing = 3f;

        // Nav items container
        GameObject navContainer = Element(sidebar.transform, "NavItems");
        sidebarNav = navContainer.transform;
        Layout(navContainer, -1f, -1f, 1f, 0f);
        VerticalLayoutGroup navLayout = navContainer.AddComponent<VerticalLayoutGroup>();
        navLayout.padding = new RectOffset(10, 10, 4, 0);
        navLayout.spacing = 6;
        navLayout.childControlWidth  = navLayout.childForceExpandWidth  = true;
        navLayout.childControlHeight = true;
        navLayout.childForceExpandHeight = false;

        // Spacer
        GameObject spacer = Element(sidebar.transform, "Spacer");
        Layout(spacer, -1f, -1f, 1f, 1f);

        // Score panel at bottom
        BuildSidebarScorePanel(sidebar.transform);
    }

    private void BuildSidebarScorePanel(Transform parent)
    {
        GameObject panel = PanelObject(parent, "ScorePanel", Color.clear);
        Layout(panel, -1f, 96f, 1f, 0f);

        VerticalLayoutGroup vl = panel.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(20, 14, 14, 14);
        vl.spacing = 2;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandHeight = false;

        TMP_Text scoreLabel = WinText(panel.transform, "ScoreLabel", "SCORE", 11, TextSecondary, FontStyles.Bold);
        scoreLabel.characterSpacing = 4f;
        sidebarScoreText = WinText(panel.transform, "Score", "0", 30, AccentBlueSoft, FontStyles.Bold);
        sidebarTickText  = WinText(panel.transform, "Tick", "Tick 0 / loading...", 12, TextSecondary);
    }

    // ── Right side (topbar + content + statusbar) ───────────────────────────
    private void BuildRightSide(Transform parent)
    {
        GameObject right = PanelObject(parent, "RightSide", Html("#1a2234"));
        Layout(right, -1f, -1f, 1f, 1f);
        VerticalLayoutGroup vl = right.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 0;
        vl.childControlWidth  = vl.childForceExpandWidth  = true;
        vl.childControlHeight = true;
        vl.childForceExpandHeight = false;

        BuildWindowTopbar(right.transform);
        BuildContentArea(right.transform);
        BuildStatusbar(right.transform);
    }

    private void BuildWindowTopbar(Transform parent)
    {
        GameObject topbar = PanelObject(parent, "WindowTopbar", Color.clear);
        Layout(topbar, -1f, TopbarHeight, 1f, 0f);

        HorizontalLayoutGroup hl = topbar.AddComponent<HorizontalLayoutGroup>();
        hl.padding  = new RectOffset(26, 22, 8, 4);
        hl.spacing  = 10;
        hl.childAlignment      = TextAnchor.MiddleLeft;
        hl.childControlWidth   = true;
        hl.childForceExpandWidth  = false;
        hl.childControlHeight  = true;
        hl.childForceExpandHeight = true;

        topbarTitleText = WinText(topbar.transform, "TopbarTitle", "Newsdesk", 20, TextPrimary, FontStyles.Bold);
        Layout(topbarTitleText.gameObject, -1f, -1f, 0f, 1f);

        // Badges container
        GameObject badges = Element(topbar.transform, "Badges");
        topbarBadges = badges.transform;
        Layout(badges, -1f, -1f, 0f, 1f);
        HorizontalLayoutGroup badgeLayout = badges.AddComponent<HorizontalLayoutGroup>();
        badgeLayout.spacing = 6;
        badgeLayout.childAlignment      = TextAnchor.MiddleLeft;
        badgeLayout.childControlWidth   = true;
        badgeLayout.childForceExpandWidth = false;
        badgeLayout.childControlHeight  = true;
        badgeLayout.childForceExpandHeight = false;

        // Flex spacer
        GameObject spacer = Element(topbar.transform, "Spacer");
        Layout(spacer, -1f, -1f, 1f, 1f);

        // Actions
        GameObject actions = Element(topbar.transform, "Actions");
        topbarActions = actions.transform;
        Layout(actions, -1f, -1f, 0f, 1f);
        HorizontalLayoutGroup actLayout = actions.AddComponent<HorizontalLayoutGroup>();
        actLayout.spacing = 8;
        actLayout.childAlignment      = TextAnchor.MiddleRight;
        actLayout.childControlWidth   = true;
        actLayout.childForceExpandWidth = false;
        actLayout.childControlHeight  = true;
        actLayout.childForceExpandHeight = false;
    }

    private void BuildContentArea(Transform parent)
    {
        RectTransform content;
        RectTransform scroll = CreateScroll(parent, "MainScroll", out content, false);
        Layout(scroll.gameObject, -1f, -1f, 1f, 1f);
        VerticalLayoutGroup cvl = content.GetComponent<VerticalLayoutGroup>();
        if (cvl != null) { cvl.padding = new RectOffset(26, 22, 8, 16); cvl.spacing = 14; }
        contentArea = content;
    }

    private void BuildStatusbar(Transform parent)
    {
        GameObject bar = PanelObject(parent, "Statusbar", Color.clear);
        Layout(bar, -1f, StatusbarHeight, 1f, 0f);

        HorizontalLayoutGroup hl = bar.AddComponent<HorizontalLayoutGroup>();
        hl.padding  = new RectOffset(26, 22, 0, 6);
        hl.spacing  = 16;
        hl.childAlignment      = TextAnchor.MiddleLeft;
        hl.childControlWidth   = true;
        hl.childForceExpandWidth  = false;
        hl.childControlHeight  = true;
        hl.childForceExpandHeight = true;

        statusbarText = WinText(bar.transform, "Status", "Backend offline", 12, TextSecondary);
        statusbarAgentText = WinText(bar.transform, "Agent", string.Empty, 12, TextSecondary);
        Layout(statusbarAgentText.gameObject, -1f, -1f, 1f, 1f);
        statusbarRightText = WinText(bar.transform, "Right", "Ready", 12, AccentBlueSoft);

        // ESC → apartment hint, sitting right next to the "Ready" text.
        BuildExitHint(bar.transform);
    }

    // ── Taskbar ─────────────────────────────────────────────────────────────
    // The wallpaper (Desktop-background) already paints the Windows-style bar,
    // the Start logo and the clock. We only overlay the app-tab buttons on top
    // of it, starting just to the right of the painted Start logo.
    private void BuildTaskbar(Transform parent)
    {
        // Taskbar background removed (transparent) — the app icons stay, but the
        // gray bar at the bottom of the screen is no longer drawn.
        GameObject bar = PanelObject(parent, "TaskbarBackground", Color.clear);
        Image barImg = bar.GetComponent<Image>();
        if (barImg != null) barImg.raycastTarget = false;
        RectTransform br = bar.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0, 0);
        br.anchorMax = new Vector2(1, 0);
        br.pivot = new Vector2(0.5f, 0);
        br.sizeDelta = new Vector2(0, TaskbarHeight);
        br.anchoredPosition = Vector2.zero;

        taskbarObject = Element(bar.transform, "TaskbarApps");
        RectTransform r = taskbarObject.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 0.5f);
        r.anchorMax = new Vector2(0f, 0.5f);
        r.pivot     = new Vector2(0f, 0.5f);
        r.anchoredPosition = new Vector2(106f, 0f);   // perfectly centered vertically within the taskbar background
        r.sizeDelta        = new Vector2(1400f, 70f);   // height for the app container

        HorizontalLayoutGroup hl = taskbarObject.AddComponent<HorizontalLayoutGroup>();
        hl.spacing  = 12;                            // slightly more spacing for larger icons
        hl.childAlignment      = TextAnchor.MiddleLeft;
        hl.childControlWidth   = false;
        hl.childForceExpandWidth  = false;
        hl.childControlHeight  = true;              // CRITICAL: uGUI forces the child RectTransforms to match exactly 70px height!
        hl.childForceExpandHeight = false;

        taskbarApps = taskbarObject.transform;
    }

    private void BuildStartIcon(Transform parent)
    {
        // 4 colored squares
        Color[] tileColors = { Html("#f35325"), Html("#81bc06"), Html("#05a6f0"), Html("#ffba08") };
        Vector2[] positions = { new Vector2(-5f, 5f), new Vector2(5f, 5f), new Vector2(-5f, -5f), new Vector2(5f, -5f) };
        for (int i = 0; i < 4; i++)
        {
            GameObject tile = PanelObject(parent, $"Tile{i}", tileColors[i]);
            RectTransform tr = tile.GetComponent<RectTransform>();
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot     = new Vector2(0.5f, 0.5f);
            tr.sizeDelta = new Vector2(8f, 8f);
            tr.anchoredPosition = positions[i];
        }
    }

    private void BuildSystemTray(Transform parent)
    {
        GameObject tray = Element(parent, "SystemTray");
        Layout(tray, 160f, -1f, 0f, 1f);
        HorizontalLayoutGroup hl = tray.AddComponent<HorizontalLayoutGroup>();
        hl.padding  = new RectOffset(8, 8, 0, 0);
        hl.spacing  = 8;
        hl.childAlignment      = TextAnchor.MiddleRight;
        hl.childControlWidth   = true;
        hl.childForceExpandWidth = false;
        hl.childControlHeight  = true;
        hl.childForceExpandHeight = true;

        // Spacer
        GameObject spacer = Element(tray.transform, "TraySpace");
        Layout(spacer, -1f, -1f, 1f, 1f);

        // Clock block
        GameObject clock = Element(tray.transform, "Clock");
        Layout(clock, 80f, -1f, 0f, 1f);
        VerticalLayoutGroup vl = clock.AddComponent<VerticalLayoutGroup>();
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        taskbarTimeText = WinText(clock.transform, "Time", "--:--", 12, TextPrimary, FontStyles.Bold);
        taskbarDateText = WinText(clock.transform, "Date", "--/--/----", 10, TextSecondary);
        taskbarTimeText.alignment = taskbarDateText.alignment = TextAlignmentOptions.Center;

        UpdateTaskbarClock();
        StartCoroutine(ClockTick());
    }

    private IEnumerator ClockTick()
    {
        while (true) { yield return new WaitForSecondsRealtime(30f); UpdateTaskbarClock(); }
    }

    private void UpdateTaskbarClock()
    {
        if (taskbarTimeText == null || taskbarDateText == null) return;
        DateTime now = DateTime.Now;
        taskbarTimeText.text = now.ToString("HH:mm");
        taskbarDateText.text = now.ToString("dd.MM.yyyy");
    }

    // ── Boot state overlay ──────────────────────────────────────────────────
    private void BuildBootState(Transform parent)
    {
        bootStateObject = PanelObject(parent, "BootOverlay", Html("#0d1b2af0")); // original translucent dark blue
        Stretch(bootStateObject.GetComponent<RectTransform>(), 0, 0, 0, 0);

        VerticalLayoutGroup vl = bootStateObject.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(0, 0, 200, 0); // original center padding
        vl.childAlignment = TextAnchor.UpperCenter; // original center alignment
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandHeight = false;

        bootTitleText = WinText(bootStateObject.transform, "BootTitle", "STARTING UP...", 48, TextPrimary, FontStyles.Bold);
        bootTitleText.alignment = TextAlignmentOptions.Center;

        bootBodyText  = WinText(bootStateObject.transform, "BootBody", "Connecting to system network...", 18, TextSecondary);
        bootBodyText.alignment  = TextAlignmentOptions.Center;
    }

    // ── Notification toast ──────────────────────────────────────────────────
    private void BuildNotificationToast(Transform parent)
    {
        notificationToast = PanelObject(parent, "NotifToast", WinBg);
        RectTransform nr = notificationToast.GetComponent<RectTransform>();
        nr.anchorMin = new Vector2(1f, 0f);
        nr.anchorMax = new Vector2(1f, 0f);
        nr.pivot     = new Vector2(1f, 0f);
        nr.anchoredPosition = new Vector2(-12f, TaskbarHeight + 8f);
        nr.sizeDelta = new Vector2(NotificationWidth, NotificationHeight);

        Outline outline = notificationToast.AddComponent<Outline>();
        outline.effectColor    = CardBorder;
        outline.effectDistance = new Vector2(0.5f, 0.5f);

        VerticalLayoutGroup vl = notificationToast.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14, 14, 12, 12);
        vl.spacing = 4;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandHeight = false;

        notifTitleText = WinText(notificationToast.transform, "NotifTitle", string.Empty, 12, TextPrimary, FontStyles.Bold);
        notifBodyText  = WinText(notificationToast.transform, "NotifBody", string.Empty, 11, TextSecondary);
        notifBodyText.textWrappingMode = TextWrappingModes.Normal;

        notificationToast.SetActive(false);
    }

    // ── Exit hint ───────────────────────────────────────────────────────────
    // A compact inline pill that lives in the window status bar, right next to the
    // "Ready" text, telling the player they can leave the computer with the ESC
    // key. The actual ESC handling lives in the player's Input System "Exit"
    // action, which closes the computer overlay.
    private void BuildExitHint(Transform parent)
    {
        Color pillBg = Html("#0a0f1ad8"); // glassy dark, slightly transparent
        GameObject hint = PanelObject(parent, "ExitHint", pillBg);
        MakeRounded(hint, pillBg, 9f);

        Image hintImg = hint.GetComponent<Image>();
        if (hintImg != null) hintImg.raycastTarget = false;

        // The status bar's HorizontalLayoutGroup controls our width/height, so the
        // pill's own layout group reports its preferred width from its children.
        HorizontalLayoutGroup hl = hint.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(7, 9, 1, 1);
        hl.spacing = 6;
        hl.childAlignment      = TextAnchor.MiddleCenter;
        hl.childControlWidth    = true;
        hl.childForceExpandWidth  = false;
        hl.childControlHeight   = true;
        hl.childForceExpandHeight = false;

        // "Q" key-cap
        Color capBg = Html("#1e2535");
        GameObject cap = PanelObject(hint.transform, "QCap", capBg);
        MakeRounded(cap, capBg, 5f);
        Layout(cap, 24f, 17f, 0f, 0f); // Q is narrower than ESC, 24f is perfect!
        cap.GetComponent<Image>().raycastTarget = false;
        HorizontalLayoutGroup capHl = cap.AddComponent<HorizontalLayoutGroup>();
        capHl.childAlignment      = TextAnchor.MiddleCenter;
        capHl.childControlWidth    = true;
        capHl.childForceExpandWidth  = true;
        capHl.childControlHeight   = true;
        capHl.childForceExpandHeight = true;
        TMP_Text capTxt = WinText(cap.transform, "Q", "Q", 9, TextPrimary, FontStyles.Bold);
        capTxt.alignment = TextAlignmentOptions.Center;

        // Label
        TMP_Text lbl = WinText(hint.transform, "Label", "Back to apartment", 11, TextSecondary, FontStyles.Bold);
        lbl.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private void ShowNotification(string title, string body)
    {
        if (notificationToast == null) return;
        notifTitleText.text = DisplayText(title);
        notifBodyText.text  = DisplayText(body);
        notificationToast.SetActive(true);
        if (notifRoutine != null) StopCoroutine(notifRoutine);
        notifRoutine = StartCoroutine(HideNotifAfterDelay(4f));
    }

    private IEnumerator HideNotifAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (notificationToast != null) notificationToast.SetActive(false);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UI RENDER
    // ════════════════════════════════════════════════════════════════════════
    private void RenderAll()
    {
        if (canvasObject == null) return;

        bool hasGame = currentGame != null;
        bootStateObject.SetActive(!hasGame);
        bool windowOpen = hasGame && !windowMinimized;
        mainWindow.SetActive(windowOpen);
        // Desktop icons stay visible behind/around the window (user preference).
        if (desktopLayer != null) desktopLayer.SetActive(true);

        if (hasGame && !windowMinimized)
        {
            UpdateWindowTitle();
            UpdateSidebarScore();
            UpdateSidebarNav();
            UpdateTaskbarApps();
            RenderTabs();
        }
        else
        {
            if (bootTitleText != null) bootTitleText.text = DisplayText(busy ? "STARTING UP..." : "STARTUP BLOCKED");
            if (bootBodyText  != null) bootBodyText.text  = DisplayText(busy ? "Connecting to network services..." : BootFailureMessage());
        }

        UpdateStatusbar();
    }

    private void UpdateWindowTitle()
    {
        if (windowTitleText == null || currentGame == null) return;
        windowTitleText.text = DisplayText(Fallback(currentGame.title, "DeepDetect"));
    }

    private void UpdateSidebarScore()
    {
        if (currentGame == null) return;
        if (sidebarScoreText != null) sidebarScoreText.text = currentGame.score.ToString();
        if (sidebarTickText  != null)
            sidebarTickText.text = DisplayText($"Tick {currentGame.worldTick} / {(currentGame.complete ? "complete" : "active")}");
    }

    private void UpdateSidebarNav()
    {
        if (sidebarNav == null) return;
        Clear(sidebarNav);

        (string tab, string label, string emoji)[] items =
        {
            ("home",     "Newsdesk",  "NEWS"),
            ("email",    "Inbox",     "MAIL"),
            ("telegram", "Telegram",  "MSG"),
            ("briefing", "Briefing",  "INFO"),
        };

        foreach (var (tab, label, emoji) in items)
        {
            bool active = activeTab == tab;
            BuildNavItem(sidebarNav, tab, label, emoji, active);
        }
    }

    private void BuildNavItem(Transform parent, string tab, string label, string iconText, bool active)
    {
        int openCount = 0;
        if (currentGame != null)
        {
            if (tab == "home" || tab == "news") openCount = OpenNewsCount(currentGame.newsItems ?? new List<ComputerNewsItem>());
            else if (tab == "email")    openCount = OpenThreadCount(currentGame.emails ?? new List<ComputerEmailItem>());
            else if (tab == "telegram") openCount = OpenThreadCount(currentGame.telegramThreads ?? new List<ComputerTelegramThread>());
        }

        GameObject row = PanelObject(parent, $"Nav-{tab}", active ? NavActive : Color.clear);
        Layout(row, -1f, 44f, 1f, 0f);

        // Left accent bar as an overlay (ignored by layout → text never shifts)
        if (active)
        {
            GameObject accent = PanelObject(row.transform, "Accent", AccentBlueSoft);
            LayoutElement ale = accent.AddComponent<LayoutElement>(); ale.ignoreLayout = true;
            accent.GetComponent<Image>().raycastTarget = false;
            RectTransform ar = accent.GetComponent<RectTransform>();
            ar.anchorMin = new Vector2(0,0.15f); ar.anchorMax = new Vector2(0,0.85f);
            ar.pivot = new Vector2(0,0.5f); ar.sizeDelta = new Vector2(4f,0);
            ar.anchoredPosition = Vector2.zero;
        }

        Button btn = row.AddComponent<Button>();
        btn.targetGraphic = row.GetComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor = active ? NavActive : Color.clear;
        cb.highlightedColor = active ? NavActive : Html("#ffffff12");
        cb.pressedColor     = Html("#ffffff20");
        btn.colors = cb;
        string capturedTab = tab;
        btn.interactable = !active && !busy && currentGame != null;
        btn.onClick.AddListener(() => { windowMinimized = false; activeTab = capturedTab; if (capturedTab != "home") activeArticleId = string.Empty; RenderTabs(); UpdateTaskbarApps(); UpdateSidebarNav(); RenderAll(); });

        HorizontalLayoutGroup hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.padding  = new RectOffset(12, 12, 0, 0);   // constant → stable alignment, a bit more left
        hl.spacing  = 12;
        hl.childAlignment      = TextAnchor.MiddleLeft;
        hl.childControlWidth   = false;
        hl.childForceExpandWidth  = false;
        hl.childControlHeight  = false;
        hl.childForceExpandHeight = false;

        // Fixed-size icon box → every icon occupies the exact same square, so all
        // labels line up perfectly regardless of source icon aspect ratio.
        Sprite iconSprite = GetNavIconSprite(tab);
        GameObject icoBox = PanelObject(row.transform, "IconBox", Color.clear);
        Layout(icoBox, 28f, 28f, 0f, 0f);
        if (iconSprite != null)
        {
            GameObject ico = PanelObject(icoBox.transform, "Icon", Color.white);
            Image img = ico.GetComponent<Image>();
            img.sprite = iconSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RectTransform icr = ico.GetComponent<RectTransform>();
            icr.anchorMin = Vector2.zero; icr.anchorMax = Vector2.one;
            icr.offsetMin = icr.offsetMax = Vector2.zero;
        }
        else
        {
            TMP_Text ic = WinText(icoBox.transform, "IconTxt", iconText, 10, active ? AccentBlueSoft : TextMuted, FontStyles.Bold);
            ic.alignment = TextAlignmentOptions.Center;
            Stretch(ic.rectTransform);
        }

        TMP_Text lbl = WinText(row.transform, "Label", label, 16, active ? TextPrimary : TextSecondary, active ? FontStyles.Bold : FontStyles.Normal);
        lbl.alignment = TextAlignmentOptions.Left;
        Layout(lbl.gameObject, 130f, 24f, 0f, 0f);

        if (openCount > 0)
        {
            // Orange (indicator1) for the news desk, blue (indicator2) for the rest.
            string indName = (tab == "home" || tab == "news") ? "indicator1" : "indicator2";
            Sprite indSprite = Resources.Load<Sprite>("UI/desktop/" + indName);
            GameObject dot = PanelObject(row.transform, "Indicator", Color.white);
            LayoutElement dle = dot.AddComponent<LayoutElement>(); dle.ignoreLayout = true;
            Image dImg = dot.GetComponent<Image>();
            dImg.raycastTarget = false;
            if (indSprite != null) { dImg.sprite = indSprite; dImg.preserveAspect = true; }
            else dImg.color = (tab == "home" || tab == "news") ? AccentAmber : NavDot;
            RectTransform dr = dot.GetComponent<RectTransform>();
            dr.anchorMin = dr.anchorMax = new Vector2(1f, 0.5f);
            dr.pivot = new Vector2(1f, 0.5f);
            dr.sizeDelta = new Vector2(14f, 14f);
            dr.anchoredPosition = new Vector2(-12f, 0f);
        }
    }

    private void UpdateTaskbarApps()
    {
        if (taskbarApps == null) return;
        Clear(taskbarApps);

        (string tab, string label)[] apps =
        {
            ("home",     "Newsdesk"),
            ("email",    "Inbox"),
            ("telegram", "Telegram"),
            ("briefing", "Briefing"),
        };

        foreach (var (tab, label) in apps)
        {
            bool active = activeTab == tab;
            BuildTaskbarApp(taskbarApps, tab, label, active);
        }
    }

    private void BuildTaskbarApp(Transform parent, string tab, string label, bool active)
    {
        // 1. Create the Tab Background Panel (square button 70x70)
        Color bgCol = active ? Html("#626c7dcc") : Html("#464d5c99"); // semi-transparent grays
        GameObject go = PanelObject(parent, $"TApp-{tab}", bgCol);
        MakeRounded(go, bgCol, 12f); // rounded gray panel around icon
        
        // Width and height are exactly 70px
        float size = 70f; 
        RectTransform r = go.GetComponent<RectTransform>();
        if (r == null) r = go.AddComponent<RectTransform>();
        r.sizeDelta = new Vector2(size, size);
        Layout(go, size, size, 0f, 0f);

        // 2. Add Clean App Icon, centered perfectly inside the square button
        Sprite iconSprite = GetNavIconSprite(tab);
        GameObject iconGo = PanelObject(go.transform, "Icon", Color.clear);
        RectTransform ir = iconGo.GetComponent<RectTransform>();
        if (ir == null) ir = iconGo.AddComponent<RectTransform>();
        ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
        ir.pivot = new Vector2(0.5f, 0.5f);
        ir.anchoredPosition = Vector2.zero;
        ir.sizeDelta = new Vector2(65f, 65f); // 65px as requested
        Layout(iconGo, 65f, 65f, 0f, 0f);

        Image img = iconGo.GetComponent<Image>();
        if (iconSprite != null)
        {
            img.sprite = iconSprite;
            img.color = Color.white;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        // 3. Setup Button Component
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = bgCol;
        cb.highlightedColor = active ? Html("#747f91") : Html("#545d6e");
        cb.pressedColor     = Html("#ffffff30");
        cb.fadeDuration     = 0.08f;
        btn.colors = cb;

        string capturedTab = tab;
        btn.onClick.AddListener(() => { windowMinimized = false; activeTab = capturedTab; if (capturedTab != "home") activeArticleId = string.Empty; RenderTabs(); UpdateTaskbarApps(); UpdateSidebarNav(); RenderAll(); });

        // 4. Active accent underline beneath the tab (clean 3px indicator)
        if (active)
        {
            GameObject underline = PanelObject(go.transform, "Underline", AccentBlueSoft);
            LayoutElement ule = underline.AddComponent<LayoutElement>(); ule.ignoreLayout = true;
            Image uImg = underline.GetComponent<Image>(); uImg.raycastTarget = false;
            RectTransform ur = underline.GetComponent<RectTransform>();
            if (ur == null) ur = underline.AddComponent<RectTransform>();
            ur.anchorMin = new Vector2(0.15f, 0f);
            ur.anchorMax = new Vector2(0.85f, 0f);
            ur.pivot     = new Vector2(0.5f, 0f);
            ur.anchoredPosition = new Vector2(0f, 2f); // beautiful visible blue line
            ur.sizeDelta = new Vector2(0, 3f);
        }
    }

    private void UpdateStatusbar()
    {
        if (statusbarText != null)
            statusbarText.text = DisplayText(initialized ? "● Connected" : "○ Offline");
        if (statusbarAgentText != null && currentGame != null)
            statusbarAgentText.text = DisplayText($"Agent: {Fallback(currentGame.agentMode, "local")} / {Fallback(currentGame.agentModel, "—")}");
    }

    // ─── Tab rendering ───────────────────────────────────────────────────────
    private void RenderTabs()
    {
        if (contentArea == null) return;
        Clear(contentArea);

        // Update topbar title & badges (hide topbar title on home/Newsdesk to avoid duplicate header!)
        if (topbarTitleText != null) 
        {
            topbarTitleText.text = (activeTab == "home") ? "" : TabTitle(activeTab);
        }
        if (topbarBadges != null)    { Clear(topbarBadges); BuildTopbarBadges(); }
        if (topbarActions != null)   { Clear(topbarActions); BuildTopbarActions(); }

        switch (activeTab)
        {
            case "home":
                if (!string.IsNullOrWhiteSpace(activeArticleId) && FindNews(activeArticleId) != null) RenderArticleReader(contentArea);
                else RenderNewsdesk(contentArea);
                break;
            case "email":     RenderInbox(contentArea); break;
            case "telegram":  RenderTelegram(contentArea); break;
            case "briefing":  RenderBriefing(contentArea); break;
            default:          RenderNewsdesk(contentArea); break;
        }
    }

    private string TabTitle(string tab)
    {
        switch (tab)
        {
            case "home":      return "Newsdesk";
            case "email":     return "Inbox";
            case "telegram":  return "Telegram";
            case "briefing":  return "Briefing";
            default:          return "Newsdesk";
        }
    }

    private void BuildTopbarBadges()
    {
        if (currentGame == null) return;
        if (activeTab == "news_removed") // Completely removed topbar badges for Newsdesk tab!
        {
            int open = OpenNewsCount(currentGame.newsItems ?? new List<ComputerNewsItem>());
            AddBadge(topbarBadges, $"{open} open", Html("#f59e0b26"), AccentAmberSoft);
            AddBadge(topbarBadges, $"Tick {currentGame.worldTick}", Html("#3b82f620"), AccentBlueSoft);
        }
        else if (activeTab == "email")
        {
            int open = OpenThreadCount(currentGame.emails ?? new List<ComputerEmailItem>());
            AddBadge(topbarBadges, $"{open} unread", Html("#3b82f620"), AccentBlueSoft);
        }
        else if (activeTab == "telegram")
        {
            int open = OpenThreadCount(currentGame.telegramThreads ?? new List<ComputerTelegramThread>());
            AddBadge(topbarBadges, $"{open} pending", Html("#7c3aed26"), Html("#a78bfa"));
        }
        else if (activeTab == "briefing")
        {
            AddBadge(topbarBadges, currentGame.complete ? "Complete" : "Active", currentGame.complete ? Html("#05966920") : Html("#3b82f620"), currentGame.complete ? AccentGreenSoft : AccentBlueSoft);
        }
    }

    private void AddBadge(Transform parent, string text, Color bg, Color fg)
    {
        GameObject badge = PanelObject(parent, "Badge", bg);
        Layout(badge, -1f, 24f, 0f, 0f);
        HorizontalLayoutGroup hl = badge.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(10, 10, 0, 0);
        TMP_Text lbl = WinText(badge.transform, "L", text, 11, fg, FontStyles.Bold);
        Layout(lbl.gameObject, -1f, -1f, 0f, 1f);
    }

    private void BuildTopbarActions()
    {
        WinActionButton(topbarActions, "Refresh", false, RefreshClicked, 80f);
        if (currentGame != null)
            WinActionButton(topbarActions, "Advance World", true, AdvanceWorldClicked, 130f);
    }

    private void WinActionButton(Transform parent, string label, bool primary, UnityAction onClick, float width)
    {
        Color bg = primary ? AccentBlue : Html("#ffffff0a");
        Color fg = primary ? Color.white : TextSecondary;
        Button btn = WinButton(parent, label, bg, fg, onClick, width, 28f);
        btn.interactable = !busy && (primary ? currentGame != null && initialized : true);
    }

    // ─── Newsdesk ────────────────────────────────────────────────────────────
    private void RenderNewsdesk(Transform parent)
    {
        List<ComputerNewsItem> items = currentGame.newsItems ?? new List<ComputerNewsItem>();
        if (items.Count == 0)
        {
            EmptyState(parent, "No news wires in this shift.");
            return;
        }

        // Header Section
        GameObject header = Element(parent, "NewsdeskHeader");
        Layout(header, -1f, 76f, 1f, 0f);
        VerticalLayoutGroup vl = header.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 6;
        vl.childControlWidth = true; vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        TMP_Text title = WinText(header.transform, "Title", "Newsdesk", 24, TextPrimary, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.Left;

        TMP_Text desc = WinText(header.transform, "Description", "Review incoming wires before they reach the DeepDetect front page.", 13, TextSecondary);
        desc.alignment = TextAlignmentOptions.Left;
        desc.textWrappingMode = TextWrappingModes.Normal;

        // Stats strip (with only ONE panel left as requested)
        BuildNewsStats(parent, items);

        // News cards
        foreach (ComputerNewsItem item in items)
            BuildNewsCard(parent, item);
    }

    private void BuildNewsStats(Transform parent, List<ComputerNewsItem> items)
    {
        GameObject strip = Element(parent, "StatsStrip");
        Layout(strip, -1f, 100f, 1f, 0f);
        HorizontalLayoutGroup hl = strip.AddComponent<HorizontalLayoutGroup>();
        hl.padding  = new RectOffset(0, 0, 10, 10);
        hl.spacing  = 10;
        hl.childControlWidth   = true;
        hl.childForceExpandWidth  = false;
        hl.childControlHeight  = true;
        hl.childForceExpandHeight = true;

        AddStatTile(strip.transform, "Wires", items.Count.ToString(), AccentBlueSoft);
        AddStatTile(strip.transform, "Open",  OpenNewsCount(items).ToString(), AccentAmberSoft);
        AddStatTile(strip.transform, "Tick",  currentGame.worldTick.ToString(), AccentGreenSoft);
    }

    private void AddStatTile(Transform parent, string label, string value, Color accent)
    {
        GameObject tile = PanelObject(parent, $"Stat-{label}", LightCardBg);
        MakeRounded(tile, LightCardBg);
        Layout(tile, 150f, -1f, 0f, 1f);
        Shadow o = tile.AddComponent<Shadow>(); o.effectColor = LightCardShadow; o.effectDistance = new Vector2(0f,-2f);
        VerticalLayoutGroup vl = tile.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14,14,10,10);
        vl.childAlignment = TextAnchor.MiddleCenter;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;
        TMP_Text lbl = WinText(tile.transform, "Label", label, 11, LightTextMuted, FontStyles.Bold);
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.characterSpacing = 2f;
        Color valColor = Color.Lerp(accent, Color.black, 0.35f);
        TMP_Text val = WinText(tile.transform, "Value", value, 26, valColor, FontStyles.Bold);
        val.alignment = TextAlignmentOptions.Center;
    }

    private void BuildNewsCard(Transform parent, ComputerNewsItem item)
    {
        bool decided = !string.IsNullOrWhiteSpace(item.decision);

        GameObject card = PanelObject(parent, $"NewsCard-{item.id}", LightCardBg);
        MakeRounded(card, LightCardBg);
        Layout(card, -1f, -1f, 1f, 0f);
        Shadow o = card.AddComponent<Shadow>(); o.effectColor = LightCardShadow; o.effectDistance = new Vector2(0f, -3f);
        Button openBtn = card.AddComponent<Button>();
        openBtn.targetGraphic = card.GetComponent<Image>();
        ColorBlock openColors = openBtn.colors;
        openColors.normalColor = LightCardBg;
        openColors.highlightedColor = Html("#f8fbff");
        openColors.pressedColor = Html("#e8eefc");
        openBtn.colors = openColors;
        string openId = item.id;
        openBtn.onClick.AddListener(() => OpenArticle(openId));
        VerticalLayoutGroup vl = card.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(24, 22, 18, 18);
        vl.spacing = 11;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        // Left status accent strip (decided = green/red, open = amber), inset so
        // it sits inside the rounded corners.
        Color accentCol = decided ? (item.correct == true ? AccentGreen : AccentRed) : AccentAmber;
        GameObject accent = PanelObject(card.transform, "Accent", accentCol);
        MakeRounded(accent, accentCol);
        LayoutElement accLe = accent.AddComponent<LayoutElement>(); accLe.ignoreLayout = true;
        accent.GetComponent<Image>().raycastTarget = false;
        RectTransform accR = accent.GetComponent<RectTransform>();
        accR.anchorMin = new Vector2(0,0.34f); accR.anchorMax = new Vector2(0,0.66f);
        accR.pivot = new Vector2(0,0.5f); accR.sizeDelta = new Vector2(5f,0); accR.anchoredPosition = new Vector2(7f,0);

        // Header row
        GameObject headerRow = Element(card.transform, "CardHeader");
        Layout(headerRow, -1f, -1f, 1f, 0f);
        HorizontalLayoutGroup hl = headerRow.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10;
        hl.childAlignment      = TextAnchor.MiddleLeft;
        hl.childControlWidth   = true;
        hl.childForceExpandWidth  = false;
        hl.childControlHeight  = true;
        hl.childForceExpandHeight = false;

        // Source pill
        string statusText = NewsStatus(item);
        Color  statusColor = decided ? (item.correct == true ? Html("#0596691f") : Html("#dc26261f")) : Html("#f59e0b22");
        Color  statusFg    = decided ? (item.correct == true ? Html("#047857")   : Html("#b91c1c"))   : Html("#b45309");
        AddBadge(headerRow.transform, statusText, statusColor, statusFg);

        TMP_Text sourceLbl = WinText(headerRow.transform, "Source", SourceHost(item.url, item.source), 12, LightTextMuted);
        Layout(sourceLbl.gameObject, -1f, -1f, 1f, 1f);

        string articleStatus = ArticleStatusLabel(item);
        Color articleBg = ArticleReady(item) ? Html("#0596691f") : Html("#3b82f61c");
        Color articleFg = ArticleReady(item) ? Html("#047857") : Html("#1d4ed8");
        AddBadge(headerRow.transform, articleStatus, articleBg, articleFg);

        if (decided)
        {
            string decText = item.decision.ToUpperInvariant();
            Color decColor = item.decision == "publish" ? Html("#0596691f") : Html("#dc26261f");
            Color decFg    = item.decision == "publish" ? Html("#047857")   : Html("#b91c1c");
            AddBadge(headerRow.transform, decText, decColor, decFg);
        }

        // Title
        TMP_Text title = WinText(card.transform, "Title", Fallback(item.title, "Untitled"), 18, LightText, FontStyles.Bold);
        title.textWrappingMode = TextWrappingModes.Normal;
        title.overflowMode     = TextOverflowModes.Overflow;
        Layout(title.gameObject, -1f, -1f, 1f, 0f);

        // Summary
        TMP_Text summary = WinText(card.transform, "Summary", Fallback(item.summary, "No summary."), 14, LightTextSub);
        summary.textWrappingMode = TextWrappingModes.Normal;
        summary.overflowMode     = TextOverflowModes.Overflow;
        summary.lineSpacing      = 8f;
        Layout(summary.gameObject, -1f, -1f, 1f, 0f);

        // Evidence strip
        if (!string.IsNullOrWhiteSpace(item.editorNote) || !string.IsNullOrWhiteSpace(item.publicPressure))
        {
            GameObject evRow = Element(card.transform, "Evidence");
            Layout(evRow, -1f, -1f, 1f, 0f);
            HorizontalLayoutGroup evHl = evRow.AddComponent<HorizontalLayoutGroup>();
            evHl.spacing = 8;
            evHl.childControlWidth = true; evHl.childForceExpandWidth = false;
            evHl.childControlHeight = true; evHl.childForceExpandHeight = false;
            if (!string.IsNullOrWhiteSpace(item.editorNote))
                AddInlinePill(evRow.transform, $"Note: {item.editorNote}", Html("#e8eefc"), Html("#1d4ed8"));
            if (!string.IsNullOrWhiteSpace(item.publicPressure))
                AddInlinePill(evRow.transform, $"Pressure: {item.publicPressure}", Html("#fdf0dd"), Html("#b45309"));
        }

        // Actions
        BuildNewsActions(card.transform, item);

        // Correct result
        if (item.correct.HasValue)
            AddResult(card.transform, item.correct);
    }

    private void AddInlinePill(Transform parent, string text, Color bg, Color fg)
    {
        GameObject pill = PanelObject(parent, "Pill", bg);
        MakeRounded(pill, bg);
        Layout(pill, -1f, 26f, 0f, 0f);
        // Shrink-wrap the pill to its text (no empty space on the right): the parent
        // Evidence row uses childControlWidth, so it reads this pill's preferred width.
        HorizontalLayoutGroup hl = pill.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(12,12,0,0);
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true; hl.childForceExpandWidth = false;
        hl.childControlHeight = true; hl.childForceExpandHeight = true;
        TMP_Text lbl = WinText(pill.transform, "L", text, 12, fg, FontStyles.Bold);
        lbl.overflowMode = TextOverflowModes.Ellipsis;
        lbl.enableWordWrapping = false;
    }

    private void BuildNewsActions(Transform parent, ComputerNewsItem item)
    {
        bool decided = !string.IsNullOrWhiteSpace(item.decision);
        GameObject row = Element(parent, "Actions");
        Layout(row, -1f, 50f, 1f, 0f);
        HorizontalLayoutGroup hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(0, 0, 6, 0);
        hl.spacing = 12;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true; hl.childForceExpandWidth = false;
        hl.childControlHeight = true; hl.childForceExpandHeight = false;

        Button pub = ImageButton(row.transform, "PublishBtn", "publish-button", () => SendActionClicked("news", item.id, "publish"), 44f);
        Button rej = ImageButton(row.transform, "RejectBtn",  "reject-button",  () => SendActionClicked("news", item.id, "reject"),  44f);
        pub.interactable = !decided && !busy;
        rej.interactable = !decided && !busy;

        if (decided)
        {
            TMP_Text decLabel = WinText(row.transform, "DecLabel", $"-> {item.decision.ToUpperInvariant()}", 13, LightTextMuted, FontStyles.Bold);
            Layout(decLabel.gameObject, -1f, -1f, 1f, 1f);
        }
    }

    private void OpenArticle(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId)) return;
        activeArticleId = itemId;
        activeTab = "home";
        windowMinimized = false;
        UpdateArticlePolling();
        RenderAll();
    }

    private void CloseArticle()
    {
        activeArticleId = string.Empty;
        RenderAll();
    }

    private void RenderArticleReader(Transform parent)
    {
        ComputerNewsItem item = FindNews(activeArticleId);
        if (item == null)
        {
            activeArticleId = string.Empty;
            RenderNewsdesk(parent);
            return;
        }

        GameObject shell = PanelObject(parent, $"ArticleReader-{item.id}", LightCardBg);
        MakeRounded(shell, LightCardBg);
        Layout(shell, -1f, -1f, 1f, 0f);
        Shadow shadow = shell.AddComponent<Shadow>();
        shadow.effectColor = LightCardShadow;
        shadow.effectDistance = new Vector2(0f, -4f);

        VerticalLayoutGroup vl = shell.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(30, 30, 24, 28);
        vl.spacing = 16;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandHeight = false;

        GameObject top = Element(shell.transform, "ReaderTop");
        Layout(top, -1f, 44f, 1f, 0f);
        HorizontalLayoutGroup th = top.AddComponent<HorizontalLayoutGroup>();
        th.spacing = 12;
        th.childAlignment = TextAnchor.MiddleLeft;
        th.childControlWidth = true; th.childForceExpandWidth = false;
        th.childControlHeight = true; th.childForceExpandHeight = true;

        Button back = WinButton(top.transform, "Back to wires", Html("#e5e7eb"), LightText, CloseArticle, 150f, 34f);
        back.interactable = !busy;
        TMP_Text source = WinText(top.transform, "Source", $"{SourceHost(item.articleSourceUrl, SourceHost(item.url, item.source))}  ·  {ArticleStatusLabel(item)}", 12, LightTextMuted, FontStyles.Bold);
        Layout(source.gameObject, -1f, -1f, 1f, 1f);
        AddBadge(top.transform, NewsStatus(item), Html("#f59e0b22"), Html("#b45309"));

        BuildArticleHero(shell.transform, item);

        TMP_Text title = WinText(shell.transform, "ArticleTitle", Fallback(item.title, "Untitled"), 31, LightText, FontStyles.Bold);
        title.textWrappingMode = TextWrappingModes.Normal;
        title.overflowMode = TextOverflowModes.Overflow;
        Layout(title.gameObject, -1f, -1f, 1f, 0f);

        string metaText = $"{Fallback(item.articleByline, "DeepDetect Wire")}  ·  {Fallback(item.publishedAt, "developing")}  ·  {Fallback(item.publicPressure, "editorial review")}";
        TMP_Text meta = WinText(shell.transform, "ArticleMeta", metaText, 13, LightTextMuted);
        meta.textWrappingMode = TextWrappingModes.Normal;
        meta.overflowMode = TextOverflowModes.Overflow;
        Layout(meta.gameObject, -1f, -1f, 1f, 0f);

        GameObject notePanel = PanelObject(shell.transform, "DeskNote", Html("#eef4ff"));
        MakeRounded(notePanel, Html("#eef4ff"), 9f);
        Layout(notePanel, -1f, -1f, 1f, 0f);
        VerticalLayoutGroup noteVl = notePanel.AddComponent<VerticalLayoutGroup>();
        noteVl.padding = new RectOffset(16, 16, 12, 12);
        noteVl.childControlWidth = noteVl.childForceExpandWidth = true;
        noteVl.childControlHeight = true; noteVl.childForceExpandHeight = false;
        TMP_Text note = WinText(notePanel.transform, "Note", $"Desk note: {Fallback(item.editorNote, "Verify source and framing before publication.")}", 14, Html("#1d4ed8"), FontStyles.Bold);
        note.textWrappingMode = TextWrappingModes.Normal;
        note.overflowMode = TextOverflowModes.Overflow;

        foreach (string paragraph in ArticleParagraphs(item))
        {
            TMP_Text p = WinText(shell.transform, "Paragraph", paragraph, 16, LightTextSub);
            p.textWrappingMode = TextWrappingModes.Normal;
            p.overflowMode = TextOverflowModes.Overflow;
            p.lineSpacing = 7f;
            Layout(p.gameObject, -1f, -1f, 1f, 0f);
        }

        if (!string.IsNullOrWhiteSpace(item.articleError))
        {
            TMP_Text err = WinText(shell.transform, "ArticleError", $"Article enrichment note: {item.articleError}", 12, AccentAmber);
            err.textWrappingMode = TextWrappingModes.Normal;
            err.overflowMode = TextOverflowModes.Overflow;
        }

        if (!string.IsNullOrWhiteSpace(item.articleSourceUrl) || !string.IsNullOrWhiteSpace(item.url))
        {
            TMP_Text src = WinText(shell.transform, "SourceUrl", $"Source: {SourceHost(item.articleSourceUrl, SourceHost(item.url, item.source))}", 12, LightTextMuted);
            src.textWrappingMode = TextWrappingModes.Normal;
            src.overflowMode = TextOverflowModes.Overflow;
        }

        BuildNewsActions(shell.transform, item);
        if (item.correct.HasValue)
            AddResult(shell.transform, item.correct);
    }

    private void BuildArticleHero(Transform parent, ComputerNewsItem item)
    {
        GameObject frame = PanelObject(parent, "HeroImage", Html("#dbe6f4"));
        MakeRounded(frame, Html("#dbe6f4"), 13f);
        Layout(frame, -1f, 282f, 1f, 0f);
        Image image = frame.GetComponent<Image>();
        image.raycastTarget = false;
        image.type = Image.Type.Sliced;
        image.preserveAspect = true;

        string imageUrl = ResolveArticleImageUrl(item);
        if (!string.IsNullOrWhiteSpace(imageUrl))
        {
            if (articleImageCache.TryGetValue(imageUrl, out Sprite sprite) && sprite != null)
            {
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.color = Color.white;
            }
            else
            {
                StartArticleImageLoad(imageUrl);
            }
        }

        GameObject overlay = PanelObject(frame.transform, "HeroOverlay", Html("#0f172650"));
        Stretch(overlay.GetComponent<RectTransform>());
        overlay.GetComponent<Image>().raycastTarget = false;

        GameObject captionBox = Element(frame.transform, "CaptionBox");
        LayoutElement cle = captionBox.AddComponent<LayoutElement>();
        cle.ignoreLayout = true;
        RectTransform cr = captionBox.GetComponent<RectTransform>();
        cr.anchorMin = new Vector2(0f, 0f);
        cr.anchorMax = new Vector2(1f, 0f);
        cr.pivot = new Vector2(0.5f, 0f);
        cr.offsetMin = new Vector2(18f, 14f);
        cr.offsetMax = new Vector2(-18f, 82f);

        VerticalLayoutGroup cvl = captionBox.AddComponent<VerticalLayoutGroup>();
        cvl.childControlWidth = cvl.childForceExpandWidth = true;
        cvl.childControlHeight = true; cvl.childForceExpandHeight = false;
        cvl.spacing = 2;

        TMP_Text cap = WinText(captionBox.transform, "Caption", HeroCaption(item), 14, Color.white, FontStyles.Bold);
        cap.textWrappingMode = TextWrappingModes.Normal;
        cap.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text credit = WinText(captionBox.transform, "Credit", Fallback(item.articleImageCredit, ArticleReady(item) ? "News image" : "Article image loading"), 11, Html("#dbeafe"));
        credit.textWrappingMode = TextWrappingModes.Normal;
        credit.overflowMode = TextOverflowModes.Ellipsis;
    }

    // ─── Inbox ───────────────────────────────────────────────────────────────
    private void RenderInbox(Transform parent)
    {
        List<ComputerEmailItem> emails = currentGame.emails ?? new List<ComputerEmailItem>();
        if (emails.Count == 0) { EmptyState(parent, "No inbox threads in this shift."); return; }
        if (string.IsNullOrWhiteSpace(activeEmailId) || !EmailExists(activeEmailId))
            activeEmailId = FirstOpenEmailId();
        ComputerEmailItem active = FindEmail(activeEmailId) ?? emails[0];

        // Two column layout — fills the window height like a real mail client.
        GameObject body = Element(parent, "InboxBody");
        Layout(body, -1f, ChatBodyHeight, 1f, 0f);
        HorizontalLayoutGroup hl = body.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10;
        hl.childControlWidth = true; hl.childForceExpandWidth = false;
        hl.childControlHeight = true; hl.childForceExpandHeight = true;

        // Left: thread list
        GameObject left = PanelObject(body.transform, "ThreadList", Html("#0f1726"));
        MakeRounded(left, Html("#0f1726"));
        Layout(left, 264f, -1f, 0f, 1f);

        VerticalLayoutGroup leftVl = left.AddComponent<VerticalLayoutGroup>();
        leftVl.padding = new RectOffset(8,8,8,8);
        leftVl.spacing = 6;
        leftVl.childControlWidth = leftVl.childForceExpandWidth = true;
        leftVl.childControlHeight = true; leftVl.childForceExpandHeight = false;

        // List header
        GameObject listHeader = PanelObject(left.transform, "ListHeader", Html("#0f1623"));
        Layout(listHeader, -1f, 36f, 1f, 0f);
        HorizontalLayoutGroup lhHl = listHeader.AddComponent<HorizontalLayoutGroup>();
        lhHl.padding = new RectOffset(14,14,0,0);
        lhHl.childAlignment = TextAnchor.MiddleLeft;
        lhHl.childControlWidth = true; lhHl.childForceExpandWidth = true;
        lhHl.childControlHeight = true; lhHl.childForceExpandHeight = true;
        TMP_Text listTitle = WinText(listHeader.transform, "T", "THREADS", 10, TextMuted, FontStyles.Bold);
        Layout(listTitle.gameObject, -1f, -1f, 1f, 1f);
        TMP_Text countLbl = WinText(listHeader.transform, "C", $"{emails.Count}", 10, AccentBlueSoft, FontStyles.Bold);

        foreach (ComputerEmailItem item in emails)
            BuildEmailRow(left.transform, item, item.id == active.id);

        // Right: reader
        BuildEmailReader(body.transform, active);
    }

    private void BuildEmailRow(Transform parent, ComputerEmailItem item, bool selected)
    {
        Color activePurple = Html("#7c3aed44"); // semi-transparent purple
        Color bg = selected ? activePurple : Color.clear;
        GameObject row = PanelObject(parent, $"ERow-{item.id}", bg);
        MakeRounded(row, bg);
        Layout(row, -1f, EmailRowHeight, 1f, 0f);
        Button btn = row.AddComponent<Button>();
        btn.targetGraphic = row.GetComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = bg;
        cb.highlightedColor = bg;                  // no white hover
        cb.pressedColor     = selected ? Html("#7c3aed66") : Html("#ffffff12"); // subtle feedback
        cb.selectedColor    = bg;
        cb.fadeDuration     = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(() => { activeEmailId = item.id; RenderTabs(); });

        VerticalLayoutGroup vl = row.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14, 12, 11, 11);
        vl.spacing = 4;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        bool resolved = ThreadResolved(item);
        TMP_Text from = WinText(row.transform, "From", Fallback(item.fromName, "Sender"), 13, resolved ? TextMuted : TextPrimary, resolved ? FontStyles.Normal : FontStyles.Bold);
        TMP_Text subj = WinText(row.transform, "Subject", Fallback(item.subject, "No subject"), 12, TextSecondary);
        subj.overflowMode = TextOverflowModes.Ellipsis;
        TMP_Text prog = WinText(row.transform, "Progress", ThreadProgress(item), 11, resolved ? AccentGreenSoft : AccentAmberSoft);
    }

    private void BuildEmailReader(Transform parent, ComputerEmailItem active)
    {
        GameObject reader = PanelObject(parent, "Reader", Html("#0e1626"));
        MakeRounded(reader, Html("#0e1626"));
        Layout(reader, -1f, -1f, 1f, 1f);
        VerticalLayoutGroup vl = reader.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(20, 20, 16, 16);
        vl.spacing = 10;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        TMP_Text subj = WinText(reader.transform, "Subject", Fallback(active.subject, "No subject"), 18, TextPrimary, FontStyles.Bold);
        subj.textWrappingMode = TextWrappingModes.Normal;
        subj.overflowMode = TextOverflowModes.Overflow;
        Layout(subj.gameObject, -1f, -1f, 1f, 0f);

        TMP_Text sender = WinText(reader.transform, "Sender", $"{Fallback(active.fromName, "Sender")}  <{Fallback(active.fromEmail, "unknown")}>  ·  {ThreadProgress(active)}", 13, TextSecondary);
        Layout(sender.gameObject, -1f, -1f, 1f, 0f);

        AddThread(reader.transform, EmailMessages(active), active.fromName);
        AddResult(reader.transform, active.correct);
        AddOptionButtons(reader.transform, "email", active.id, active.options, ThreadResolved(active));
        if (!ThreadResolved(active))
            AddCustomReply(reader.transform, "email", active.id, "Write a newsroom reply...");
    }

    // ─── Telegram ────────────────────────────────────────────────────────────
    private void RenderTelegram(Transform parent)
    {
        List<ComputerTelegramThread> threads = currentGame.telegramThreads ?? new List<ComputerTelegramThread>();
        if (threads.Count == 0) { EmptyState(parent, "No Telegram threads in this shift."); return; }
        if (string.IsNullOrWhiteSpace(activeTelegramId) || !TelegramExists(activeTelegramId))
            activeTelegramId = FirstOpenTelegramId();
        ComputerTelegramThread active = FindTelegram(activeTelegramId) ?? threads[0];

        GameObject body = Element(parent, "TelegramBody");
        Layout(body, -1f, ChatBodyHeight, 1f, 0f);
        HorizontalLayoutGroup hl = body.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 10;
        hl.childControlWidth = true; hl.childForceExpandWidth = false;
        hl.childControlHeight = true; hl.childForceExpandHeight = true;

        // Left list
        GameObject left = PanelObject(body.transform, "ChatList", Html("#0f1726"));
        MakeRounded(left, Html("#0f1726"));
        Layout(left, 264f, -1f, 0f, 1f);
        VerticalLayoutGroup leftVl = left.AddComponent<VerticalLayoutGroup>();
        leftVl.padding = new RectOffset(8,8,8,8);
        leftVl.spacing = 6;
        leftVl.childControlWidth = leftVl.childForceExpandWidth = true;
        leftVl.childControlHeight = true; leftVl.childForceExpandHeight = false;

        // List header
        GameObject listHeader = PanelObject(left.transform, "ListHeader", Color.clear);
        Layout(listHeader, -1f, 34f, 1f, 0f);
        HorizontalLayoutGroup lhHl = listHeader.AddComponent<HorizontalLayoutGroup>();
        lhHl.padding = new RectOffset(8,8,0,2);
        lhHl.childAlignment = TextAnchor.MiddleLeft;
        lhHl.childControlWidth = true; lhHl.childForceExpandWidth = true;
        lhHl.childControlHeight = true; lhHl.childForceExpandHeight = true;
        TMP_Text listTitle = WinText(listHeader.transform, "T", "CHATS", 11, TextMuted, FontStyles.Bold);
        listTitle.characterSpacing = 3f;
        Layout(listTitle.gameObject, -1f, -1f, 1f, 1f);
        TMP_Text countLbl = WinText(listHeader.transform, "C", $"{threads.Count}", 10, Html("#a78bfa"), FontStyles.Bold);

        foreach (ComputerTelegramThread thread in threads)
            BuildTelegramRow(left.transform, thread, thread.id == active.id);

        // Right conversation
        BuildTelegramConversation(body.transform, active);
    }

    private void BuildTelegramRow(Transform parent, ComputerTelegramThread thread, bool selected)
    {
        Color activePurple = Html("#7c3aed44"); // semi-transparent purple
        Color baseCol = selected ? activePurple : Color.clear;
        // Initialize the row's base image to its NORMAL state color (not white).
        // The Button's color tint cross-fades from the image's current color to
        // normalColor over fadeDuration when colors are applied; starting from
        // white caused every row to briefly flash white (most visible when
        // switching chats, since RenderTabs rebuilds the whole list).
        GameObject row = PanelObject(parent, $"TRow-{thread.id}", baseCol);
        MakeRounded(row, baseCol);
        Layout(row, -1f, TelegramRowHeight, 1f, 0f);
        Button btn = row.AddComponent<Button>();
        btn.targetGraphic = row.GetComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor = baseCol;
        cb.highlightedColor = baseCol;                 // no white hover
        cb.pressedColor = selected ? Html("#7c3aed66") : Html("#ffffff12"); // subtle feedback
        cb.selectedColor = baseCol;
        cb.fadeDuration = 0.08f;
        btn.colors = cb;
        btn.onClick.AddListener(() => { activeTelegramId = thread.id; RenderTabs(); });

        VerticalLayoutGroup vl = row.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(14,12,11,11);
        vl.spacing = 4;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        bool resolved = ThreadResolved(thread);
        TMP_Text contact = WinText(row.transform, "Contact", Fallback(thread.contact, "Contact"), 13, resolved ? TextMuted : TextPrimary, resolved ? FontStyles.Normal : FontStyles.Bold);
        TMP_Text rel = WinText(row.transform, "Rel", Fallback(thread.relationship, "relationship"), 12, TextSecondary);
        TMP_Text prog = WinText(row.transform, "Prog", ThreadProgress(thread), 11, resolved ? AccentGreenSoft : Html("#a78bfa"));
    }

    private void BuildTelegramConversation(Transform parent, ComputerTelegramThread active)
    {
        GameObject conv = PanelObject(parent, "Conversation", Html("#0e1626"));
        MakeRounded(conv, Html("#0e1626"));
        Layout(conv, -1f, -1f, 1f, 1f);
        VerticalLayoutGroup vl = conv.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(20, 20, 16, 16);
        vl.spacing = 10;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        TMP_Text contactName = WinText(conv.transform, "Contact", Fallback(active.contact, "Contact"), 18, TextPrimary, FontStyles.Bold);
        Layout(contactName.gameObject, -1f, -1f, 1f, 0f);
        TMP_Text meta = WinText(conv.transform, "Meta", $"{Fallback(active.relationship, "relationship")}  ·  {ThreadProgress(active)}", 13, TextSecondary);
        Layout(meta.gameObject, -1f, -1f, 1f, 0f);
        AddThread(conv.transform, active.messages ?? new List<JToken>(), active.contact, true);
        AddResult(conv.transform, active.correct);
        AddOptionButtons(conv.transform, "telegram", active.id, active.options, ThreadResolved(active));
        if (!ThreadResolved(active))
            AddCustomReply(conv.transform, "telegram", active.id, "Write your reply...");
    }

    // ─── Briefing ────────────────────────────────────────────────────────────
    private void RenderBriefing(Transform parent)
    {
        // Summary cards
        BuildBriefingSummary(parent);

        // Rules — inside a clean white card (no divider lines)
        string[] rules =
        {
            "1. You decide what appears on the DeepDetect front page.",
            "2. Publish only when source and framing are credible.",
            "3. Manipulated stories carry pressure, unsupported certainty, or emotional wording.",
            "4. Email and Telegram sidequests affect your trust score.",
        };
        GameObject rulesCard = BuildSectionCard(parent, "RULES OF ENGAGEMENT");
        foreach (string rule in rules)
        {
            TMP_Text r = WinText(rulesCard.transform, "Rule", rule, 14, LightTextSub);
            r.textWrappingMode = TextWrappingModes.Normal; r.overflowMode = TextOverflowModes.Overflow;
            r.lineSpacing = 6f;
            Layout(r.gameObject, -1f, -1f, 1f, 0f);
        }

        // Action log — inside a clean white card
        GameObject logCard = BuildSectionCard(parent, "ACTION LOG");
        List<string> lines = currentGame.actionLog ?? new List<string>();
        if (lines.Count == 0)
        {
            TMP_Text empty = WinText(logCard.transform, "EmptyLog", "No actions recorded yet.", 14, LightTextMuted);
            Layout(empty.gameObject, -1f, -1f, 1f, 0f);
        }
        else
        {
            foreach (string line in lines)
            {
                TMP_Text l = WinText(logCard.transform, "Log", line, 13, LightTextSub);
                l.textWrappingMode = TextWrappingModes.Normal; l.overflowMode = TextOverflowModes.Overflow;
                l.lineSpacing = 4f;
                Layout(l.gameObject, -1f, -1f, 1f, 0f);
            }
        }
    }

    // A clean white section card with an uppercase heading. Returns the card
    // transform so callers can append their own content rows.
    private GameObject BuildSectionCard(Transform parent, string heading)
    {
        GameObject card = PanelObject(parent, $"Section-{heading}", LightCardBg);
        MakeRounded(card, LightCardBg, 16f); // rounded corners for beautiful presentation
        Layout(card, -1f, -1f, 1f, 0f);
        Shadow o = card.AddComponent<Shadow>(); o.effectColor = LightCardShadow; o.effectDistance = new Vector2(0f, -3f);
        VerticalLayoutGroup vl = card.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(22, 22, 16, 18);
        vl.spacing = 10;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;
        TMP_Text h = WinText(card.transform, "Heading", heading, 12, LightTextMuted, FontStyles.Bold);
        h.characterSpacing = 3f;
        Layout(h.gameObject, -1f, -1f, 1f, 0f);
        return card;
    }

    private void BuildBriefingSummary(Transform parent)
    {
        // Cards row
        GameObject row = Element(parent, "SummaryRow");
        Layout(row, -1f, -1f, 1f, 0f);
        HorizontalLayoutGroup hl = row.AddComponent<HorizontalLayoutGroup>();
        hl.spacing = 14;
        hl.childControlWidth = true; hl.childForceExpandWidth = true;
        hl.childControlHeight = true; hl.childForceExpandHeight = false;

        // Status card
        BuildSummaryCard(row.transform, "STATUS", new string[]
        {
            currentGame.complete ? "SHIFT COMPLETE" : "SHIFT ACTIVE",
            Fallback(currentGame.title, "DeepDetect"),
            $"Tick {currentGame.worldTick}",
            $"Score: {currentGame.score}",
        }, new Color[]
        {
            currentGame.complete ? Html("#047857") : Html("#1d4ed8"),
            LightText, LightTextMuted, LightText,
        });

        // Quests card
        List<ComputerQuest> quests = currentGame.quests ?? new List<ComputerQuest>();
        string[] questLines = quests.Count == 0
            ? new string[] { "No active quests." }
            : System.Array.ConvertAll(quests.ToArray(), q => $"{q.current}/{q.target} {Fallback(q.title, "Quest")}");
        Color[] questColors = quests.Count == 0
            ? new Color[] { LightTextMuted }
            : System.Array.ConvertAll(quests.ToArray(), q => (Color)(q.complete ? Html("#047857") : LightTextSub));
        BuildSummaryCard(row.transform, "QUESTS", questLines, questColors);

        // Values card
        List<ComputerValue> values = ValuesList();
        if (values.Count > 0)
        {
            GameObject valCard = PanelObject(row.transform, "ValuesCard", LightCardBg);
            MakeRounded(valCard, LightCardBg, 16f); // rounded corners for beautiful presentation
            Layout(valCard, -1f, -1f, 1f, 1f);
            Shadow o = valCard.AddComponent<Shadow>(); o.effectColor = LightCardShadow; o.effectDistance = new Vector2(0f,-3f);
            VerticalLayoutGroup vl = valCard.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(16,16,14,14); vl.spacing = 10;
            vl.childControlWidth = vl.childForceExpandWidth = true;
            vl.childControlHeight = true; vl.childForceExpandHeight = false;
            TMP_Text title = WinText(valCard.transform, "T", "VALUES", 12, LightTextMuted, FontStyles.Bold);
            title.characterSpacing = 3f;
            foreach (ComputerValue v in values)
                AddMeterRow(valCard.transform, Fallback(v.label, "Value"), v.value);
        }
    }

    private void BuildSummaryCard(Transform parent, string title, string[] lines, Color[] colors)
    {
        GameObject card = PanelObject(parent, $"Card-{title}", LightCardBg);
        MakeRounded(card, LightCardBg, 16f); // rounded corners for beautiful presentation
        Layout(card, -1f, -1f, 1f, 1f);
        Shadow o = card.AddComponent<Shadow>(); o.effectColor = LightCardShadow; o.effectDistance = new Vector2(0f,-3f);
        VerticalLayoutGroup vl = card.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(16,16,14,14); vl.spacing = 7;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;
        TMP_Text titleLbl = WinText(card.transform, "T", title, 12, LightTextMuted, FontStyles.Bold);
        titleLbl.characterSpacing = 3f;
        for (int i = 0; i < lines.Length; i++)
        {
            Color c = i < colors.Length ? colors[i] : LightTextSub;
            TMP_Text l = WinText(card.transform, $"L{i}", lines[i], i == 0 ? 15 : 13, c, i == 0 ? FontStyles.Bold : FontStyles.Normal);
            l.textWrappingMode = TextWrappingModes.Normal; l.overflowMode = TextOverflowModes.Overflow;
            Layout(l.gameObject, -1f, -1f, 1f, 0f);
        }
    }

    private void AddMeterRow(Transform parent, string label, int value)
    {
        GameObject block = Element(parent, "Meter");
        Layout(block, -1f, -1f, 1f, 0f);
        VerticalLayoutGroup vl = block.AddComponent<VerticalLayoutGroup>(); vl.spacing = 6;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        // Label + value on one row (value right-aligned)
        GameObject head = Element(block.transform, "Head");
        Layout(head, -1f, 18f, 1f, 0f);
        HorizontalLayoutGroup hhl = head.AddComponent<HorizontalLayoutGroup>();
        hhl.childControlWidth = true; hhl.childForceExpandWidth = false;
        hhl.childControlHeight = true; hhl.childForceExpandHeight = true;
        hhl.childAlignment = TextAnchor.MiddleLeft;
        TMP_Text lbl = WinText(head.transform, "L", label, 13, LightTextSub, FontStyles.Bold);
        Layout(lbl.gameObject, -1f, -1f, 1f, 1f);
        TMP_Text valTxt = WinText(head.transform, "V", $"{value}/100", 13, LightTextMuted);
        valTxt.alignment = TextAlignmentOptions.Right;

        // Rounded track + rounded fill, taller for a neat look
        const float barH = 10f;
        GameObject track = PanelObject(block.transform, "Track", Html("#e2e8f0"));
        MakeRounded(track, Html("#e2e8f0"));
        Layout(track, -1f, barH, 1f, 0f);
        Color fillCol = value >= 55 ? AccentGreen : AccentAmber;
        GameObject fill = PanelObject(track.transform, "Fill", fillCol);
        MakeRounded(fill, fillCol);
        RectTransform fr = fill.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(0,0); fr.anchorMax = new Vector2(Mathf.Clamp01(value/100f), 1f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;
    }

    // ─── Shared UI helpers ───────────────────────────────────────────────────
    private void AddThread(Transform parent, List<JToken> messages, string fallbackSender, bool enableUnsafeLinks = false)
    {
        RectTransform content;
        RectTransform scroll = CreateScroll(parent, "Thread", out content, false);
        // Grow to fill the conversation pane (min ~200) instead of a fixed block,
        // so chats use the whole window like a real messenger.
        Layout(scroll.gameObject, -1f, 200f, 1f, 1f);
        Image scrollImg = scroll.GetComponent<Image>(); if (scrollImg != null) scrollImg.color = Html("#0e1626");
        ScrollRect sr = scroll.GetComponent<ScrollRect>(); if (sr != null) sr.scrollSensitivity = 30f;
        VerticalLayoutGroup vl = content.GetComponent<VerticalLayoutGroup>();
        if (vl != null) { vl.padding = new RectOffset(12,12,12,12); vl.spacing = 8; }

        if (messages == null || messages.Count == 0)
        { WinText(content, "Empty", "No messages yet.", 13, TextMuted); return; }

        foreach (JToken msg in messages)
        {
            bool player = MessageRole(msg) == "player" || MessageSender(msg) == "You";
            // Player = blue on the right, others = gray on the left (real messenger).
            Color bubbleBg = player ? Html("#2b6cb0") : Html("#2a3344");

            // Row wrapper offsets the bubble left or right so it never spans full width.
            GameObject rowWrap = Element(content, "MsgRow");
            Layout(rowWrap, -1f, -1f, 1f, 0f);
            HorizontalLayoutGroup rhl = rowWrap.AddComponent<HorizontalLayoutGroup>();
            rhl.padding = player ? new RectOffset(90, 0, 0, 0) : new RectOffset(0, 90, 0, 0);
            rhl.childControlWidth = true; rhl.childForceExpandWidth = true;
            rhl.childControlHeight = true; rhl.childForceExpandHeight = false;
            rhl.childAlignment = player ? TextAnchor.UpperRight : TextAnchor.UpperLeft;

            GameObject bubble = PanelObject(rowWrap.transform, "Bubble", bubbleBg);
            MakeRounded(bubble, bubbleBg);
            Layout(bubble, -1f, -1f, 1f, 0f);
            VerticalLayoutGroup bvl = bubble.AddComponent<VerticalLayoutGroup>();
            bvl.padding = new RectOffset(14,14,10,10); bvl.spacing = 4;
            bvl.childControlWidth = bvl.childForceExpandWidth = true;
            bvl.childControlHeight = true; bvl.childForceExpandHeight = false;

            string sender = Fallback(MessageSender(msg), player ? "You" : fallbackSender);
            TMP_Text senderLbl = WinText(bubble.transform, "Sender", sender, 11, player ? Html("#cfe3ff") : Html("#9fb3cc"), FontStyles.Bold);
            TMP_Text bodyLbl   = WinText(bubble.transform, "Text", MessageText(msg), 14, Color.white);
            bodyLbl.textWrappingMode = TextWrappingModes.Normal;
            bodyLbl.overflowMode     = TextOverflowModes.Overflow;
            bodyLbl.lineSpacing      = 5f;
            Layout(bodyLbl.gameObject, -1f, -1f, 1f, 0f);

            AddMessageLinks(bubble.transform, msg, enableUnsafeLinks);
        }
    }

    private void AddMessageLinks(Transform parent, JToken message, bool enableUnsafeLinks)
    {
        if (!enableUnsafeLinks) return;
        List<JToken> links = MessageLinks(message);
        if (links.Count == 0) return;

        GameObject group = Element(parent, "Links");
        Layout(group, -1f, -1f, 1f, 0f);
        VerticalLayoutGroup vl = group.AddComponent<VerticalLayoutGroup>();
        vl.spacing = 6;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;

        foreach (JToken link in links)
        {
            if (!LinkUnsafe(link)) continue;
            string label = Fallback(LinkLabel(link), "Open link");
            string url = Fallback(LinkUrl(link), "about:blank");

            GameObject go = PanelObject(group.transform, "UnsafeLink", Html("#0f766e"));
            MakeRounded(go, Html("#0f766e"), 8f);
            Layout(go, -1f, -1f, 1f, 0f);
            VerticalLayoutGroup bvl = go.AddComponent<VerticalLayoutGroup>();
            bvl.padding = new RectOffset(12, 12, 8, 8);
            bvl.spacing = 3;
            bvl.childControlWidth = bvl.childForceExpandWidth = true;
            bvl.childControlHeight = true; bvl.childForceExpandHeight = false;

            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = go.GetComponent<Image>();
            ColorBlock cb = btn.colors;
            cb.normalColor = Html("#0f766e");
            cb.highlightedColor = Html("#14b8a6");
            cb.pressedColor = Html("#0f4f47");
            cb.disabledColor = Html("#334155");
            cb.fadeDuration = 0.08f;
            btn.colors = cb;
            btn.interactable = !fakeBrowserActive && !virusActive;
            btn.onClick.AddListener(() => HandleUnsafeTelegramLinkClicked(label, url));

            TMP_Text title = WinText(go.transform, "Label", label, 13, Color.white, FontStyles.Bold);
            title.textWrappingMode = TextWrappingModes.Normal;
            title.overflowMode = TextOverflowModes.Overflow;
            title.raycastTarget = false;
            Layout(title.gameObject, -1f, -1f, 1f, 0f);

            TMP_Text host = WinText(go.transform, "Url", SourceHost(url, url), 11, Html("#bbf7d0"));
            host.textWrappingMode = TextWrappingModes.Normal;
            host.overflowMode = TextOverflowModes.Ellipsis;
            host.raycastTarget = false;
            Layout(host.gameObject, -1f, -1f, 1f, 0f);
        }
    }

    private void AddOptionButtons(Transform parent, string surface, string itemId, List<ComputerOption> options, bool resolved)
    {
        if (options == null || options.Count == 0) return;

        // Reply options are rendered as outgoing chat bubbles — blue and aligned
        // to the right, exactly like the player's own sent messages in AddThread —
        // so they read as "messages you can send" rather than flat form buttons.
        GameObject group = Element(parent, "Options");
        Layout(group, -1f, -1f, 1f, 0f);
        VerticalLayoutGroup gvl = group.AddComponent<VerticalLayoutGroup>();
        gvl.spacing = 8;
        gvl.childControlWidth = gvl.childForceExpandWidth = true;
        gvl.childControlHeight = true; gvl.childForceExpandHeight = false;

        // Small right-aligned caption above the suggestions.
        TMP_Text caption = WinText(group.transform, "SuggestLabel", "SUGGESTED REPLIES", 10, Html("#7f9bc0"), FontStyles.Bold);
        caption.characterSpacing = 3f;
        caption.alignment = TextAlignmentOptions.Right;
        Layout(caption.gameObject, -1f, 16f, 1f, 0f);

        Color bubbleBg = Html("#2b6cb0");          // same blue as the player's messages
        bool enabled = !resolved && !busy;

        foreach (ComputerOption opt in options)
        {
            if (opt == null) continue;
            string capturedId = opt.id;

            // Right-aligned row so the bubble hugs the right edge (outgoing look).
            GameObject rowWrap = Element(group.transform, "OptionRow");
            Layout(rowWrap, -1f, -1f, 1f, 0f);
            HorizontalLayoutGroup rhl = rowWrap.AddComponent<HorizontalLayoutGroup>();
            rhl.padding = new RectOffset(70, 0, 0, 0);
            rhl.childControlWidth = true; rhl.childForceExpandWidth = true;
            rhl.childControlHeight = true; rhl.childForceExpandHeight = false;
            rhl.childAlignment = TextAnchor.UpperRight;

            GameObject bubble = PanelObject(rowWrap.transform, "OptionBubble", bubbleBg);
            MakeRounded(bubble, bubbleBg);
            Layout(bubble, -1f, -1f, 1f, 0f);
            VerticalLayoutGroup bvl = bubble.AddComponent<VerticalLayoutGroup>();
            bvl.padding = new RectOffset(16, 16, 11, 11); bvl.spacing = 2;
            bvl.childControlWidth = bvl.childForceExpandWidth = true;
            bvl.childControlHeight = true; bvl.childForceExpandHeight = false;

            // The whole bubble is the clickable button (with hover/press feedback).
            Button btn = bubble.AddComponent<Button>();
            btn.targetGraphic = bubble.GetComponent<Image>();
            ColorBlock cb = btn.colors;
            cb.normalColor      = bubbleBg;
            cb.highlightedColor = Color.Lerp(bubbleBg, Color.white, 0.16f);
            cb.pressedColor     = Color.Lerp(bubbleBg, Color.black, 0.14f);
            cb.disabledColor    = Html("#3a4a63");
            cb.fadeDuration     = 0.08f;
            btn.colors = cb;
            btn.interactable = enabled;
            btn.onClick.AddListener(() => SendActionClicked(surface, itemId, capturedId));

            TMP_Text body = WinText(bubble.transform, "Text", Fallback(opt.label, opt.id), 14, Color.white, FontStyles.Normal);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.overflowMode     = TextOverflowModes.Overflow;
            body.lineSpacing      = 4f;
            body.alignment        = TextAlignmentOptions.Left;
            body.raycastTarget    = false;   // let clicks fall through to the bubble button
            Layout(body.gameObject, -1f, -1f, 1f, 0f);
        }
    }

    private void AddCustomReply(Transform parent, string surface, string itemId, string placeholder)
    {
        Color boxBg = Html("#10192b");
        GameObject box = PanelObject(parent, "CustomReply", boxBg);
        MakeRounded(box, boxBg);
        Layout(box, -1f, -1f, 1f, 0f);
        HorizontalLayoutGroup hl = box.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(12,12,12,12); hl.spacing = 10;
        hl.childAlignment = TextAnchor.MiddleLeft;
        hl.childControlWidth = true; hl.childForceExpandWidth = false;
        hl.childControlHeight = true; hl.childForceExpandHeight = true;
        TMP_InputField input = InputField(box.transform, placeholder);
        Layout(input.gameObject, -1f, 50f, 1f, 0f);
        ImageButton(box.transform, "SendBtn", "send-button", () => SendCustomReplyClicked(surface, itemId, input), 50f);
    }

    private void AddResult(Transform parent, bool? correct)
    {
        if (!correct.HasValue) return;
        Color c = correct.Value ? AccentGreenSoft : AccentRedSoft;
        Color bg = correct.Value ? Html("#05966918") : Html("#dc262618");
        GameObject pill = PanelObject(parent, "Result", bg);
        Layout(pill, -1f, 28f, 1f, 0f);
        HorizontalLayoutGroup hl = pill.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(10,10,0,0);
        hl.childAlignment = TextAnchor.MiddleLeft;
        TMP_Text lbl = WinText(pill.transform, "L", correct.Value ? "✓ Correct call" : "⚠ Risky call", 12, c, FontStyles.Bold);
        Layout(lbl.gameObject, -1f, -1f, 0f, 1f);
    }

    private void EmptyState(Transform parent, string message)
    {
        GameObject wrap = Element(parent, "EmptyState");
        Layout(wrap, -1f, 200f, 1f, 0f);
        VerticalLayoutGroup vl = wrap.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(0,0,60,0);
        vl.childAlignment = TextAnchor.UpperCenter;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true; vl.childForceExpandHeight = false;
        TMP_Text lbl = WinText(wrap.transform, "Empty", message, 14, TextMuted);
        lbl.alignment = TextAlignmentOptions.Center;
        Layout(lbl.gameObject, -1f, -1f, 1f, 0f);
    }

    private void Divider(Transform parent)
    {
        GameObject div = PanelObject(parent, "Divider", Html("#ffffff08"));
        Layout(div, -1f, 1f, 1f, 0f);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  LOW-LEVEL WIDGET FACTORY
    // ════════════════════════════════════════════════════════════════════════
    private static Button WinButton(Transform parent, string label, Color bg, Color fg, UnityAction onClick, float width, float height)
    {
        GameObject go = PanelObject(parent, $"Btn-{label}", bg);
        Layout(go, width, height, 0f, 0f);
        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = bg;
        cb.highlightedColor = Color.Lerp(bg, Color.white, 0.15f);
        cb.pressedColor     = Color.Lerp(bg, Color.black, 0.15f);
        cb.disabledColor    = Html("#1e2535");
        btn.colors = cb;
        btn.onClick.AddListener(onClick);
        TMP_Text txt = WinText(go.transform, "L", label, 12, fg, FontStyles.Bold);
        txt.alignment = TextAlignmentOptions.Center;
        Stretch(txt.rectTransform);
        return btn;
    }

    // Button whose visual is a sprite image (e.g. Figma publish/reject buttons).
    // Height is fixed; width is derived from the sprite aspect ratio.
    private static Button ImageButton(Transform parent, string name, string spriteName, UnityAction onClick, float height)
    {
        Sprite sp = Resources.Load<Sprite>("UI/desktop/" + spriteName);
        float w = height * 3.9f;
        if (sp != null && sp.rect.height > 0f) w = height * (sp.rect.width / sp.rect.height);

        GameObject go = PanelObject(parent, name, Color.white);
        Layout(go, w, height, 0f, 0f);
        Image img = go.GetComponent<Image>();
        if (sp != null) { img.sprite = sp; img.type = Image.Type.Simple; img.preserveAspect = true; }

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        ColorBlock cb = btn.colors;
        cb.normalColor      = Color.white;
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.88f);
        cb.pressedColor     = new Color(0.85f, 0.9f, 1f, 1f);
        cb.disabledColor    = new Color(1f, 1f, 1f, 0.4f);
        cb.fadeDuration     = 0.1f;
        btn.colors = cb;
        btn.onClick.AddListener(onClick);
        return btn;
    }

    private static TMP_InputField InputField(Transform parent, string placeholder)
    {
        GameObject root = PanelObject(parent, "InputField", Color.white);
        Sprite inputSprite = Resources.Load<Sprite>("UI/desktop/input");
        if (inputSprite != null)
        {
            Image img = root.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = inputSprite;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }
        }
        else
        {
            MakeRounded(root, Html("#0f1623"));
        }
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        input.targetGraphic = root.GetComponent<Image>();

        GameObject viewport = Element(root.transform, "Viewport");
        Stretch(viewport.GetComponent<RectTransform>(), 8f, 6f, 8f, 6f);
        viewport.AddComponent<RectMask2D>();

        // The input background is white, so typed text must be dark to be
        // readable (it used to be the light theme TextPrimary on white = barely
        // visible gray).
        Color inputTextColor = Html("#111827");   // near-black
        Color placeholderColor = Html("#9aa3b2"); // medium gray, clearly distinct from typed text

        TMP_Text text = WinText(viewport.transform, "Text", string.Empty, 13, inputTextColor);
        Stretch(text.rectTransform);
        text.alignment = TextAlignmentOptions.TopLeft;

        TMP_Text ph = WinText(viewport.transform, "Placeholder", placeholder, 13, placeholderColor);
        Stretch(ph.rectTransform);
        ph.alignment = TextAlignmentOptions.TopLeft;

        input.textViewport  = viewport.GetComponent<RectTransform>();
        input.textComponent = text;
        input.placeholder   = ph;
        input.lineType       = TMP_InputField.LineType.MultiLineNewline;
        input.characterLimit = 900;

        // A visible, blinking caret so it's obvious the field is focused and ready
        // for typing (the default caret was white-on-white = invisible).
        input.customCaretColor = true;
        input.caretColor       = inputTextColor;
        input.caretWidth       = 2;
        input.caretBlinkRate   = 0.85f;
        input.selectionColor   = new Color(0.20f, 0.45f, 0.85f, 0.35f); // blue text selection highlight
        return input;
    }

    private static TMP_Text WinText(Transform parent, string name, string value, int size, Color color, FontStyles style = FontStyles.Normal)
    {
        GameObject go = Element(parent, name);
        TMP_Text t = go.AddComponent<TextMeshProUGUI>();
        t.text              = DisplayText(value);
        t.fontSize          = WinFontSize(size);
        t.color             = color;
        t.fontStyle         = style;
        t.richText          = false;
        t.raycastTarget     = false;
        t.textWrappingMode  = TextWrappingModes.NoWrap;
        t.overflowMode      = TextOverflowModes.Ellipsis;
        Layout(go, -1f, -1f, 1f, 0f);
        return t;
    }

    private static int WinFontSize(int requested)
    {
        if (requested <= 9)  return 12;
        if (requested <= 11) return 14;
        if (requested <= 12) return 15;
        if (requested <= 13) return 16;
        if (requested <= 14) return 17;
        if (requested <= 16) return 19;
        if (requested <= 18) return 21;
        if (requested <= 22) return 24;
        return Mathf.RoundToInt(requested * 1.06f);
    }

    private static RectTransform CreateScroll(Transform parent, string name, out RectTransform content, bool horizontal)
    {
        GameObject root = Element(parent, name);
        ScrollRect scroll = root.AddComponent<ScrollRect>();
        scroll.horizontal    = horizontal;
        scroll.vertical      = !horizontal;
        scroll.movementType  = ScrollRect.MovementType.Clamped;
        root.AddComponent<Image>().color = Color.clear;

        GameObject viewport = PanelObject(root.transform, "Viewport", Color.clear);
        Stretch(viewport.GetComponent<RectTransform>());
        viewport.AddComponent<RectMask2D>();

        GameObject contentObj = Element(viewport.transform, "Content");
        content = contentObj.GetComponent<RectTransform>();
        content.anchorMin = horizontal ? new Vector2(0,0) : new Vector2(0,1);
        content.anchorMax = horizontal ? new Vector2(0,1) : new Vector2(1,1);
        content.pivot     = new Vector2(0,1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        if (horizontal)
        {
            HorizontalLayoutGroup hl = contentObj.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10;
            hl.childControlHeight = true; hl.childForceExpandHeight = false;
            ContentSizeFitter f = contentObj.AddComponent<ContentSizeFitter>();
            f.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        else
        {
            VerticalLayoutGroup vl = contentObj.AddComponent<VerticalLayoutGroup>();
            vl.padding = new RectOffset(12,12,12,12);
            vl.spacing = 10;
            vl.childControlWidth = vl.childForceExpandWidth = true;
            vl.childControlHeight = true; vl.childForceExpandHeight = false;
            ContentSizeFitter f = contentObj.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content  = content;
        return root.GetComponent<RectTransform>();
    }

    private static GameObject PanelObject(Transform parent, string name, Color color)
    {
        GameObject go = Element(parent, name);
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static GameObject Element(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // Procedurally generated white rounded-rectangle sprite (9-sliced) so any
    // panel can have smooth, non-pixelated rounded corners and be tinted freely.
    private static Dictionary<float, Sprite> _roundedSprites = new Dictionary<float, Sprite>();
    private static Sprite GetRoundedSprite(float radius)
    {
        if (_roundedSprites.TryGetValue(radius, out Sprite existing)) return existing;
        
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - (x + 0.5f), (x + 0.5f) - (size - radius), 0f);
                float dy = Mathf.Max(radius - (y + 0.5f), (y + 0.5f) - (size - radius), 0f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(radius - dist + 0.5f); // 1px anti-aliased edge
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        Sprite s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        _roundedSprites[radius] = s;
        return s;
    }

    // Turn an existing PanelObject's Image into a rounded, tinted panel.
    private static void MakeRounded(GameObject go, Color color, float radius = 12f)
    {
        Image im = go.GetComponent<Image>();
        if (im == null) return;
        im.sprite = GetRoundedSprite(radius);
        im.type = Image.Type.Sliced;
        im.pixelsPerUnitMultiplier = 1f;
        im.color = color;
    }

    private static void Layout(GameObject go, float pw, float ph, float fw, float fh)
    {
        LayoutElement le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
        if (pw >= 0f) le.preferredWidth  = pw;
        if (ph >= 0f) le.preferredHeight = ph;
        le.flexibleWidth  = fw;
        le.flexibleHeight = fh;
    }

    private static void Stretch(RectTransform r) => Stretch(r, 0,0,0,0);
    private static void Stretch(RectTransform r, float l, float t, float ri, float b)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(l, b); r.offsetMax = new Vector2(-ri, -t);
    }

    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  STATUS / BUSY
    // ════════════════════════════════════════════════════════════════════════
    private void SetBusy(bool value, string message)
    {
        busy = value;
        RefreshCanvasInteractivity();
        SetStatus(message);
        UpdateStatusbar();
    }

    private void SetStatus(string message)
    {
        lastStatusMessage = string.IsNullOrWhiteSpace(message) ? "Request failed" : message;
        if (statusbarRightText != null)
            statusbarRightText.text = DisplayText(lastStatusMessage);
        if (bootBodyText != null && currentGame == null)
            bootBodyText.text = DisplayText(lastStatusMessage);
    }

    private string BootFailureMessage()
    {
        if (!string.IsNullOrWhiteSpace(lastStatusMessage) &&
            lastStatusMessage != "Connecting to DeepDetect backend..." &&
            lastStatusMessage != "Starting system network services..." &&
            lastStatusMessage != "Connecting to network services...")
        {
            return lastStatusMessage;
        }

        return "Backend unavailable. Press Refresh to try starting up again.";
    }

    private void RefreshCanvasInteractivity()
    {
        if (canvasGroup == null) return;
        // NOTE: 'busy' is intentionally NOT part of this condition. Toggling
        // canvasGroup.interactable off during a network request forced EVERY
        // Selectable on the canvas into its disabled (semi-white) state, so all
        // buttons looked faded while one was being processed. Double-submits are
        // already prevented by the 'if (busy) return' guards in the click
        // handlers, so the canvas can stay interactive during requests.
        bool interactive = computerOpen && !focusTransitioning && (focusActive || !usingWorldMonitor);
        canvasGroup.interactable   = interactive;
        canvasGroup.blocksRaycasts = interactive;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MONITOR ATTACH / FOCUS
    // ════════════════════════════════════════════════════════════════════════
    private void AttachCanvasToComputerSurface()
    {
        if (canvasObject == null || canvas == null || canvasScaler == null) return;
        Transform monitor = FindMonitorTransform();
        if (monitor == null) { ConfigureScreenFallback(); return; }

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasScaler.uiScaleMode          = CanvasScaler.ScaleMode.ConstantPixelSize;
        canvasScaler.referencePixelsPerUnit = 100f;
        canvasScaler.dynamicPixelsPerUnit   = 6f;

        Vector3 monFwd  = monitor.forward.sqrMagnitude > 0.001f ? monitor.forward.normalized : Vector3.forward;
        Vector3 surfUp  = Vector3.Dot(monitor.up, Vector3.up) > 0.1f ? monitor.up.normalized : Vector3.up;

        // Anchor to the actual screen surface (the lit "glass" quad) so the desktop
        // sits flush on the monitor. Using the monitor's full bounding box pushed
        // the canvas forward by the whole body/stand half-depth (~1.1 units), which
        // made the desktop float ~0.5 units in front of the screen. The screen quad
        // half-depth is only ~0.17, so the canvas now lands on the glass.
        Bounds surfaceBounds;
        Vector3 surfCtr;
        float worldWidth;
        if (TryGetScreenSurfaceBounds(monitor, out surfaceBounds))
        {
            surfCtr    = surfaceBounds.center;
            worldWidth = Mathf.Clamp(surfaceBounds.size.x * 0.985f, 3.2f, 8.2f);
        }
        else
        {
            Bounds bounds; bool hasBounds = TryGetRendererBounds(monitor, out bounds);
            surfaceBounds = hasBounds ? bounds : new Bounds(monitor.position, Vector3.zero);
            surfCtr    = hasBounds ? bounds.center + surfUp * (bounds.size.y * MonitorScreenVerticalOffsetRatio) : monitor.position;
            worldWidth = hasBounds ? Mathf.Clamp(bounds.size.x * MonitorScreenWidthRatio, 3.2f, 8.2f) : MonitorFallbackWorldWidth;
        }

        Vector3 viewDir = GetPreferredViewDirection(surfCtr);
        Vector3 scrNorm = monFwd;
        if (viewDir.sqrMagnitude > 0.001f && Vector3.Dot(scrNorm, viewDir) < 0f) scrNorm = -scrNorm;
        float worldScale = worldWidth / CanvasReferenceWidth;
        float fwdHalfDepth = ProjectBoundsExtent(surfaceBounds.extents, scrNorm);

        canvasRect.anchorMin = canvasRect.anchorMax = new Vector2(0.5f,0.5f);
        canvasRect.pivot     = new Vector2(0.5f,0.5f);
        canvasRect.sizeDelta = new Vector2(CanvasReferenceWidth, CanvasReferenceHeight);
        canvasRect.anchoredPosition = Vector2.zero;
        canvasObject.transform.SetParent(null, false);
        canvasObject.transform.position  = surfCtr + scrNorm * (fwdHalfDepth + MonitorSurfaceOffset);
        canvasObject.transform.rotation  = Quaternion.LookRotation(-scrNorm, surfUp);
        canvasObject.transform.localScale = Vector3.one * worldScale;
        usingWorldMonitor = true;
    }

    private void ConfigureScreenFallback()
    {
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        canvasScaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution  = new Vector2(CanvasReferenceWidth, CanvasReferenceHeight);
        canvasScaler.screenMatchMode      = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        canvasScaler.matchWidthOrHeight   = 0.5f;
        canvasObject.transform.SetParent(null, false);
        canvasObject.transform.localPosition = Vector3.zero;
        canvasObject.transform.localRotation = Quaternion.identity;
        canvasObject.transform.localScale    = Vector3.one;
        canvasRect.anchorMin = Vector2.zero; canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;
        canvasRect.pivot = new Vector2(0.5f,0.5f);
        usingWorldMonitor = false;
    }

    private void EnterFocusMode()
    {
        if (!usingWorldMonitor || canvasObject == null) return;
        Camera cam = Camera.main; if (cam == null) return;
        if (focusActive && !focusTransitioning) return;
        if (!focusActive)
        {
            savedCameraPosition = cam.transform.position;
            savedCameraRotation = cam.transform.rotation;
            savedCameraFov      = cam.fieldOfView;
            focusActive = true;
        }
        canvas.worldCamera = cam;
        Vector3 tPos; Quaternion tRot;
        GetFocusPose(cam, out tPos, out tRot);
        StartFocusTransition(cam, tPos, tRot, FocusFov, FocusEnterTransitionDuration, false, false);
    }

    private void ExitFocusMode()
    {
        if (!focusActive) return;
        Camera cam = Camera.main;
        if (cam != null) { StartFocusTransition(cam, savedCameraPosition, savedCameraRotation, savedCameraFov, FocusExitTransitionDuration, false, true); return; }
        focusActive = false; focusTransitioning = false; RefreshCanvasInteractivity();
    }

    private void ExitFocusModeImmediate()
    {
        if (focusTransitionRoutine != null) { StopCoroutine(focusTransitionRoutine); focusTransitionRoutine = null; }
        if (focusActive)
        {
            Camera cam = Camera.main;
            if (cam != null) { cam.transform.position = savedCameraPosition; cam.transform.rotation = savedCameraRotation; cam.fieldOfView = savedCameraFov; }
        }
        focusActive = false; focusTransitioning = false; RefreshCanvasInteractivity();
    }

    private void GetFocusPose(Camera cam, out Vector3 pos, out Quaternion rot)
    {
        float dist = GetFocusDistance(cam);
        Vector3 target = canvasObject.transform.position + Vector3.up * FocusHeightOffset;
        Vector3 viewDir = GetPreferredViewDirection(canvasObject.transform.position);
        if (viewDir.sqrMagnitude < 0.001f) viewDir = -canvasObject.transform.forward;
        pos = canvasObject.transform.position + viewDir.normalized * dist + Vector3.up * FocusHeightOffset;
        rot = Quaternion.LookRotation((target - pos).normalized, Vector3.up);
    }

    private void SetFocusAnchor(Transform anchor) { if (anchor != null) focusAnchor = anchor; }

    private Vector3 GetPreferredViewDirection(Vector3 origin)
    {
        if (focusAnchor != null) { Vector3 d = focusAnchor.position - origin; d.y=0; if (d.sqrMagnitude > 0.001f) return d.normalized; }
        Camera cam = Camera.main;
        if (cam != null) { Vector3 d = cam.transform.position - origin; d.y=0; if (d.sqrMagnitude > 0.001f) return d.normalized; }
        return Vector3.zero;
    }

    private void StartFocusTransition(Camera cam, Vector3 tPos, Quaternion tRot, float tFov, float dur, bool hideOnDone, bool clearOnDone)
    {
        if (focusTransitionRoutine != null) StopCoroutine(focusTransitionRoutine);
        focusTransitionRoutine = StartCoroutine(FocusTransition(cam, tPos, tRot, tFov, dur, hideOnDone, clearOnDone));
    }

    private IEnumerator FocusTransition(Camera cam, Vector3 tPos, Quaternion tRot, float tFov, float dur, bool hideOnDone, bool clearOnDone)
    {
        focusTransitioning = true; RefreshCanvasInteractivity();
        Vector3 sPos = cam.transform.position; Quaternion sRot = cam.transform.rotation; float sFov = cam.fieldOfView;
        float elapsed = 0f; float d = Mathf.Max(dur, 0.01f);
        while (elapsed < d && cam != null)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0,1, Mathf.Clamp01(elapsed/d));
            cam.transform.position = Vector3.Lerp(sPos, tPos, t);
            cam.transform.rotation = Quaternion.Slerp(sRot, tRot, t);
            cam.fieldOfView        = Mathf.Lerp(sFov, tFov, t);
            yield return null;
        }
        if (cam != null) { cam.transform.position = tPos; cam.transform.rotation = tRot; cam.fieldOfView = tFov; }
        if (hideOnDone && canvasObject != null) canvasObject.SetActive(false);
        if (clearOnDone) focusActive = false;
        focusTransitioning = false; focusTransitionRoutine = null; RefreshCanvasInteractivity();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  MONITOR UTILS
    // ════════════════════════════════════════════════════════════════════════
    private static Transform FindMonitorTransform()
    {
        GameObject m = GameObject.Find(PrimaryMonitorName); if (m != null) return m.transform;
        m = GameObject.Find(FallbackMonitorName); return m != null ? m.transform : null;
    }

    // Finds the lit screen "glass" surface under the monitor (the quad that shows
    // the desktop material) so the canvas can be placed flush on it rather than on
    // the monitor's full bounding box.
    private static bool TryGetScreenSurfaceBounds(Transform monitor, out Bounds bounds)
    {
        bounds = default;
        if (monitor == null) return false;
        MeshRenderer best = null;
        foreach (MeshRenderer r in monitor.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (r == null || r.bounds.size.sqrMagnitude < 0.0001f) continue;
            string matName = r.sharedMaterial != null ? r.sharedMaterial.name : string.Empty;
            bool isScreen =
                matName.IndexOf("MonitorScreen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.gameObject.name.IndexOf("ScreenQuad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                r.gameObject.name.IndexOf("Screen", StringComparison.OrdinalIgnoreCase) >= 0;
            if (isScreen) { best = r; break; }
        }
        if (best == null) return false;
        bounds = best.bounds;
        return bounds.size.sqrMagnitude > 0.0001f;
    }

    private static bool TryGetRendererBounds(Transform root, out Bounds bounds)
    {
        bounds = default; if (root == null) return false;
        Renderer[] rs = root.GetComponentsInChildren<Renderer>(true);
        bool init = false;
        foreach (Renderer r in rs)
        {
            if (r == null || r.bounds.size.sqrMagnitude < 0.001f) continue;
            if (!init) { bounds = r.bounds; init = true; } else bounds.Encapsulate(r.bounds);
        }
        return init;
    }

    private static float ProjectBoundsExtent(Vector3 ext, Vector3 dir)
    {
        Vector3 a = new Vector3(Mathf.Abs(dir.x), Mathf.Abs(dir.y), Mathf.Abs(dir.z));
        return ext.x*a.x + ext.y*a.y + ext.z*a.z;
    }

    private float GetFocusDistance(Camera cam)
    {
        RectTransform cr = canvasObject.GetComponent<RectTransform>();
        if (cr == null || cam == null) return FocusDistance;
        float ww = cr.rect.width  * canvasObject.transform.lossyScale.x;
        float wh = cr.rect.height * canvasObject.transform.lossyScale.y;
        float vRad = FocusFov * Mathf.Deg2Rad;
        float hRad = Camera.VerticalToHorizontalFieldOfView(FocusFov, Mathf.Max(cam.aspect, 0.1f)) * Mathf.Deg2Rad;
        float vFit = wh*0.5f / Mathf.Tan(vRad*0.5f);
        float hFit = ww*0.5f / Mathf.Tan(hRad*0.5f);
        return Mathf.Max(FocusDistance, vFit, hFit) * 1.08f;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  GAME STATE HELPERS
    // ════════════════════════════════════════════════════════════════════════
    private void UpdateArticlePolling()
    {
        bool shouldPoll = computerOpen && initialized && api != null && currentGame != null && ArticleEnrichmentPending();
        if (shouldPoll)
        {
            if (articlePollRoutine == null)
                articlePollRoutine = StartCoroutine(PollArticleEnrichment());
        }
        else
        {
            StopArticlePolling();
        }
    }

    private void StopArticlePolling()
    {
        if (articlePollRoutine != null)
        {
            StopCoroutine(articlePollRoutine);
            articlePollRoutine = null;
        }
    }

    private bool ArticleEnrichmentPending()
    {
        if (currentGame?.newsItems == null) return false;
        foreach (ComputerNewsItem item in currentGame.newsItems)
        {
            if (item == null) continue;
            string status = (item.articleStatus ?? string.Empty).Trim().ToLowerInvariant();
            if (status == "pending" || status == "generating") return true;
        }
        return false;
    }

    private IEnumerator PollArticleEnrichment()
    {
        while (computerOpen && initialized && api != null && currentGame != null && ArticleEnrichmentPending())
        {
            yield return new WaitForSecondsRealtime(2f);
            if (!computerOpen || currentGame == null || busy) continue;

            Task<ComputerGameResponse> task = api.GetGameAsync(currentGame.id);
            while (!task.IsCompleted) yield return null;
            if (task.Status == TaskStatus.RanToCompletion && task.Result != null && task.Result.game != null)
            {
                SetCurrentGame(task.Result.game, false);
                RenderAll();
            }
        }
        articlePollRoutine = null;
    }

    private static ComputerGameState NormalizeGame(ComputerGameState g)
    {
        if (g == null) return null;
        g.values          = g.values          ?? new Dictionary<string, ComputerValue>();
        g.quests          = g.quests          ?? new List<ComputerQuest>();
        g.questLog        = g.questLog        ?? new List<string>();
        g.generationLog   = g.generationLog   ?? new List<string>();
        g.worldFeed       = g.worldFeed       ?? new List<string>();
        g.goals           = g.goals           ?? new List<ComputerGoal>();
        g.newsItems       = g.newsItems       ?? new List<ComputerNewsItem>();
        g.emails          = g.emails          ?? new List<ComputerEmailItem>();
        g.telegramThreads = g.telegramThreads ?? new List<ComputerTelegramThread>();
        g.actionLog       = g.actionLog       ?? new List<string>();
        foreach (ComputerNewsItem n in g.newsItems)    { if (n==null) continue; n.articleParagraphs=n.articleParagraphs??new List<string>(); if (string.IsNullOrWhiteSpace(n.articleStatus)) n.articleStatus="pending"; }
        foreach (ComputerEmailItem e in g.emails)       { if (e==null) continue; e.messages=e.messages??new List<JToken>(); e.options=e.options??new List<ComputerOption>(); }
        foreach (ComputerTelegramThread t in g.telegramThreads) { if (t==null) continue; t.messages=t.messages??new List<JToken>(); t.options=t.options??new List<ComputerOption>(); }
        return g;
    }

    private List<ComputerValue> ValuesList()
    {
        List<ComputerValue> vs = new List<ComputerValue>();
        if (currentGame?.values != null)
            foreach (KeyValuePair<string,ComputerValue> p in currentGame.values)
                if (p.Value != null) vs.Add(p.Value);
        return vs;
    }

    private bool   EmailExists(string id)             => FindEmail(id) != null;
    private bool   TelegramExists(string id)          => FindTelegram(id) != null;
    private bool   NewsExists(string id)              => FindNews(id) != null;

    private ComputerNewsItem FindNews(string id)
    {
        if (currentGame?.newsItems == null) return null;
        foreach (ComputerNewsItem n in currentGame.newsItems) if (n?.id == id) return n;
        return null;
    }

    private ComputerEmailItem FindEmail(string id)
    {
        if (currentGame?.emails == null) return null;
        foreach (ComputerEmailItem e in currentGame.emails) if (e?.id == id) return e;
        return null;
    }

    private ComputerTelegramThread FindTelegram(string id)
    {
        if (currentGame?.telegramThreads == null) return null;
        foreach (ComputerTelegramThread t in currentGame.telegramThreads) if (t?.id == id) return t;
        return null;
    }

    private string FirstOpenEmailId()
    {
        if (currentGame?.emails == null || currentGame.emails.Count == 0) return string.Empty;
        foreach (ComputerEmailItem e in currentGame.emails) if (e!=null && !ThreadResolved(e)) return e.id;
        return currentGame.emails[0].id;
    }

    private string FirstOpenTelegramId()
    {
        if (currentGame?.telegramThreads == null || currentGame.telegramThreads.Count == 0) return string.Empty;
        foreach (ComputerTelegramThread t in currentGame.telegramThreads) if (t!=null && !ThreadResolved(t)) return t.id;
        return currentGame.telegramThreads[0].id;
    }

    private static List<JToken> EmailMessages(ComputerEmailItem e)
    {
        if (e == null) return new List<JToken>();
        if (e.messages != null && e.messages.Count > 0) return e.messages;
        return new List<JToken> { new JValue(Fallback(e.body, "No message body.")) };
    }

    private static bool ThreadResolved(ComputerEmailItem e)    => e != null && (e.resolved || !string.IsNullOrWhiteSpace(e.selected));
    private static bool ThreadResolved(ComputerTelegramThread t) => t != null && (t.resolved || !string.IsNullOrWhiteSpace(t.selected));

    private static string ThreadProgress(ComputerEmailItem item)
    {
        if (ThreadResolved(item)) return "Resolved";
        int max = item != null ? Mathf.Max(item.maxTurns, item.minTurns, 3) : 3;
        return $"Thread {Mathf.Min(item?.chatTurns??0, max)}/{max}";
    }
    private static string ThreadProgress(ComputerTelegramThread item)
    {
        if (ThreadResolved(item)) return "Resolved";
        int max = item != null ? Mathf.Max(item.maxTurns, item.minTurns, 3) : 3;
        return $"Thread {Mathf.Min(item?.chatTurns??0, max)}/{max}";
    }

    private static string MessageText(JToken m)
    {
        if (m == null) return string.Empty;
        if (m.Type == JTokenType.String) return m.Value<string>();
        JToken t = m["text"]; return t != null ? t.Value<string>() : m.ToString(Formatting.None);
    }
    private static string MessageSender(JToken m) { if (m==null||m.Type!=JTokenType.Object) return string.Empty; JToken s=m["sender"]; return s!=null?s.Value<string>():string.Empty; }
    private static string MessageRole(JToken m)   { if (m==null||m.Type!=JTokenType.Object) return string.Empty; JToken r=m["role"];   return r!=null?r.Value<string>():string.Empty; }
    private static List<JToken> MessageLinks(JToken m)
    {
        List<JToken> result = new List<JToken>();
        if (m == null || m.Type != JTokenType.Object) return result;
        JToken links = m["links"];
        if (links == null || links.Type != JTokenType.Array) return result;
        foreach (JToken link in links)
            if (link != null && link.Type == JTokenType.Object)
                result.Add(link);
        return result;
    }
    private static string LinkLabel(JToken link) { if (link==null||link.Type!=JTokenType.Object) return string.Empty; JToken l=link["label"]; return l!=null?l.Value<string>():string.Empty; }
    private static string LinkUrl(JToken link)   { if (link==null||link.Type!=JTokenType.Object) return string.Empty; JToken u=link["url"];   return u!=null?u.Value<string>():string.Empty; }
    private static bool LinkUnsafe(JToken link)
    {
        if (link == null || link.Type != JTokenType.Object) return false;
        JToken unsafeToken = link["unsafe"];
        return unsafeToken != null && unsafeToken.Type == JTokenType.Boolean && unsafeToken.Value<bool>();
    }

    private List<string> ArticleParagraphs(ComputerNewsItem item)
    {
        if (item?.articleParagraphs != null && item.articleParagraphs.Count > 0)
        {
            List<string> paragraphs = new List<string>();
            foreach (string p in item.articleParagraphs)
                if (!string.IsNullOrWhiteSpace(p)) paragraphs.Add(p);
            if (paragraphs.Count > 0) return paragraphs;
        }
        return new List<string> { Fallback(item?.summary, "This developing story needs editorial review before publication.") };
    }

    private static bool ArticleReady(ComputerNewsItem item)
    {
        return item != null && string.Equals(item.articleStatus, "ready", StringComparison.OrdinalIgnoreCase);
    }

    private static string ArticleStatusLabel(ComputerNewsItem item)
    {
        if (item == null) return "Article pending";
        string status = (item.articleStatus ?? string.Empty).Trim().ToLowerInvariant();
        if (status == "ready") return "Full article";
        if (status == "generating") return "Building article";
        if (status == "failed") return "Brief only";
        return "Article pending";
    }

    private static string HeroCaption(ComputerNewsItem item)
    {
        if (!string.IsNullOrWhiteSpace(item?.articleImageCaption)) return item.articleImageCaption;
        if (ArticleReady(item)) return "Editorial image for this wire.";
        return "Full article media is loading.";
    }

    private string ResolveArticleImageUrl(ComputerNewsItem item)
    {
        string url = item?.articleImageUrl;
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri absolute)) return absolute.ToString();
        if (url.StartsWith("/") && api != null && !string.IsNullOrWhiteSpace(api.BaseUrl))
            return api.BaseUrl.TrimEnd('/') + url;
        return url;
    }

    private void StartArticleImageLoad(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || articleImageCache.ContainsKey(url) || articleImageLoading.Contains(url) || articleImageFailed.Contains(url))
            return;
        articleImageLoading.Add(url);
        StartCoroutine(LoadArticleImage(url));
    }

    private IEnumerator LoadArticleImage(string url)
    {
        using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(url))
        {
            request.timeout = 12;
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();
            while (!operation.isDone) yield return null;

            articleImageLoading.Remove(url);
            if (request.result != UnityWebRequest.Result.Success)
            {
                articleImageFailed.Add(url);
                yield break;
            }

            Texture2D tex = DownloadHandlerTexture.GetContent(request);
            if (tex == null)
            {
                articleImageFailed.Add(url);
                yield break;
            }

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            articleImageCache[url] = sprite;
            if (!string.IsNullOrWhiteSpace(activeArticleId))
                RenderAll();
        }
    }

    private static int OpenNewsCount(List<ComputerNewsItem> items) { int c=0; foreach (var i in items) if (i!=null && string.IsNullOrWhiteSpace(i.decision)) c++; return c; }
    private static int OpenThreadCount<T>(List<T> items) { int c=0; foreach (T i in items??new List<T>()) if (!ThreadResolved(i)) c++; return c; }

    private static string NewsStatus(ComputerNewsItem item)
    {
        if (item == null) return "Pending";
        if (!string.IsNullOrWhiteSpace(item.decision)) return item.correct==true ? "Cleared" : "Flagged";
        return item.truthLabel=="manipulated" ? "Needs checks" : "Ready check";
    }

    private static string SourceHost(string url, string fallback)
    {
        Uri u; if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out u))
        { string h = u.Host; return h.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? h.Substring(4) : h; }
        return Fallback(fallback, "source");
    }

    private static string Fallback(string v, string fb) => string.IsNullOrWhiteSpace(v) ? fb : v;

    // ════════════════════════════════════════════════════════════════════════
    //  TEXT SANITIZE
    // ════════════════════════════════════════════════════════════════════════
    private static string DisplayText(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string norm = value.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder(norm.Length);
        bool sawUnsupported = false;
        foreach (char ch in norm)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (ch == '\r') continue;
            if (ch == '\n') { sb.Append('\n'); continue; }
            string r = AsciiReplacement(ch); if (r != null) { sb.Append(r); continue; }
            if (ch >= 32 && ch <= 126) { sb.Append(ch); continue; }
            if (char.IsWhiteSpace(ch)) { sb.Append(' '); continue; }
            sawUnsupported = true; sb.Append(' ');
        }
        string compact = CompactSpaces(sb.ToString());
        return compact.Length == 0 && sawUnsupported ? "[unsupported characters]" : compact;
    }

    private static string AsciiReplacement(char ch)
    {
        switch ((int)ch)
        {
            case 0x00A0: return " ";
            case 0x00A9: return "(C)";
            case 0x00AE: return "(R)";
            case 0x2018: case 0x2019: case 0x201A: case 0x201B: return "'";
            case 0x201C: case 0x201D: case 0x201E: case 0x201F: return "\"";
            case 0x2013: case 0x2014: case 0x2212: return "-";
            case 0x2022: case 0x00B7: return "-";
            case 0x2026: return "...";
            case 0x2122: return "TM";
            default: return null;
        }
    }

    private static string CompactSpaces(string value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        string[] lines = value.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            StringBuilder line = new StringBuilder(lines[i].Length);
            bool prev = false;
            foreach (char ch in lines[i])
            {
                if (ch==' ') { if (!prev) line.Append(ch); prev=true; continue; }
                line.Append(ch); prev=false;
            }
            lines[i] = line.ToString().Trim();
        }
        return string.Join("\n", lines).Trim();
    }

    private static Color Html(string v) { Color c; return ColorUtility.TryParseHtmlString(v, out c) ? c : Color.white; }

    private Sprite GetTabSprite(string tab)
    {
        string spriteName = string.Empty;
        switch (tab)
        {
            case "home":     spriteName = "NewsDesk-icon"; break;
            case "email":    spriteName = "Inbox-icon"; break;
            case "telegram": spriteName = "Telegram-icon"; break;
            case "briefing": spriteName = "Briefing-icon"; break;
            case "recycle":  spriteName = "recycle-bin"; break;
            default: return null;
        }
        return Resources.Load<Sprite>("UI/desktop/" + spriteName);
    }

    private Sprite GetTaskSprite(string tab)
    {
        string spriteName;
        switch (tab)
        {
            case "home":     spriteName = "NewsDesk-task"; break;
            case "email":    spriteName = "Inbox-task";    break;
            case "telegram": spriteName = "telegram-task"; break;
            case "briefing": spriteName = "briefing-task"; break;
            default: return null;
        }
        return Resources.Load<Sprite>("UI/desktop/" + spriteName);
    }

    // Icon-only (no caption) versions used in the WORKSPACES sidebar.
    private Sprite GetNavIconSprite(string tab)
    {
        string spriteName;
        switch (tab)
        {
            case "home":     spriteName = "newsdesk-withoutText"; break;
            case "email":    spriteName = "inbox-withoutText";    break;
            case "telegram": spriteName = "telegram-withoutText"; break;
            case "briefing": spriteName = "briefing-withoutText"; break;
            default: return null;
        }
        return Resources.Load<Sprite>("UI/desktop/" + spriteName);
    }

    // Fake in-game browser used for scam Telegram links. It never opens a real URL.
    private GameObject fakeBrowserOverlay;
    private Coroutine fakeBrowserRoutine;
    private bool fakeBrowserActive;

    private void HandleUnsafeTelegramLinkClicked(string label, string url)
    {
        if (fakeBrowserActive || virusActive || canvasObject == null) return;
        fakeBrowserRoutine = StartCoroutine(FakeBrowserThenVirus(label, url));
    }

    private IEnumerator FakeBrowserThenVirus(string label, string url)
    {
        fakeBrowserActive = true;
        ShowFakeBrowserOverlay(label, url);
        yield return new WaitForSecondsRealtime(1.35f);
        HideFakeBrowserOverlay();
        fakeBrowserActive = false;
        fakeBrowserRoutine = null;
        TriggerVirusAttack();
    }

    private void ShowFakeBrowserOverlay(string label, string url)
    {
        HideFakeBrowserOverlay();
        if (canvasObject == null) return;

        fakeBrowserOverlay = PanelObject(canvasObject.transform, "FakeBrowserOverlay", new Color(0f, 0f, 0f, 0.42f));
        Stretch(fakeBrowserOverlay.GetComponent<RectTransform>());
        Image blocker = fakeBrowserOverlay.GetComponent<Image>();
        if (blocker != null) blocker.raycastTarget = true;
        fakeBrowserOverlay.transform.SetAsLastSibling();

        GameObject window = PanelObject(fakeBrowserOverlay.transform, "FakeBrowserWindow", Html("#f8fafc"));
        MakeRounded(window, Html("#f8fafc"), 12f);
        RectTransform wr = window.GetComponent<RectTransform>();
        wr.anchorMin = wr.anchorMax = new Vector2(0.5f, 0.5f);
        wr.pivot = new Vector2(0.5f, 0.5f);
        wr.sizeDelta = new Vector2(780f, 360f);
        wr.anchoredPosition = Vector2.zero;
        Shadow shadow = window.AddComponent<Shadow>();
        shadow.effectColor = Html("#00000055");
        shadow.effectDistance = new Vector2(0f, -6f);

        VerticalLayoutGroup vl = window.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(18, 18, 16, 18);
        vl.spacing = 12;
        vl.childControlWidth = vl.childForceExpandWidth = true;
        vl.childControlHeight = true;
        vl.childForceExpandHeight = false;

        GameObject chrome = PanelObject(window.transform, "Chrome", Html("#e5e7eb"));
        MakeRounded(chrome, Html("#e5e7eb"), 8f);
        Layout(chrome, -1f, 46f, 1f, 0f);
        HorizontalLayoutGroup chl = chrome.AddComponent<HorizontalLayoutGroup>();
        chl.padding = new RectOffset(12, 12, 6, 6);
        chl.spacing = 10;
        chl.childAlignment = TextAnchor.MiddleLeft;
        chl.childControlWidth = true; chl.childForceExpandWidth = false;
        chl.childControlHeight = true; chl.childForceExpandHeight = true;

        TMP_Text lockIcon = WinText(chrome.transform, "Lock", "!", 13, AccentAmber, FontStyles.Bold);
        Layout(lockIcon.gameObject, 22f, -1f, 0f, 1f);
        TMP_Text address = WinText(chrome.transform, "Address", url, 13, LightTextMuted);
        address.textWrappingMode = TextWrappingModes.NoWrap;
        address.overflowMode = TextOverflowModes.Ellipsis;
        Layout(address.gameObject, -1f, -1f, 1f, 1f);

        TMP_Text title = WinText(window.transform, "Title", Fallback(label, "Opening link..."), 24, LightText, FontStyles.Bold);
        title.textWrappingMode = TextWrappingModes.Normal;
        title.overflowMode = TextOverflowModes.Overflow;
        Layout(title.gameObject, -1f, -1f, 1f, 0f);

        TMP_Text body = WinText(window.transform, "Body", "Checking link...\nLoading remote page...", 17, LightTextSub);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.overflowMode = TextOverflowModes.Overflow;
        body.lineSpacing = 8f;
        Layout(body.gameObject, -1f, -1f, 1f, 0f);

        GameObject barTrack = PanelObject(window.transform, "LoadTrack", Html("#d1d5db"));
        MakeRounded(barTrack, Html("#d1d5db"), 8f);
        Layout(barTrack, -1f, 18f, 1f, 0f);
        GameObject barFill = PanelObject(barTrack.transform, "LoadFill", AccentAmber);
        MakeRounded(barFill, AccentAmber, 8f);
        RectTransform fr = barFill.GetComponent<RectTransform>();
        fr.anchorMin = new Vector2(0f, 0f);
        fr.anchorMax = new Vector2(0.72f, 1f);
        fr.offsetMin = fr.offsetMax = Vector2.zero;
    }

    private void HideFakeBrowserOverlay()
    {
        if (fakeBrowserOverlay != null)
        {
            Destroy(fakeBrowserOverlay);
            fakeBrowserOverlay = null;
        }
    }

    private void CancelFakeBrowserSequence()
    {
        if (fakeBrowserRoutine != null)
        {
            StopCoroutine(fakeBrowserRoutine);
            fakeBrowserRoutine = null;
        }
        fakeBrowserActive = false;
        HideFakeBrowserOverlay();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  VIRUS POP-UP ATTACK (horror event)
    // ════════════════════════════════════════════════════════════════════════
    // After every couple of wrong calls the work window is forced shut and the
    // desktop is overrun by virus pop-ups (virus1..virus4). Each pop-up has a
    // close (✕) button in its corner; closing one can spawn another (like a real
    // infection) up to a hard cap, so the wave always ends and the player can
    // keep playing once the desktop is clean.
    private const int   VirusWrongThreshold = 2;     // attack every N wrong calls
    private const int   VirusInitialCount   = 5;     // pop-ups in the first burst
    private const int   VirusMaxTotal       = 9;     // hard cap on pop-ups per wave
    private const float VirusRespawnChance  = 0.65f; // chance a close spawns a new one

    private int  wrongDecisionsSinceVirus;
    private int  virusSpawnedThisWave;
    private bool virusActive;
    private GameObject virusLayer;
    private readonly List<GameObject> activeVirusPopups = new List<GameObject>();

    private void RegisterWrongDecisions(int count)
    {
        if (count <= 0) return;
        wrongDecisionsSinceVirus += count;
        if (wrongDecisionsSinceVirus >= VirusWrongThreshold && !virusActive)
        {
            wrongDecisionsSinceVirus = 0;
            TriggerVirusAttack();
        }
    }

    private void TriggerVirusAttack()
    {
        if (virusActive || canvasObject == null) return;
        virusActive = true;
        virusSpawnedThisWave = 0;

        // Force the work window shut → reveal the (now infected) desktop.
        windowMinimized = true;
        RenderAll();

        EnsureVirusLayer();

        // Jump-scare audio (the clip is assigned on the GlobalCanvas inspector).
        if (GlobalCanvasPersistent.Instance != null)
            GlobalCanvasPersistent.Instance.PlayVirusScream();

        for (int i = 0; i < VirusInitialCount; i++)
            SpawnVirusPopup();

        Debug.Log("[ComputerOverlay] Virus attack triggered.");
    }

    private void EnsureVirusLayer()
    {
        if (virusLayer != null) return;
        // Slight dark tint + raycast blocker so the desktop/taskbar can't be used
        // until every pop-up is closed.
        virusLayer = PanelObject(canvasObject.transform, "VirusLayer", new Color(0f, 0f, 0f, 0.35f));
        Stretch(virusLayer.GetComponent<RectTransform>());
        Image bg = virusLayer.GetComponent<Image>();
        if (bg != null) bg.raycastTarget = true;
        virusLayer.transform.SetAsLastSibling();
    }

    private void SpawnVirusPopup()
    {
        if (virusLayer == null || virusSpawnedThisWave >= VirusMaxTotal) return;
        virusSpawnedThisWave++;

        int idx = UnityEngine.Random.Range(1, 5); // virus1..virus4
        Sprite sp = Resources.Load<Sprite>("UI/desktop/virus" + idx);

        GameObject popup = PanelObject(virusLayer.transform, "VirusPopup", Color.white);
        Image img = popup.GetComponent<Image>();

        // virus2 is the "boss" pop-up — almost half the screen; the rest stay small.
        float w = idx == 2 ? 960f : 440f;
        float h = idx == 2 ? 700f : 320f;
        if (sp != null)
        {
            img.sprite = sp;
            img.preserveAspect = true;
            if (sp.rect.width > 0f) h = w * (sp.rect.height / sp.rect.width);
        }
        else
        {
            img.color = Html("#10131c");
        }

        RectTransform r = popup.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        r.pivot = new Vector2(0.5f, 0.5f);
        r.sizeDelta = new Vector2(w, h);

        float maxX = Mathf.Max(0f, (CanvasReferenceWidth  - w) * 0.5f - 40f);
        float maxY = Mathf.Max(0f, (CanvasReferenceHeight - h) * 0.5f - 40f);
        r.anchoredPosition = new Vector2(UnityEngine.Random.Range(-maxX, maxX), UnityEngine.Random.Range(-maxY, maxY));
        r.localRotation    = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-6f, 6f));

        BuildVirusCloseButton(popup.transform, popup);
        activeVirusPopups.Add(popup);
    }

    private void BuildVirusCloseButton(Transform parent, GameObject popup)
    {
        GameObject go = PanelObject(parent, "VirusClose", WinBtnClose);
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.anchoredPosition = new Vector2(-8f, -8f);
        r.sizeDelta = new Vector2(40f, 40f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = go.GetComponent<Image>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = WinBtnClose;
        cb.highlightedColor = Color.Lerp(WinBtnClose, Color.white, 0.2f);
        cb.pressedColor     = Color.Lerp(WinBtnClose, Color.black, 0.2f);
        btn.colors = cb;
        btn.onClick.AddListener(() => CloseVirusPopup(popup));

        // ✕ glyph (two crossed bars)
        GameObject glyph = Element(go.transform, "Glyph");
        RectTransform gr = glyph.GetComponent<RectTransform>();
        gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
        gr.pivot = new Vector2(0.5f, 0.5f);
        gr.sizeDelta = new Vector2(16f, 16f);
        gr.anchoredPosition = Vector2.zero;
        MakeBar(glyph.transform, new Vector2(18f, 2.2f), Vector2.zero,  45f, Color.white);
        MakeBar(glyph.transform, new Vector2(18f, 2.2f), Vector2.zero, -45f, Color.white);
    }

    private void CloseVirusPopup(GameObject popup)
    {
        if (popup == null) return;
        activeVirusPopups.Remove(popup);
        Destroy(popup);

        // Closing one can spawn another, until the per-wave cap is reached.
        if (virusSpawnedThisWave < VirusMaxTotal && UnityEngine.Random.value < VirusRespawnChance)
            SpawnVirusPopup();

        // Desktop is clean → the wave is over and the player can resume.
        if (activeVirusPopups.Count == 0)
            EndVirusAttack();
    }

    private void EndVirusAttack()
    {
        virusActive = false;
        activeVirusPopups.Clear();
        if (virusLayer != null) { Destroy(virusLayer); virusLayer = null; }
        // Window stays minimized; the player reopens it via a desktop/taskbar icon.
        Debug.Log("[ComputerOverlay] Virus attack cleared.");
    }
}
