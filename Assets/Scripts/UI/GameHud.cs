using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ігровий HUD для телефона. Будується з коду, тож у сцені потрібен лише цей компонент.
//  - справа зверху: пауза і перемикач швидкості x1/x2;
//  - зверху по центру: лічильник хвиль;
//  - знизу по центру: кнопка "Start" / "Next wave" з відліком і бонусом (видно лише між хвилями);
//  - під лічильником: активний бустер і скільки секунд йому лишилось;
//  - екран паузи з кнопками Resume, Restart, Menu і перемикачем вібрації.
// Також створює екран результату (ResultScreen) і вибір бустера на старті (BoostPicker).
// Тексти латиницею: поточні шрифти можуть не мати кирилиці.
public class GameHud : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EnemySpawner spawner;
    [Tooltip("Необов'язково. Шрифт HUD. Якщо порожньо, береться шрифт TextMeshPro за замовчуванням.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Розташування (у пікселях при 1080x1920)")]
    [Tooltip("Відступ від країв екрана. Збільште, якщо HUD перекриває інші елементи або виріз камери.")]
    [SerializeField] private float margin = 30f;
    [Tooltip("Відступ верхнього ряду (лічильник хвиль, пауза, x2) від верху екрана. " +
             "Має бути нижче смуги HP бази, щоб нічого не накладалося.")]
    [SerializeField] private float topOffset = 115f;
    [SerializeField] private float smallButtonSize = 110f;
    [SerializeField] private Vector2 nextWaveButtonSize = new Vector2(420f, 150f);
    [Tooltip("Наскільки кнопка Next wave піднята від низу екрана.")]
    [SerializeField] private float nextWaveBottomOffset = 60f;

    [Header("Кольори")]
    [SerializeField] private Color buttonColor = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private Color accentColor = new Color(0.16f, 0.45f, 0.25f, 1f);
    [SerializeField] private Color pauseOverlayColor = new Color(0f, 0f, 0f, 0.65f);

    private GameObject hudRoot;
    private TMP_Text speedText;
    private TMP_Text waveText;
    private TMP_Text boostText;
    private GameObject toastRoot;
    private TMP_Text toastText;
    private float toastHideAt;
    private Button nextWaveButton;
    private TMP_Text nextWaveText;
    private GameObject pausePanel;
    private TMP_Text vibrationText;

    private void Awake()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        if (gameManager == null) Debug.LogError("[GameHud] На сцені немає GameManager.", this);

        BuildUI();
        pausePanel.SetActive(false);
        UpdateSpeedLabel();

        // Окремі вікна з власними Canvas, щоб кожне можна було правити незалежно.
        gameObject.AddComponent<ResultScreen>().Init(gameManager, font);
        gameObject.AddComponent<BoostPicker>().Init(gameManager, font);
        gameObject.AddComponent<CoinFlyEffect>();
    }

    private void OnEnable()
    {
        GameEvents.StateChanged += OnStateChanged;
        AdsManager.RewardedUnavailable += OnAdUnavailable;
    }

    private void OnDisable()
    {
        GameEvents.StateChanged -= OnStateChanged;
        AdsManager.RewardedUnavailable -= OnAdUnavailable;
    }

    private void OnAdUnavailable() => ShowToast("Ad is not ready yet. Try again in a moment.");

    // Коротке повідомлення внизу екрана. Реальний час, бо гра може стояти на паузі.
    public void ShowToast(string message, float seconds = 2.5f)
    {
        toastText.text = message;
        toastRoot.SetActive(true);
        toastHideAt = Time.unscaledTime + seconds;
    }

    private void OnStateChanged(GameState state)
    {
        pausePanel.SetActive(state == GameState.Paused);
        // Після перемоги чи поразки HUD ховається, щоб не перекривати їхні панелі.
        hudRoot.SetActive(state == GameState.Playing || state == GameState.Paused);
    }

    private void Update()
    {
        if (toastRoot.activeSelf && Time.unscaledTime >= toastHideAt)
            toastRoot.SetActive(false);

        if (spawner == null) return;

        int current = spawner.CurrentWave;
        int total = spawner.WaveCount;
        string prefix = LevelSession.IsDaily ? "Daily  " : "";
        waveText.text = prefix + (current == 0 ? "Get ready!" : $"Wave {current}/{total}");

        boostText.text = GameModifiers.TryGetActiveBoost(out BoostCard boost, out float timeLeft)
            ? $"{boost.DisplayName}  {Mathf.CeilToInt(timeLeft)}s"
            : "";

        bool showNextWave = spawner.IsWaiting && gameManager != null && gameManager.State == GameState.Playing;
        if (nextWaveButton.gameObject.activeSelf != showNextWave)
            nextWaveButton.gameObject.SetActive(showNextWave);

        if (showNextWave)
        {
            int seconds = Mathf.CeilToInt(spawner.TimeToNextWave);
            int bonus = spawner.NextWaveBonus;
            if (current == 0)
                nextWaveText.text = $"Start\n<size=70%>{seconds}s</size>";
            else
                nextWaveText.text = bonus > 0
                    ? $"Next wave  {seconds}s\n<size=80%>+{bonus} coins</size>"
                    : $"Next wave\n<size=70%>{seconds}s</size>";
        }
    }

    // ---------- Дії кнопок ----------

    private void OnPauseClicked() => gameManager.TogglePause();

    private void OnSpeedClicked()
    {
        gameManager.ToggleSpeed();
        UpdateSpeedLabel();
    }

    private void UpdateSpeedLabel()
    {
        if (speedText != null && gameManager != null)
            speedText.text = gameManager.IsFast ? "x2" : "x1";
    }

    private void OnNextWaveClicked() => spawner.CallNextWave();

    private void OnVibrationClicked()
    {
        Haptics.Enabled = !Haptics.Enabled;
        UpdateVibrationLabel();
        Haptics.Light(); // одразу відчутно, що вібрація ввімкнулась
    }

    private void UpdateVibrationLabel()
    {
        if (vibrationText != null)
            vibrationText.text = Haptics.Enabled ? "Vibration: ON" : "Vibration: OFF";
    }

    // ---------- Створення інтерфейсу ----------

    private void BuildUI()
    {
        // Нижче за меню будівництва (10), щоб меню відкривалось поверх HUD.
        RectTransform canvas = UiFactory.CreateCanvas("HudCanvas", transform, 5);

        hudRoot = UiFactory.CreateRect("Hud", canvas).gameObject;
        UiFactory.Stretch((RectTransform)hudRoot.transform);
        Vector2 small = Vector2.one * smallButtonSize;

        // Пауза — у правому верхньому куті.
        Button pause = UiFactory.CreateButton("Pause", hudRoot.transform, small, buttonColor,
                                              OnPauseClicked, font, 46f, out _, out TMP_Text pauseText);
        pauseText.text = "II";
        UiFactory.Anchor((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-margin, -topOffset));

        // Швидкість — ліворуч від паузи.
        Button speed = UiFactory.CreateButton("Speed", hudRoot.transform, small, buttonColor,
                                              OnSpeedClicked, font, 40f, out _, out speedText);
        UiFactory.Anchor((RectTransform)speed.transform, new Vector2(1f, 1f),
                         new Vector2(-margin - smallButtonSize - 16f, -topOffset));

        // Лічильник хвиль — зверху по центру.
        RectTransform waveRect = UiFactory.CreateRect("Wave", hudRoot.transform);
        waveRect.sizeDelta = new Vector2(340f, 70f);
        UiFactory.Anchor(waveRect, new Vector2(0.5f, 1f), new Vector2(0f, -topOffset));
        waveText = UiFactory.CreateText(waveRect, font, 44f);
        waveText.enableAutoSizing = true;
        waveText.fontSizeMin = 26f;
        waveText.fontSizeMax = 44f;

        RectTransform boostRect = UiFactory.CreateRect("Boost", hudRoot.transform);
        boostRect.sizeDelta = new Vector2(340f, 55f);
        UiFactory.Anchor(boostRect, new Vector2(0.5f, 1f), new Vector2(0f, -topOffset - 75f));
        boostText = UiFactory.CreateText(boostRect, font, 34f);
        boostText.enableAutoSizing = true;
        boostText.fontSizeMin = 22f;
        boostText.fontSizeMax = 34f;
        boostText.color = new Color(1f, 0.85f, 0.3f, 1f);

        // Наступна хвиля — знизу по центру, великою кнопкою під великий палець.
        nextWaveButton = UiFactory.CreateButton("NextWave", hudRoot.transform, nextWaveButtonSize, accentColor,
                                                OnNextWaveClicked, font, 44f, out _, out nextWaveText);
        UiFactory.Anchor((RectTransform)nextWaveButton.transform, new Vector2(0.5f, 0f),
                         new Vector2(0f, nextWaveBottomOffset));
        nextWaveButton.gameObject.SetActive(false);

        // Повідомлення — над кнопкою Next wave.
        RectTransform toastRect = UiFactory.CreateRect("Toast", canvas);
        toastRect.sizeDelta = new Vector2(1000f, 90f);
        UiFactory.Anchor(toastRect, new Vector2(0.5f, 0f), new Vector2(0f, nextWaveBottomOffset + nextWaveButtonSize.y + 40f));
        toastRect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);
        RectTransform toastLabel = UiFactory.CreateRect("Label", toastRect);
        UiFactory.Stretch(toastLabel);
        toastText = UiFactory.CreateText(toastLabel, font, 38f);
        toastRoot = toastRect.gameObject; // ховаємо і показуємо весь блок разом із фоном
        toastRoot.SetActive(false);

        BuildPausePanel(canvas);
    }

    private void BuildPausePanel(RectTransform canvas)
    {
        // Затемнення на весь екран: блокує тапи по полю, поки гра на паузі.
        RectTransform panel = UiFactory.CreateRect("PausePanel", canvas);
        UiFactory.Stretch(panel);
        panel.gameObject.AddComponent<Image>().color = pauseOverlayColor;
        pausePanel = panel.gameObject;

        RectTransform title = UiFactory.CreateRect("Title", panel);
        title.sizeDelta = new Vector2(800f, 140f);
        UiFactory.Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(0f, 380f));
        UiFactory.CreateText(title, font, 96f).text = "PAUSED";

        Vector2 size = new Vector2(480f, 150f);
        Button resume = UiFactory.CreateButton("Resume", panel, size, accentColor,
                                               () => gameManager.Resume(), font, 52f, out _, out TMP_Text resumeText);
        resumeText.text = "Resume";
        UiFactory.Anchor((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f));

        Button restart = UiFactory.CreateButton("Restart", panel, size, buttonColor,
                                                () => gameManager.Restart(), font, 52f, out _, out TMP_Text restartText);
        restartText.text = "Restart";
        UiFactory.Anchor((RectTransform)restart.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f));

        Button menu = UiFactory.CreateButton("Menu", panel, size, buttonColor,
                                             () => gameManager.ToMenu(), font, 52f, out _, out TMP_Text menuText);
        menuText.text = "Menu";
        UiFactory.Anchor((RectTransform)menu.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -180f));

        Button vibration = UiFactory.CreateButton("Vibration", panel, size, buttonColor,
                                                  OnVibrationClicked, font, 44f, out _, out vibrationText);
        UiFactory.Anchor((RectTransform)vibration.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -360f));
        UpdateVibrationLabel();
    }
}
