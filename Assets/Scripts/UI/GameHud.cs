using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Ігровий HUD для телефона. Будується з коду, тож у сцені потрібен лише цей компонент.
//  - справа зверху: пауза і перемикач швидкості x1/x2;
//  - зверху по центру: лічильник хвиль;
//  - знизу по центру: кнопка "Start" / "Next wave" з відліком і бонусом (видно лише між хвилями);
//  - екран паузи з кнопками Resume, Restart і перемикачем вібрації.
// Тексти латиницею: поточні шрифти можуть не мати кирилиці.
public class GameHud : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;
    [SerializeField] private EnemySpawner spawner;
    [Tooltip("Необов'язково. Шрифт HUD. Якщо порожньо, береться шрифт TextMeshPro за замовчуванням.")]
    [SerializeField] private TMP_FontAsset font;

    [Header("Розташування (у пікселях при 1080x1920)")]
    [Tooltip("Відступ від країв екрана. Збільште, якщо HUD перекриває інші елементи або виріз камери.")]
    [SerializeField] private float margin = 40f;
    [SerializeField] private float smallButtonSize = 130f;
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
    }

    private void OnEnable() => GameEvents.StateChanged += OnStateChanged;
    private void OnDisable() => GameEvents.StateChanged -= OnStateChanged;

    private void OnStateChanged(GameState state)
    {
        pausePanel.SetActive(state == GameState.Paused);
        // Після перемоги чи поразки HUD ховається, щоб не перекривати їхні панелі.
        hudRoot.SetActive(state == GameState.Playing || state == GameState.Paused);
    }

    private void Update()
    {
        if (spawner == null) return;

        int current = spawner.CurrentWave;
        int total = spawner.WaveCount;
        waveText.text = current == 0 ? "Get ready!" : $"Wave {current}/{total}";

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
                                              OnPauseClicked, font, 52f, out _, out TMP_Text pauseText);
        pauseText.text = "II";
        UiFactory.Anchor((RectTransform)pause.transform, new Vector2(1f, 1f), new Vector2(-margin, -margin));

        // Швидкість — ліворуч від паузи.
        Button speed = UiFactory.CreateButton("Speed", hudRoot.transform, small, buttonColor,
                                              OnSpeedClicked, font, 44f, out _, out speedText);
        UiFactory.Anchor((RectTransform)speed.transform, new Vector2(1f, 1f),
                         new Vector2(-margin - smallButtonSize - 20f, -margin));

        // Лічильник хвиль — зверху по центру.
        RectTransform waveRect = UiFactory.CreateRect("Wave", hudRoot.transform);
        waveRect.sizeDelta = new Vector2(420f, 80f);
        UiFactory.Anchor(waveRect, new Vector2(0.5f, 1f), new Vector2(0f, -margin));
        waveText = UiFactory.CreateText(waveRect, font, 46f);

        // Наступна хвиля — знизу по центру, великою кнопкою під великий палець.
        nextWaveButton = UiFactory.CreateButton("NextWave", hudRoot.transform, nextWaveButtonSize, accentColor,
                                                OnNextWaveClicked, font, 44f, out _, out nextWaveText);
        UiFactory.Anchor((RectTransform)nextWaveButton.transform, new Vector2(0.5f, 0f),
                         new Vector2(0f, nextWaveBottomOffset));
        nextWaveButton.gameObject.SetActive(false);

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
        UiFactory.Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f));
        UiFactory.CreateText(title, font, 96f).text = "PAUSED";

        Vector2 size = new Vector2(480f, 150f);
        Button resume = UiFactory.CreateButton("Resume", panel, size, accentColor,
                                               () => gameManager.Resume(), font, 52f, out _, out TMP_Text resumeText);
        resumeText.text = "Resume";
        UiFactory.Anchor((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f));

        Button restart = UiFactory.CreateButton("Restart", panel, size, buttonColor,
                                                () => gameManager.Restart(), font, 52f, out _, out TMP_Text restartText);
        restartText.text = "Restart";
        UiFactory.Anchor((RectTransform)restart.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f));

        Button vibration = UiFactory.CreateButton("Vibration", panel, size, buttonColor,
                                                  OnVibrationClicked, font, 44f, out _, out vibrationText);
        UiFactory.Anchor((RectTransform)vibration.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -340f));
        UpdateVibrationLabel();
    }
}
