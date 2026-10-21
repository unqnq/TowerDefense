using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Вибір бустер-карти на початку рівня: 1 з 3 випадкових карт, один бонус на рівень.
//  - Якщо попередній рівень пройдено на 3 зірки — карта безкоштовна.
//  - Інакше карту можна взяти за перегляд rewarded-реклами.
//  - Можна пропустити.
// Поки вікно відкрите, гра заморожена (GameManager.Hold), тож відлік до першої хвилі стоїть.
// Ефект діє Duration секунд від моменту вибору.
public class BoostPicker : MonoBehaviour
{
    private GameManager gameManager;
    private Bank bank;
    private TMP_FontAsset font;

    private GameObject root;
    private TMP_Text subtitleText;
    private readonly List<(BoostCard card, Button button, TMP_Text footer)> cards = new List<(BoostCard, Button, TMP_Text)>();
    private bool free;
    private bool waitingForAd;

    private static readonly Color Dark = new Color(0f, 0f, 0f, 0.6f);

    public void Init(GameManager manager, TMP_FontAsset fontAsset)
    {
        gameManager = manager;
        font = fontAsset;
        bank = FindAnyObjectByType<Bank>();
    }

    private void Start()
    {
        GameConfig config = GameConfig.Instance;
        if (config == null || config.boostCards.Count == 0 || gameManager == null) return;

        free = IsFree();
        bool canWatchAd = gameManager.Ads != null;
        if (!free && !canWatchAd) return; // нічим "заплатити" — вікно не показуємо

        BuildUI(PickCards(config));
        gameManager.Hold(true);
    }

    // Безкоштовно, якщо попередній рівень кампанії пройдено на 3/3. Щоденне випробування — лише за рекламу.
    private static bool IsFree()
    {
        if (LevelSession.IsDaily) return false;
        int index = LevelSession.LevelIndex;
        var levels = GameConfig.Instance.levels;
        return index > 0 && index - 1 < levels.Count && Progress.GetStars(levels[index - 1]) >= 3;
    }

    private static List<BoostCard> PickCards(GameConfig config)
    {
        var pool = new List<BoostCard>(config.boostCards);
        pool.RemoveAll(c => c == null);
        var result = new List<BoostCard>();
        int count = Mathf.Min(config.boostChoices, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }
        return result;
    }

    private void OnCardClicked(BoostCard card)
    {
        if (waitingForAd) return;

        if (free)
        {
            Apply(card);
            return;
        }

        waitingForAd = true;
        gameManager.Ads.ShowRewarded(
            () => { waitingForAd = false; Apply(card); },
            () => { waitingForAd = false; subtitleText.text = "Ad is not ready yet. Try again or skip."; });
    }

    private void Apply(BoostCard card)
    {
        if (card.IsInstant)
        {
            if (bank != null) bank.AddMoney(Mathf.RoundToInt(card.Value));
        }
        else
        {
            GameModifiers.Activate(card);
        }
        Haptics.Medium();
        Close();
    }

    private void Close()
    {
        root.SetActive(false);
        gameManager.Hold(false);
    }

    private void BuildUI(List<BoostCard> choices)
    {
        RectTransform canvas = UiFactory.CreateCanvas("BoostCanvas", transform, 20);
        RectTransform overlay = UiFactory.CreateOverlay("BoostPicker", canvas, new Color(0f, 0f, 0f, 0.75f));
        root = overlay.gameObject;

        RectTransform title = UiFactory.CreateRect("Title", overlay);
        title.sizeDelta = new Vector2(1000f, 120f);
        UiFactory.Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(0f, 520f));
        UiFactory.CreateText(title, font, 80f).text = "Choose a boost";

        RectTransform subtitle = UiFactory.CreateRect("Subtitle", overlay);
        subtitle.sizeDelta = new Vector2(1000f, 80f);
        UiFactory.Anchor(subtitle, new Vector2(0.5f, 0.5f), new Vector2(0f, 420f));
        subtitleText = UiFactory.CreateText(subtitle, font, 38f);
        subtitleText.text = free ? "Free: you got 3 stars on the previous level!" : "Watch an ad to take one card";

        // Карти стовпчиком: на вузькому портретному екрані так кожна читається повністю.
        Vector2 cardSize = new Vector2(860f, 200f);
        for (int i = 0; i < choices.Count; i++)
        {
            BoostCard card = choices[i];
            Button button = UiFactory.CreateButton(card.name, overlay, cardSize, card.Color,
                                                   () => OnCardClicked(card), font, 46f, out _, out TMP_Text text);
            UiFactory.Anchor((RectTransform)button.transform, new Vector2(0.5f, 0.5f),
                             new Vector2(0f, 230f - i * (cardSize.y + 30f)));

            string duration = card.IsInstant ? "" : $"  ({Mathf.RoundToInt(card.Duration)}s)";
            string footer = free ? "TAP TO TAKE" : "WATCH AD";
            text.text = $"{card.DisplayName}{duration}\n<size=70%>{card.Description}</size>\n<size=60%>{footer}</size>";
            cards.Add((card, button, text));
        }

        Button skip = UiFactory.CreateButton("Skip", overlay, new Vector2(360f, 120f), Dark, Close,
                                             font, 44f, out _, out TMP_Text skipText);
        skipText.text = "Skip";
        UiFactory.Anchor((RectTransform)skip.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -560f));
    }
}
