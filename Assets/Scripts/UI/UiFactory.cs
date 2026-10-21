using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Спільні помічники для інтерфейсу, який будується з коду (меню будівництва, HUD).
// Усі розміри задаються в пікселях при роздільності 1080x1920, CanvasScaler масштабує їх під екран.
public static class UiFactory
{
    public static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

    public static RectTransform CreateCanvas(string name, Transform parent, int sortingOrder)
    {
        var canvasObject = new GameObject(name,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f; // однаковий вигляд і в портретній, і в альбомній орієнтації

        return canvasObject.GetComponent<RectTransform>();
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    // Прив'язує елемент до точки екрана: (0,0) — лівий низ, (1,1) — правий верх, (0.5,1) — центр зверху.
    public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 offset)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = offset;
    }

    public static TextMeshProUGUI CreateText(RectTransform rect, TMP_FontAsset font, float fontSize)
    {
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // Зірки малюються ромбами з простих Image: шрифт може не мати символу ★, а окремий спрайт не потрібен.
    public static Image[] CreateStars(Transform parent, float size, float spacing, Vector2 offset)
    {
        var stars = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            RectTransform rect = CreateRect("Star" + (i + 1), parent);
            rect.sizeDelta = Vector2.one * size;
            Anchor(rect, new Vector2(0.5f, 0.5f), offset + new Vector2((i - 1) * (size + spacing), 0f));
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            stars[i] = rect.gameObject.AddComponent<Image>();
            stars[i].raycastTarget = false;
        }
        return stars;
    }

    public static readonly Color StarOn = new Color(1f, 0.82f, 0.2f, 1f);
    public static readonly Color StarOff = new Color(1f, 1f, 1f, 0.2f);

    public static void SetStars(Image[] stars, int count)
    {
        for (int i = 0; i < stars.Length; i++)
            stars[i].color = i < count ? StarOn : StarOff;
    }

    // Затемнення на весь екран, яке блокує тапи по грі під ним.
    public static RectTransform CreateOverlay(string name, Transform parent, Color color)
    {
        RectTransform overlay = CreateRect(name, parent);
        Stretch(overlay);
        overlay.gameObject.AddComponent<Image>().color = color;
        return overlay;
    }

    public static Button CreateButton(string name, Transform parent, Vector2 size, Color color,
                                      UnityAction onClick, TMP_FontAsset font, float fontSize,
                                      out Image image, out TMP_Text text)
    {
        RectTransform rect = CreateRect(name, parent);
        rect.sizeDelta = size;

        image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        if (onClick != null) button.onClick.AddListener(onClick);

        RectTransform textRect = CreateRect("Label", rect);
        Stretch(textRect);
        text = CreateText(textRect, font, fontSize);
        return button;
    }
}
