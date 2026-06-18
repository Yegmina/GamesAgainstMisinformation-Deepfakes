using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Collections;

public class EndingCutscenePlayer : MonoBehaviour
{
    [Header("Video Setup")]
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RawImage rawImage;
    [SerializeField] private VideoClip cutsceneClip;

    [Header("UI Elements to Hide/Show")]
    [SerializeField] private GameObject endingPopup;
    [SerializeField] private GameObject tipCard;
    [SerializeField] private GameObject overlay;
    [SerializeField] private GameObject background;

    private bool isFinished = false;
    private float startTime;
    private const float minSkipDelay = 1.0f; // Increased delay to 1s to be safe
    private bool isPreparing = false;

    private void Start()
    {
        startTime = Time.time;

        // Keep the video playing even if the editor/game window loses focus.
        // Without this the VideoPlayer (and the whole game) pauses when unfocused,
        // which makes the cutscene look "stuck on pause".
        Application.runInBackground = true;

        // Make sure the cutscene renders ON TOP of the persistent HUD canvas
        // (GlobalCanvas uses sortingOrder 100). Otherwise the frozen HUD shows
        // over the video and it looks like the game paused.
        Canvas cutsceneCanvas = GetComponentInParent<Canvas>();
        if (cutsceneCanvas != null)
        {
            cutsceneCanvas.overrideSorting = true;
            cutsceneCanvas.sortingOrder = 500;
        }

        // Hide the persistent global HUD so it doesn't overlay the ending.
        GameObject globalCanvas = GameObject.Find("GlobalCanvas");
        if (globalCanvas != null)
        {
            Transform hud = globalCanvas.transform.Find("HUD");
            if (hud != null) hud.gameObject.SetActive(false);
        }

        if (videoPlayer == null)
        {
            videoPlayer = gameObject.GetComponent<VideoPlayer>();
            if (videoPlayer == null) videoPlayer = gameObject.AddComponent<VideoPlayer>();
        }

        if (rawImage == null || cutsceneClip == null)
        {
            Debug.LogError("[EndingCutscenePlayer] Missing references! RawImage: " + (rawImage != null) + ", Clip: " + (cutsceneClip != null));
            ShowEndingUI();
            return;
        }

        // Hide the ending UI elements initially
        if (endingPopup != null) endingPopup.SetActive(false);
        if (tipCard != null) tipCard.SetActive(false);
        if (overlay != null) overlay.SetActive(false);
        if (background != null) background.SetActive(false);
        
        rawImage.gameObject.SetActive(true);
        rawImage.color = Color.black;

        int width = (int)cutsceneClip.width;
        int height = (int)cutsceneClip.height;
        if (width <= 0 || height <= 0) { width = 1920; height = 1080; }

        // Use APIOnly render mode: the VideoPlayer decodes into its OWN internal
        // texture (videoPlayer.texture), which we copy onto the RawImage every frame
        // in Update(). This is the most robust mode and fixes the "only the first
        // frame shows / picture frozen" problem that RenderTexture mode causes on
        // some GPUs and codecs (exactly the symptom seen in the build).
        videoPlayer.source = VideoSource.VideoClip;
        videoPlayer.clip = cutsceneClip;
        videoPlayer.renderMode = VideoRenderMode.APIOnly;

        // Drive playback from the unscaled game clock so the video keeps advancing
        // even if Time.timeScale is changed, and never freezes on the first frame.
        videoPlayer.playOnAwake = false;
        videoPlayer.waitForFirstFrame = true;
        videoPlayer.skipOnDrop = false; // never skip frames; show every frame
        videoPlayer.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
        videoPlayer.playbackSpeed = 1f;

        // Sync aspect ratio
        AspectRatioFitter aspectFitter = rawImage.GetComponent<AspectRatioFitter>();
        if (aspectFitter == null) aspectFitter = rawImage.gameObject.AddComponent<AspectRatioFitter>();
        aspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        aspectFitter.aspectRatio = (float)width / height;

        // Subscribe to events
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.errorReceived += OnVideoError;
        videoPlayer.prepareCompleted += OnVideoPrepared;

        Debug.Log($"[EndingCutscenePlayer] Starting preparation for {cutsceneClip.name} ({width}x{height})");
        isPreparing = true;
        videoPlayer.Prepare();

        // Generous safety timeout for preparation so slow machines / large videos
        // are never cut off prematurely (was 5s, which could skip the cutscene).
        StartCoroutine(PrepareTimeout(20.0f));
    }

    private IEnumerator PrepareTimeout(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (isPreparing && !isFinished)
        {
            Debug.LogWarning("[EndingCutscenePlayer] Video preparation timed out after " + delay + "s. Skipping cutscene.");
            FinishCutscene();
        }
    }

    private void OnVideoPrepared(VideoPlayer vp)
    {
        if (isFinished) return;
        isPreparing = false;
        Debug.Log("[EndingCutscenePlayer] Video prepared successfully. Starting playback.");
        rawImage.color = Color.white;
        if (vp.texture != null) rawImage.texture = vp.texture;
        vp.Play();

        // Hard safety net: guarantee the cutscene ends even if loopPointReached
        // never fires (some platforms/codecs miss the end event). Ends after the
        // clip's real length plus a small buffer.
        float maxDuration = (cutsceneClip != null ? (float)cutsceneClip.length : 6f) + 2f;
        StartCoroutine(PlaybackWatchdog(maxDuration));
    }

    private IEnumerator PlaybackWatchdog(float maxDuration)
    {
        float t = 0f;
        while (!isFinished && t < maxDuration)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (!isFinished)
        {
            Debug.Log("[EndingCutscenePlayer] Playback watchdog reached end (" + maxDuration.ToString("F1") + "s). Finishing cutscene.");
            FinishCutscene();
        }
    }

    private void OnVideoError(VideoPlayer vp, string message)
    {
        Debug.LogError($"[EndingCutscenePlayer] Video Error: {message}");
        FinishCutscene();
    }

    private void Update()
    {
        if (isFinished) return;

        // Continuously copy the VideoPlayer's freshly decoded frame onto the RawImage
        // (APIOnly mode). This is what makes the picture actually advance instead of
        // being stuck on the first frame.
        if (videoPlayer != null && videoPlayer.texture != null && rawImage != null)
        {
            if (rawImage.texture != videoPlayer.texture)
                rawImage.texture = videoPlayer.texture;
        }

        // Skip check
        if (Time.time - startTime > minSkipDelay)
        {
            // Only skip on a deliberate key press (not mouse click), so that
            // clicking back into the game window to refocus it does NOT skip the video.
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Escape))
            {
                Debug.Log("[EndingCutscenePlayer] Cutscene skipped by player input.");
                FinishCutscene();
            }
        }
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        Debug.Log("[EndingCutscenePlayer] Cutscene reached end naturally.");
        FinishCutscene();
    }

    private void FinishCutscene()
    {
        if (isFinished) return;
        isFinished = true;
        isPreparing = false;

        Debug.Log("[EndingCutscenePlayer] FinishCutscene called.");

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;
            videoPlayer.errorReceived -= OnVideoError;
            videoPlayer.prepareCompleted -= OnVideoPrepared;
            videoPlayer.Stop();
        }

        if (rawImage != null)
        {
            rawImage.texture = null;
            rawImage.gameObject.SetActive(false);
        }

        ShowEndingUI();
    }

    private void ShowEndingUI()
    {
        if (endingPopup != null)
        {
            // Dynamically update actual score and paranoia level before showing the UI
            Transform[] children = endingPopup.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in children)
            {
                if (t.name == "StatRow")
                {
                    Transform labelTrans = t.Find("Label");
                    Transform valueTrans = t.Find("Value");
                    if (labelTrans != null && valueTrans != null)
                    {
                        TMPro.TMP_Text labelTextComp = labelTrans.GetComponent<TMPro.TMP_Text>();
                        TMPro.TMP_Text valueTextComp = valueTrans.GetComponent<TMPro.TMP_Text>();
                        if (labelTextComp != null && valueTextComp != null)
                        {
                            string labelText = labelTextComp.text;
                            if (labelText.Contains("Score") || labelText.Contains("score") || labelText.Contains("Final"))
                            {
                                int actualPoints = GlobalCanvasPersistent.Instance != null ? GlobalCanvasPersistent.Instance.Points : 0;
                                valueTextComp.text = actualPoints.ToString();
                                Debug.Log($"[EndingCutscenePlayer] Updated Final Score text to: {actualPoints}");
                            }
                            else if (labelText.Contains("Paranoia") || labelText.Contains("paranoia"))
                            {
                                int actualParanoia = GlobalCanvasPersistent.Instance != null ? GlobalCanvasPersistent.Instance.Paranoia : 0;
                                valueTextComp.text = actualParanoia + "%";
                                Debug.Log($"[EndingCutscenePlayer] Updated Paranoia Level text to: {actualParanoia}%");
                            }
                        }
                    }
                }
            }

            endingPopup.SetActive(true);
        }
        if (tipCard != null) tipCard.SetActive(true);
        if (overlay != null) overlay.SetActive(true);
        if (background != null) background.SetActive(true);
    }
}
