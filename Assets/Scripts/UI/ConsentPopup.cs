using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Вікно згоди на персоналізовану рекламу.
// Показується при першому запуску (з головного меню) і відкривається повторно кнопкою "Privacy".
// Кнопки "Accept" і "Decline" однакові за розміром і помітністю: за GDPR відмова має бути такою ж простою, як згода.
// Відмова не вимикає рекламу, а лише робить її неперсоналізованою, тож гра лишається безкоштовною в обох випадках.
public class ConsentPopup : MonoBehaviour
{
    private GameObject root;
    private TMP_FontAsset font;
    private Action onClosed;

    private static readonly Color ButtonColor = new Color(0.16f, 0.45f, 0.25f, 1f);
    private static readonly Color LinkColor = new Color(0f, 0f, 0f, 0f);

    public static ConsentPopup Create(Transform parent, TMP_FontAsset font)
    {
        var go = new GameObject("ConsentPopup");
        go.transform.SetParent(parent, false);
        var popup = go.AddComponent<ConsentPopup>();
        popup.font = font;
        popup.BuildUI();
        popup.root.SetActive(false);
        return popup;
    }

    public void Show(Action closed = null)
    {
        onClosed = closed;
        root.SetActive(true);
    }

    private void Answer(bool granted)
    {
        Consent.Set(granted);
        root.SetActive(false);
        onClosed?.Invoke();
    }

    private void BuildUI()
    {
        // Найвищий шар: поки гравець не відповів, решта меню недоступна.
        RectTransform canvas = UiFactory.CreateCanvas("ConsentCanvas", transform, 50);
        RectTransform overlay = UiFactory.CreateOverlay("Consent", canvas, new Color(0f, 0f, 0f, 0.85f));
        root = overlay.gameObject;

        RectTransform panel = UiFactory.CreateRect("Panel", overlay);
        panel.sizeDelta = new Vector2(940f, 1100f);
        UiFactory.Anchor(panel, new Vector2(0.5f, 0.5f), Vector2.zero);
        panel.gameObject.AddComponent<Image>().color = new Color(0.12f, 0.16f, 0.2f, 1f);

        RectTransform title = UiFactory.CreateRect("Title", panel);
        title.sizeDelta = new Vector2(860f, 110f);
        UiFactory.Anchor(title, new Vector2(0.5f, 1f), new Vector2(0f, -40f));
        UiFactory.CreateText(title, font, 62f).text = "Your privacy";

        RectTransform body = UiFactory.CreateRect("Body", panel);
        body.sizeDelta = new Vector2(840f, 560f);
        UiFactory.Anchor(body, new Vector2(0.5f, 1f), new Vector2(0f, -170f));
        TextMeshProUGUI text = UiFactory.CreateText(body, font, 36f);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.text =
            "This game is free thanks to ads.\n\n" +
            "With your permission, our ad partners can use your device's advertising ID " +
            "and similar data to show ads that are more relevant to you.\n\n" +
            "If you decline, you will still see ads, but they will not be personalized.\n\n" +
            "You can change your choice at any time with the Privacy button in the main menu.";

        float y = -760f;
        string url = GameConfig.Instance != null ? GameConfig.Instance.privacyPolicyUrl : "";
        if (!string.IsNullOrEmpty(url))
        {
            Button link = UiFactory.CreateButton("PrivacyPolicy", panel, new Vector2(600f, 70f), LinkColor,
                                                 () => Application.OpenURL(url), font, 34f, out _, out TMP_Text linkText);
            linkText.text = "<u>Privacy Policy</u>";
            linkText.color = new Color(0.55f, 0.75f, 1f, 1f);
            UiFactory.Anchor((RectTransform)link.transform, new Vector2(0.5f, 1f), new Vector2(0f, y));
        }

        // Дві однакові кнопки поруч.
        Vector2 size = new Vector2(400f, 140f);
        Button accept = UiFactory.CreateButton("Accept", panel, size, ButtonColor, () => Answer(true),
                                               font, 46f, out _, out TMP_Text acceptText);
        acceptText.text = "Accept";
        UiFactory.Anchor((RectTransform)accept.transform, new Vector2(0.5f, 0f), new Vector2(-215f, 50f));

        Button decline = UiFactory.CreateButton("Decline", panel, size, ButtonColor, () => Answer(false),
                                                font, 46f, out _, out TMP_Text declineText);
        declineText.text = "Decline";
        UiFactory.Anchor((RectTransform)decline.transform, new Vector2(0.5f, 0f), new Vector2(215f, 50f));
    }
}
