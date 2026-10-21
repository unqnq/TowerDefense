using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Екран результату рівня. Створюється GameHud.
//  - Перемога: зірки, рекорд / нагорода за щоденне, кнопки Next, Restart, Menu.
//  - Поразка: "Second chance" за рекламу (раз за рівень), Restart, Menu.
public class ResultScreen : MonoBehaviour
{
    private GameManager gameManager;
    private TMP_FontAsset font;

    private GameObject root;
    private TMP_Text titleText;
    private TMP_Text infoText;
    private Image[] stars;
    private Button nextButton;
    private Button secondChanceButton;
    private TMP_Text secondChanceText;

    private static readonly Color Accent = new Color(0.16f, 0.45f, 0.25f, 1f);
    private static readonly Color Dark = new Color(0f, 0f, 0f, 0.6f);
    private static readonly Color AdColor = new Color(0.75f, 0.5f, 0.1f, 1f);

    public void Init(GameManager manager, TMP_FontAsset fontAsset)
    {
        gameManager = manager;
        font = fontAsset;
        BuildUI();
        root.SetActive(false);
    }

    private void OnEnable()
    {
        GameEvents.LevelFinished += Show;
        GameEvents.StateChanged += OnStateChanged;
    }

    private void OnDisable()
    {
        GameEvents.LevelFinished -= Show;
        GameEvents.StateChanged -= OnStateChanged;
    }

    // Після другого життя гра знову Playing — ховаємо екран.
    private void OnStateChanged(GameState state)
    {
        if (state == GameState.Playing && root != null) root.SetActive(false);
    }

    private void Show(bool won)
    {
        root.SetActive(true);
        stars[0].transform.parent.gameObject.SetActive(won);

        if (won)
        {
            titleText.text = "VICTORY";
            UiFactory.SetStars(stars, gameManager.Stars);

            if (LevelSession.IsDaily)
                infoText.text = gameManager.DailyReward > 0
                    ? $"Daily challenge complete!  +{gameManager.DailyReward} stars"
                    : "Daily challenge complete! (reward already taken today)";
            else
                infoText.text = gameManager.NewRecord ? "New record!" : "";

            nextButton.gameObject.SetActive(LevelSession.HasNextLevel);
            secondChanceButton.gameObject.SetActive(false);
        }
        else
        {
            titleText.text = "DEFEAT";
            infoText.text = gameManager.CanOfferSecondLife ? "Watch an ad to restore the base and keep playing" : "";
            nextButton.gameObject.SetActive(false);
            secondChanceButton.gameObject.SetActive(gameManager.CanOfferSecondLife);
            secondChanceButton.interactable = true;
            secondChanceText.text = "Second chance\n<size=70%>watch ad</size>";
        }
    }

    private void OnSecondChance()
    {
        secondChanceButton.interactable = false; // захист від подвійного тапу, поки реклама відкривається
        gameManager.RequestSecondLife(() =>
        {
            secondChanceButton.interactable = true;
            secondChanceText.text = "Ad not ready\n<size=70%>try again</size>";
        });
    }

    private void BuildUI()
    {
        RectTransform canvas = UiFactory.CreateCanvas("ResultCanvas", transform, 20);
        RectTransform overlay = UiFactory.CreateOverlay("Result", canvas, new Color(0f, 0f, 0f, 0.7f));
        root = overlay.gameObject;

        RectTransform title = UiFactory.CreateRect("Title", overlay);
        title.sizeDelta = new Vector2(900f, 150f);
        UiFactory.Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(0f, 420f));
        titleText = UiFactory.CreateText(title, font, 110f);

        RectTransform starsRoot = UiFactory.CreateRect("Stars", overlay);
        starsRoot.sizeDelta = new Vector2(600f, 160f);
        UiFactory.Anchor(starsRoot, new Vector2(0.5f, 0.5f), new Vector2(0f, 260f));
        stars = UiFactory.CreateStars(starsRoot, 100f, 60f, Vector2.zero);

        RectTransform info = UiFactory.CreateRect("Info", overlay);
        info.sizeDelta = new Vector2(900f, 90f);
        UiFactory.Anchor(info, new Vector2(0.5f, 0.5f), new Vector2(0f, 140f));
        infoText = UiFactory.CreateText(info, font, 40f);

        Vector2 size = new Vector2(520f, 150f);
        nextButton = AddButton(overlay, "Next level", Accent, () => gameManager.NextLevel(), 0f, size, out _);
        secondChanceButton = AddButton(overlay, "", AdColor, OnSecondChance, 0f, size, out secondChanceText);
        AddButton(overlay, "Restart", Dark, () => gameManager.Restart(), -180f, size, out _);
        AddButton(overlay, "Menu", Dark, () => gameManager.ToMenu(), -360f, size, out _);
    }

    private Button AddButton(RectTransform parent, string label, Color color, UnityEngine.Events.UnityAction onClick,
                             float y, Vector2 size, out TMP_Text text)
    {
        Button button = UiFactory.CreateButton(label, parent, size, color, onClick, font, 50f, out _, out text);
        text.text = label;
        UiFactory.Anchor((RectTransform)button.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, y));
        return button;
    }
}
