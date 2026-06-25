using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class MouseSettingsUiBuilder
{
    private const string BlockName = "MouseSettingsBlock";
    private const string SensitivitySliderName = "MouseSensitivitySlider";
    private const string SensitivityValueName = "MouseSensitivityValue";
    private const string InvertHorizontalToggleName = "InvertHorizontalToggle";
    private const string InvertVerticalToggleName = "InvertVerticalToggle";

    private static readonly Color LabelColor = new Color(0.7f, 0.75f, 0.8f, 1f);
    private static readonly Color ValueColor = Color.white;
    private static readonly Color AccentColor = new Color(0f, 0.82f, 0.97f, 1f);
    private static readonly Color DarkControlColor = new Color(0.04f, 0.05f, 0.07f, 1f);

    public static void EnsureStartMenu(GameObject settingsPanel, Slider sliderTemplate, TMP_Text textTemplate)
    {
        Ensure(settingsPanel, sliderTemplate, textTemplate, new Vector2(0f, 0.5f), new Vector2(150f, -55f), new Vector2(540f, 160f));
    }

    public static void EnsurePauseMenu(GameObject settingsPanel, Slider sliderTemplate, TMP_Text textTemplate)
    {
        Ensure(settingsPanel, sliderTemplate, textTemplate, new Vector2(0.5f, 0.5f), new Vector2(0f, -95f), new Vector2(540f, 160f));
    }

    private static void Ensure(GameObject settingsPanel, Slider sliderTemplate, TMP_Text textTemplate, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
    {
        if (settingsPanel == null)
        {
            return;
        }

        Transform existing = settingsPanel.transform.Find(BlockName);
        if (existing != null)
        {
            SyncExisting(existing);
            return;
        }

        GameObject block = new GameObject(BlockName, typeof(RectTransform));
        block.transform.SetParent(settingsPanel.transform, false);

        RectTransform rect = block.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor.x <= 0.01f ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        CreateText(block.transform, "MouseSensitivityLabel", "MOUSE SENSITIVITY", textTemplate, 18f, LabelColor, FontStyles.Bold, TextAlignmentOptions.Left, new Vector2(0f, 58f), new Vector2(400f, 30f));

        Slider sensitivitySlider = CreateSlider(block.transform, sliderTemplate, new Vector2(0f, 22f), new Vector2(400f, 30f));
        TMP_Text sensitivityValue = CreateText(block.transform, SensitivityValueName, MouseLookSettings.FormatSensitivity(MouseLookSettings.Sensitivity), textTemplate, 18f, ValueColor, FontStyles.Bold, TextAlignmentOptions.Right, new Vector2(420f, 22f), new Vector2(90f, 30f));

        sensitivitySlider.minValue = MouseLookSettings.MinSensitivity;
        sensitivitySlider.maxValue = MouseLookSettings.MaxSensitivity;
        sensitivitySlider.wholeNumbers = false;
        sensitivitySlider.SetValueWithoutNotify(MouseLookSettings.Sensitivity);
        sensitivitySlider.onValueChanged.RemoveAllListeners();
        sensitivitySlider.onValueChanged.AddListener(value =>
        {
            MouseLookSettings.SetSensitivity(value);
            sensitivityValue.text = MouseLookSettings.FormatSensitivity(value);
        });

        CreateToggle(block.transform, InvertHorizontalToggleName, "Invert horizontal", MouseLookSettings.InvertHorizontal, MouseLookSettings.SetInvertHorizontal, textTemplate, new Vector2(0f, -35f));
        CreateToggle(block.transform, InvertVerticalToggleName, "Invert vertical", MouseLookSettings.InvertVertical, MouseLookSettings.SetInvertVertical, textTemplate, new Vector2(270f, -35f));
    }

    private static Slider CreateSlider(Transform parent, Slider template, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = template != null
            ? Object.Instantiate(template.gameObject, parent, false)
            : CreateFallbackSlider(parent);

        go.name = SensitivitySliderName;

        RectTransform rect = GetOrAddRectTransform(go);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Slider slider = go.GetComponent<Slider>();
        if (slider == null)
        {
            slider = go.AddComponent<Slider>();
        }

        slider.onValueChanged.RemoveAllListeners();
        slider.interactable = true;
        return slider;
    }

    private static GameObject CreateFallbackSlider(Transform parent)
    {
        GameObject root = new GameObject(SensitivitySliderName, typeof(RectTransform), typeof(Slider));
        root.transform.SetParent(parent, false);

        Image background = CreateImage(root.transform, "Background", new Color(0.11f, 0.13f, 0.17f, 1f));
        Stretch(background.rectTransform, Vector2.zero, Vector2.zero);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(root.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect, new Vector2(5f, 0f), new Vector2(-5f, 0f));

        Image fill = CreateImage(fillArea.transform, "Fill", AccentColor);
        Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(root.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        Stretch(handleAreaRect, new Vector2(5f, 0f), new Vector2(-5f, 0f));

        Image handle = CreateImage(handleArea.transform, "Handle", Color.white);
        RectTransform handleRect = handle.rectTransform;
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(22f, 28f);

        Slider slider = root.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;

        return root;
    }

    private static TMP_Text CreateText(Transform parent, string name, string text, TMP_Text template, float fontSize, Color color, FontStyles fontStyle, TextAlignmentOptions alignment, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = template != null
            ? Object.Instantiate(template.gameObject, parent, false)
            : new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));

        go.name = name;

        TMP_Text tmp = go.GetComponent<TMP_Text>();
        if (tmp == null)
        {
            tmp = go.AddComponent<TextMeshProUGUI>();
        }

        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.fontStyle = fontStyle;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.raycastTarget = false;

        RectTransform rect = GetOrAddRectTransform(go);
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        return tmp;
    }

    private static Toggle CreateToggle(Transform parent, string name, string label, bool initialValue, UnityEngine.Events.UnityAction<bool> onChanged, TMP_Text textTemplate, Vector2 anchoredPosition)
    {
        GameObject root = new GameObject(name, typeof(RectTransform), typeof(Toggle));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0f, 0.5f);
        rootRect.anchorMax = new Vector2(0f, 0.5f);
        rootRect.pivot = new Vector2(0f, 0.5f);
        rootRect.anchoredPosition = anchoredPosition;
        rootRect.sizeDelta = new Vector2(250f, 34f);

        Image box = CreateImage(root.transform, "Box", DarkControlColor);
        RectTransform boxRect = box.rectTransform;
        boxRect.anchorMin = new Vector2(0f, 0.5f);
        boxRect.anchorMax = new Vector2(0f, 0.5f);
        boxRect.pivot = new Vector2(0f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = new Vector2(26f, 26f);

        TMP_Text check = CreateText(box.transform, "Check", "X", textTemplate, 17f, AccentColor, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, new Vector2(26f, 26f));
        RectTransform checkRect = check.rectTransform;
        checkRect.anchorMin = Vector2.zero;
        checkRect.anchorMax = Vector2.one;
        checkRect.pivot = new Vector2(0.5f, 0.5f);
        checkRect.offsetMin = Vector2.zero;
        checkRect.offsetMax = Vector2.zero;

        CreateText(root.transform, "Label", label, textTemplate, 16f, ValueColor, FontStyles.Normal, TextAlignmentOptions.Left, new Vector2(36f, 0f), new Vector2(210f, 30f));

        Toggle toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = box;
        toggle.graphic = check;
        toggle.SetIsOnWithoutNotify(initialValue);
        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(onChanged);

        return toggle;
    }

    private static void SyncExisting(Transform block)
    {
        Slider slider = FindChildComponent<Slider>(block, SensitivitySliderName);
        if (slider != null)
        {
            slider.SetValueWithoutNotify(MouseLookSettings.Sensitivity);
        }

        TMP_Text valueText = FindChildComponent<TMP_Text>(block, SensitivityValueName);
        if (valueText != null)
        {
            valueText.text = MouseLookSettings.FormatSensitivity(MouseLookSettings.Sensitivity);
        }

        Toggle invertHorizontal = FindChildComponent<Toggle>(block, InvertHorizontalToggleName);
        if (invertHorizontal != null)
        {
            invertHorizontal.SetIsOnWithoutNotify(MouseLookSettings.InvertHorizontal);
        }

        Toggle invertVertical = FindChildComponent<Toggle>(block, InvertVerticalToggleName);
        if (invertVertical != null)
        {
            invertVertical.SetIsOnWithoutNotify(MouseLookSettings.InvertVertical);
        }
    }

    private static T FindChildComponent<T>(Transform parent, string childName) where T : Component
    {
        Transform child = parent.Find(childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static RectTransform GetOrAddRectTransform(GameObject go)
    {
        RectTransform rect = go.GetComponent<RectTransform>();
        if (rect == null)
        {
            rect = go.AddComponent<RectTransform>();
        }

        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }
}
