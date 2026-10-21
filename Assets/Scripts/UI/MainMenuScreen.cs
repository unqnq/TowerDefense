using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Головне меню (сцена MainMenu). Усе будується з коду за GameConfig:
//  - карта рівнів: кнопка на кожен рівень із зірками, закриті рівні сірі;
//  - щоденне випробування: сьогоднішній рівень і модифікатор, нагорода, таймер до наступного;
//  - дерево покращень за зірки.
public class MainMenuScreen : MonoBehaviour
{
    [Tooltip("Необов'язково. Шрифт меню. Якщо порожньо, береться шрифт TextMeshPro за замовчуванням.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Вигляд (у пікселях при 1080x1920)")]
    [SerializeField] private Color backgroundColor = new Color(0.12f, 0.16f, 0.2f, 1f);
    [SerializeField] private Color levelColor = new Color(0.16f, 0.45f, 0.25f, 1f);
    [SerializeField] private Color lockedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    [SerializeField] private Color dailyColor = new Color(0.55f, 0.3f, 0.7f, 1f);
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.4f);
    [SerializeField] private int columns = 3;

    private GameConfig config;
    private ConsentPopup consentPopup;
    private TMP_Text starsText;
    private TMP_Text dailyText;
    private GameObject levelsPage;
    private GameObject upgradesPage;
    private readonly List<(MetaUpgrade upgrade, Button button, Image image, TMP_Text text)> upgradeRows =
        new List<(MetaUpgrade, Button, Image, TMP_Text)>();

    private void Awake()
    {
        Time.timeScale = 1f; // на випадок виходу з рівня на паузі
        config = GameConfig.Instance;
        if (config == null)
        {
            Debug.LogError("[MainMenuScreen] Не знайдено Assets/Resources/GameConfig.");
            return;
        }
        BuildUI();
        ShowLevels();

        // Перший запуск: питаємо згоду до того, як гравець зайде в рівень (де ініціалізується реклама).
        consentPopup = ConsentPopup.Create(transform, font);
        if (!Consent.HasAnswered) consentPopup.Show();
    }

    private void Update()
    {
        if (config == null) return;
        starsText.text = $"Stars: {Progress.AvailableStars}";
        UpdateDailyText();
    }

    private void ShowLevels()
    {
        levelsPage.SetActive(true);
        upgradesPage.SetActive(false);
    }

    private void ShowUpgrades()
    {
        levelsPage.SetActive(false);
        upgradesPage.SetActive(true);
        RefreshUpgrades();
    }

    // ---------- Щоденне випробування ----------

    private void UpdateDailyText()
    {
        if (dailyText == null) return;
        if (!DailyChallenge.IsAvailable)
        {
            dailyText.text = "Daily challenge\n<size=60%>not configured</size>";
            return;
        }

        int day = DailyChallenge.Today;
        DailyModifier modifier = DailyChallenge.Modifier(day);
        LevelData level = config.levels[DailyChallenge.LevelIndex(day)];
        var left = DailyChallenge.TimeUntilNext;
        string status = Progress.IsDailyDone(day)
            ? $"Done! Next in {left.Hours}h {left.Minutes}m"
            : $"Reward: +{config.dailyStarReward} stars";
        dailyText.text = $"Daily: {modifier.displayName}\n<size=60%>{level.DisplayName} - {modifier.description}\n{status}</size>";
    }

    // ---------- Покращення ----------

    private void RefreshUpgrades()
    {
        foreach (var row in upgradeRows)
        {
            int rank = Progress.GetRank(row.upgrade);
            bool maxed = rank >= row.upgrade.maxRank;
            bool canBuy = Progress.CanBuy(row.upgrade);
            string value = FormatValue(row.upgrade);

            row.text.text = $"{row.upgrade.displayName}  {rank}/{row.upgrade.maxRank}\n" +
                            $"<size=65%>{string.Format(row.upgrade.description, value)} per level</size>\n" +
                            $"<size=70%>{(maxed ? "MAX" : $"Buy: {row.upgrade.costPerRank} stars")}</size>";
            row.button.interactable = canBuy;
            row.image.color = canBuy ? levelColor : lockedColor;
        }
    }

    private static string FormatValue(MetaUpgrade upgrade) =>
        upgrade.type == UpgradeType.StartMoney
            ? Mathf.RoundToInt(upgrade.valuePerRank).ToString()
            : Mathf.RoundToInt(upgrade.valuePerRank * 100f) + "%";

    private void Buy(MetaUpgrade upgrade)
    {
        if (Progress.TryBuy(upgrade)) Haptics.Light();
        RefreshUpgrades();
    }

    // ---------- Створення інтерфейсу ----------

    private void BuildUI()
    {
        RectTransform canvas = UiFactory.CreateCanvas("MenuCanvas", transform, 0);
        RectTransform background = UiFactory.CreateOverlay("Background", canvas, backgroundColor);

        RectTransform title = UiFactory.CreateRect("Title", background);
        title.sizeDelta = new Vector2(1000f, 160f);
        UiFactory.Anchor(title, new Vector2(0.5f, 1f), new Vector2(0f, -100f));
        UiFactory.CreateText(title, font, 100f).text = "TOWER DEFENSE";

        RectTransform stars = UiFactory.CreateRect("Stars", background);
        stars.sizeDelta = new Vector2(600f, 80f);
        UiFactory.Anchor(stars, new Vector2(0.5f, 1f), new Vector2(0f, -270f));
        starsText = UiFactory.CreateText(stars, font, 52f);
        starsText.color = UiFactory.StarOn;

        levelsPage = UiFactory.CreateRect("LevelsPage", background).gameObject;
        UiFactory.Stretch((RectTransform)levelsPage.transform);
        BuildLevelsPage((RectTransform)levelsPage.transform);

        upgradesPage = UiFactory.CreateRect("UpgradesPage", background).gameObject;
        UiFactory.Stretch((RectTransform)upgradesPage.transform);
        BuildUpgradesPage((RectTransform)upgradesPage.transform);
    }

    private void BuildLevelsPage(RectTransform page)
    {
        Vector2 size = new Vector2(280f, 260f);
        float spacing = 40f;
        float startX = -(columns - 1) * (size.x + spacing) * 0.5f;

        for (int i = 0; i < config.levels.Count; i++)
        {
            int index = i;
            LevelData level = config.levels[i];
            bool unlocked = Progress.IsUnlocked(i);

            Button button = UiFactory.CreateButton(level.name, page, size, unlocked ? levelColor : lockedColor,
                                                   () => LevelSession.PlayLevel(index), font, 70f,
                                                   out _, out TMP_Text text);
            button.interactable = unlocked;
            var rect = (RectTransform)button.transform;
            int row = i / columns, col = i % columns;
            UiFactory.Anchor(rect, new Vector2(0.5f, 1f),
                             new Vector2(startX + col * (size.x + spacing), -420f - row * (size.y + spacing)));

            text.text = unlocked ? (i + 1).ToString() : "?";
            text.rectTransform.offsetMin = new Vector2(0f, 60f); // місце під зірки

            Image[] starImages = UiFactory.CreateStars(rect, 44f, 22f, new Vector2(0f, -size.y * 0.5f + 55f));
            UiFactory.SetStars(starImages, Progress.GetStars(level));
        }

        int rows = Mathf.CeilToInt(config.levels.Count / (float)columns);
        float bottomOfGrid = -420f - rows * (size.y + spacing);

        Button daily = UiFactory.CreateButton("Daily", page, new Vector2(920f, 240f), dailyColor,
                                              () => LevelSession.PlayDaily(), font, 54f, out _, out dailyText);
        daily.interactable = DailyChallenge.IsAvailable;
        UiFactory.Anchor((RectTransform)daily.transform, new Vector2(0.5f, 1f), new Vector2(0f, bottomOfGrid - 20f));

        Button upgrades = UiFactory.CreateButton("Upgrades", page, new Vector2(920f, 160f), panelColor,
                                                 ShowUpgrades, font, 54f, out _, out TMP_Text upgradesText);
        upgradesText.text = "Upgrades";
        UiFactory.Anchor((RectTransform)upgrades.transform, new Vector2(0.5f, 1f), new Vector2(0f, bottomOfGrid - 300f));

        // Змінити рішення щодо реклами можна будь-коли — це вимога GDPR.
        Button privacy = UiFactory.CreateButton("Privacy", page, new Vector2(360f, 110f), panelColor,
                                                () => consentPopup.Show(), font, 40f, out _, out TMP_Text privacyText);
        privacyText.text = "Privacy";
        UiFactory.Anchor((RectTransform)privacy.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f));
    }

    private void BuildUpgradesPage(RectTransform page)
    {
        Vector2 size = new Vector2(920f, 200f);
        for (int i = 0; i < config.upgrades.Count; i++)
        {
            MetaUpgrade upgrade = config.upgrades[i];
            Button button = UiFactory.CreateButton(upgrade.id, page, size, levelColor,
                                                   () => Buy(upgrade), font, 46f, out Image image, out TMP_Text text);
            UiFactory.Anchor((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0f, -400f - i * (size.y + 24f)));
            upgradeRows.Add((upgrade, button, image, text));
        }

        Button back = UiFactory.CreateButton("Back", page, new Vector2(500f, 150f), panelColor,
                                             ShowLevels, font, 54f, out _, out TMP_Text backText);
        backText.text = "Back";
        UiFactory.Anchor((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0f, 80f));
    }
}
