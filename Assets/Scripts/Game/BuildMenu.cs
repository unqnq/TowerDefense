using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Меню клітинки для тачскріну. Відкривається тапом по клітинці над нею:
//  - вільна клітинка: кнопки веж з цінами, тап по вежі будує її;
//  - зайнята клітинка: назва і рівень вежі, підказка про merge і кнопка "Sell" (скільки повернеться).
//    Покращення робиться не тут, а перетягуванням вежі на таку саму (див. BuildCell).
// Тап будь-де повз панель закриває меню.
// Кнопки веж створюються з коду за списком у TurretSpawner, тож нова вежа з'являється в меню сама.
public class BuildMenu : MonoBehaviour
{
    [SerializeField] private TurretSpawner spawner;
    [SerializeField] private Bank bank;
    [SerializeField] private GameCursor cursor;
    [Tooltip("Необов'язково. Шрифт кнопок. Якщо порожньо, береться шрифт TextMeshPro за замовчуванням.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Вигляд (у пікселях при роздільності 1080x1920)")]
    [SerializeField] private Vector2 buttonSize = new Vector2(220f, 150f);
    [SerializeField] private float spacing = 20f;
    [SerializeField] private float padding = 16f;
    [SerializeField] private float titleHeight = 56f;
    [SerializeField] private float hintHeight = 40f;
    [Tooltip("На скільки панель піднімається над клітинкою.")]
    [SerializeField] private float verticalOffset = 170f;
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color buttonColor = new Color(0.16f, 0.45f, 0.25f, 1f);
    [SerializeField] private Color sellColor = new Color(0.6f, 0.22f, 0.18f, 1f);
    [SerializeField] private Color notEnoughMoneyColor = new Color(0.4f, 0.4f, 0.4f, 1f);

    private class Entry
    {
        public TowerData data;
        public Button button;
        public Image image;
    }

    private readonly List<Entry> buildEntries = new List<Entry>();
    private RectTransform canvasRect;
    private GameObject root;

    private RectTransform buildPanel;
    private RectTransform actionPanel;
    private RectTransform activePanel;

    private TMP_Text titleText;
    private TMP_Text hintText;
    private TMP_Text sellText;

    private BuildCell currentCell;
    private RangeIndicator rangeIndicator;
    private GameManager gameManager;
    private Camera cam;

    public bool IsOpen => currentCell != null;

    private void Awake()
    {
        if (spawner == null) spawner = FindAnyObjectByType<TurretSpawner>();
        if (bank == null) bank = FindAnyObjectByType<Bank>();
        if (cursor == null) cursor = FindAnyObjectByType<GameCursor>();
        gameManager = FindAnyObjectByType<GameManager>();
        cam = Camera.main;

        BuildUI();
        root.SetActive(false);
        rangeIndicator = RangeIndicator.Create(transform);
    }

    private void OnEnable() => GameEvents.StateChanged += OnStateChanged;
    private void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

    // Перемога, поразка або пауза закривають меню.
    private void OnStateChanged(GameState state)
    {
        if (state != GameState.Playing) Close();
    }

    public void Open(BuildCell cell)
    {
        if (gameManager != null && gameManager.State != GameState.Playing) return;
        // Заблокована клітинка (вимкнена в інспекторі, без вежі): меню не потрібне.
        if (!cell.IsFree && cell.PlacedTurret == null) return;

        currentCell = cell;
        if (cursor != null) cursor.MoveTo(cell.transform.position, true);

        bool building = cell.IsFree;
        buildPanel.gameObject.SetActive(building);
        actionPanel.gameObject.SetActive(!building);
        activePanel = building ? buildPanel : actionPanel;

        root.SetActive(true);
        Refresh();
    }

    public void Close()
    {
        currentCell = null;
        if (root != null) root.SetActive(false);
        if (rangeIndicator != null) rangeIndicator.Hide();
    }

    private void LateUpdate()
    {
        if (!IsOpen)
        {
            // Клітинку знищили, поки меню було відкрите.
            if (root.activeSelf) Close();
            return;
        }
        // Гроші можуть змінитися, поки меню відкрите (вбили ворога), тому оновлюємо щокадру.
        Refresh();
    }

    // ---------- Дії ----------

    private void Build(TowerData data)
    {
        if (currentCell == null) return;
        if (currentCell.TryBuild(data)) Close();
    }

    private void Sell()
    {
        if (currentCell == null) return;
        currentCell.Sell();
        Close();
    }

    // ---------- Оновлення ----------

    private void Refresh()
    {
        if (activePanel == buildPanel) RefreshBuild();
        else if (!RefreshActions()) return;
        UpdatePosition();
    }

    private void RefreshBuild()
    {
        foreach (Entry entry in buildEntries)
        {
            bool canAfford = bank.CanAfford(entry.data.Cost);
            entry.button.interactable = canAfford;
            entry.image.color = canAfford ? buttonColor : notEnoughMoneyColor;
        }
    }

    private bool RefreshActions()
    {
        Turret turret = currentCell.PlacedTurret;
        if (turret == null)
        {
            Close();
            return false;
        }

        titleText.text = $"{turret.Data.DisplayName}  Lv {turret.Level}";
        rangeIndicator.Show(turret.transform.position, turret.Range); // кожен кадр: бустер дальності може закінчитись
        hintText.text = turret.Data.NextLevel != null
            ? $"Drag onto {turret.Data.DisplayName} Lv {turret.Level} to merge"
            : "MAX level";

        sellText.text = $"Sell\n<size=120%>+{spawner.GetSellPrice(turret)}</size>";
        return true;
    }

    // Ставить панель над клітинкою і не дає їй вилізти за край екрана.
    // Якщо зверху не вміщається, панель опускається під клітинку.
    private void UpdatePosition()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 screen = cam.WorldToScreenPoint(currentCell.transform.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 cellPos);

        Vector2 half = canvasRect.rect.size * 0.5f;
        Vector2 panelHalf = activePanel.rect.size * 0.5f;

        Vector2 pos = cellPos + Vector2.up * verticalOffset;
        if (pos.y + panelHalf.y > half.y)
            pos = cellPos - Vector2.up * verticalOffset;

        pos.x = Mathf.Clamp(pos.x, -half.x + panelHalf.x, half.x - panelHalf.x);
        pos.y = Mathf.Clamp(pos.y, -half.y + panelHalf.y, half.y - panelHalf.y);
        activePanel.anchoredPosition = pos;
    }

    // ---------- Створення інтерфейсу ----------

    private void BuildUI()
    {
        // Власний Canvas, щоб меню не залежало від розмітки основного UI.
        // Над ігровим полем і HUD; панелі перемоги/поразки/паузи закривають меню через подію.
        canvasRect = UiFactory.CreateCanvas("BuildMenuCanvas", transform, 10);

        root = CreateRect("Root", canvasRect).gameObject;
        Stretch((RectTransform)root.transform);

        // Прозорий блокер на весь екран: тап повз панель закриває меню
        // і заодно не дає цьому ж тапу випадково спрацювати на іншій клітинці.
        RectTransform blocker = CreateRect("Blocker", root.transform);
        Stretch(blocker);
        Image blockerImage = blocker.gameObject.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0f);
        Button blockerButton = blocker.gameObject.AddComponent<Button>();
        blockerButton.transition = Selectable.Transition.None;
        blockerButton.onClick.AddListener(Close);

        BuildBuildPanel();
        BuildActionPanel();
    }

    private void BuildBuildPanel()
    {
        int count = spawner != null ? spawner.Towers.Count : 0;
        buildPanel = CreatePanel("BuildPanel", count, 0f);

        for (int i = 0; i < count; i++)
        {
            TowerData data = spawner.Towers[i];
            if (data == null) continue;

            Button button = CreateButton(data.name, buildPanel, ButtonPosition(count, i, 0f),
                                         () => Build(data), out Image image, out TMP_Text text);

            // З іконкою: іконка зверху, ціна знизу. Без іконки: назва і ціна текстом.
            if (data.Icon != null)
            {
                RectTransform iconRect = CreateRect("Icon", button.transform);
                iconRect.anchorMin = new Vector2(0.15f, 0.35f);
                iconRect.anchorMax = new Vector2(0.85f, 0.95f);
                iconRect.offsetMin = iconRect.offsetMax = Vector2.zero;
                Image icon = iconRect.gameObject.AddComponent<Image>();
                icon.sprite = data.Icon;
                icon.preserveAspect = true;
                icon.raycastTarget = false;

                RectTransform textRect = text.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = new Vector2(1f, 0.35f);
                text.text = data.Cost.ToString();
            }
            else
            {
                text.text = $"{data.DisplayName}\n<size=120%>{data.Cost}</size>";
            }

            buildEntries.Add(new Entry { data = data, button = button, image = image });
        }
    }

    private void BuildActionPanel()
    {
        // Ширина як на дві кнопки, щоб уміщалася підказка; кнопка продажу по центру.
        float top = titleHeight + hintHeight;
        actionPanel = CreatePanel("ActionPanel", 2, top);

        // Заголовок: назва і рівень вежі. Під ним підказка, як покращити.
        titleText = CreateText(CreateTopRow("Title", titleHeight, padding * 0.5f));
        hintText = CreateText(CreateTopRow("Hint", hintHeight, padding * 0.5f + titleHeight));
        hintText.fontSize = 26f;
        hintText.color = new Color(1f, 1f, 1f, 0.8f);

        CreateButton("Sell", actionPanel, new Vector2(0f, -top * 0.5f),
                     Sell, out Image sellImage, out sellText);
        sellImage.color = sellColor;
    }

    private RectTransform CreateTopRow(string name, float height, float offsetFromTop)
    {
        RectTransform row = CreateRect(name, actionPanel);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = new Vector2(1f, 1f);
        row.pivot = new Vector2(0.5f, 1f);
        row.sizeDelta = new Vector2(-padding * 2f, height);
        row.anchoredPosition = new Vector2(0f, -offsetFromTop);
        return row;
    }

    private RectTransform CreatePanel(string name, int buttonCount, float extraHeight)
    {
        RectTransform panel = CreateRect(name, root.transform);
        panel.gameObject.AddComponent<Image>().color = panelColor;
        panel.sizeDelta = new Vector2(PanelWidth(buttonCount), buttonSize.y + padding * 2f + extraHeight);
        return panel;
    }

    private float PanelWidth(int buttonCount) =>
        buttonCount * buttonSize.x + Mathf.Max(0, buttonCount - 1) * spacing + padding * 2f;

    private Vector2 ButtonPosition(int buttonCount, int index, float y)
    {
        float width = PanelWidth(buttonCount);
        float x = -width * 0.5f + padding + buttonSize.x * 0.5f + index * (buttonSize.x + spacing);
        return new Vector2(x, y);
    }

    private Button CreateButton(string name, RectTransform parent, Vector2 position, UnityAction onClick,
                                out Image image, out TMP_Text text)
    {
        Button button = UiFactory.CreateButton(name, parent, buttonSize, buttonColor, onClick,
                                               font, 36f, out image, out text);
        ((RectTransform)button.transform).anchoredPosition = position;
        return button;
    }

    private TMP_Text CreateText(RectTransform rect) => UiFactory.CreateText(rect, font, 36f);

    private static RectTransform CreateRect(string name, Transform parent) => UiFactory.CreateRect(name, parent);

    private static void Stretch(RectTransform rect) => UiFactory.Stretch(rect);
}
