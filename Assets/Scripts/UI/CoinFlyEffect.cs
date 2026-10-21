using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Візуальний ефект нагороди: з убитого ворога вилітають монети й летять до лічильника монет.
// Гроші нараховуються одразу при вбивстві (Bank), а цей ефект лише показує їх, тож на економіку він не впливає.
// Створюється GameHud. Ціль — об'єкт "MoneyImage" у сцені (іконка монети біля лічильника);
// якщо його немає, монети летять до тексту з кількістю грошей.
public class CoinFlyEffect : MonoBehaviour
{
    private const int MaxCoinsPerKill = 5;     // більше монет за раз лише захаращують екран
    private const float CoinSize = 56f;        // у пікселях при 1080x1920
    private const float BurstTime = 0.18f;     // монети "вистрибують" з ворога
    private const float FlyTime = 0.55f;       // і летять до лічильника
    private const float PunchTime = 0.15f;     // лічильник коротко "підстрибує" при влучанні

    private RectTransform canvasRect;
    private RectTransform target;
    private Vector3 targetBaseScale;
    private Sprite coinSprite;
    private Camera cam;
    private float punchLeft;
    private readonly Stack<Image> pool = new Stack<Image>();

    private void Awake()
    {
        // Над HUD (5), але під меню будівництва (10) і вікнами результату (20).
        canvasRect = UiFactory.CreateCanvas("CoinCanvas", transform, 6);
        coinSprite = Resources.Load<Sprite>("CoinImage");
        cam = Camera.main;
        FindTarget();
    }

    private void OnEnable() => GameEvents.CoinsDropped += OnCoinsDropped;
    private void OnDisable() => GameEvents.CoinsDropped -= OnCoinsDropped;

    private void FindTarget()
    {
        GameObject icon = GameObject.Find("MoneyImage");
        if (icon != null) target = icon.transform as RectTransform;
        else
        {
            Bank bank = FindAnyObjectByType<Bank>();
            if (bank != null) target = bank.MoneyLabel;
        }
        if (target != null) targetBaseScale = target.localScale;
        else Debug.LogWarning("[CoinFlyEffect] Не знайдено лічильник монет (MoneyImage), ефект вимкнено.", this);
    }

    private void OnCoinsDropped(Vector3 worldPosition, int amount)
    {
        if (amount <= 0 || target == null || coinSprite == null) return;
        if (cam == null) cam = Camera.main;
        if (cam == null) return;

        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        if (screen.z < 0f) return; // ворог позаду камери

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 start);
        int count = Mathf.Clamp(amount, 1, MaxCoinsPerKill);
        for (int i = 0; i < count; i++)
            StartCoroutine(Fly(GetCoin(), start, i * 0.06f));
    }

    // Реальний час, а не ігровий: монети долітають і на паузі, і після перемоги, а не застигають у повітрі.
    private IEnumerator Fly(Image coin, Vector2 start, float delay)
    {
        RectTransform rect = coin.rectTransform;
        Vector2 burst = start + Random.insideUnitCircle.normalized * Random.Range(40f, 90f) + Vector2.up * 30f;
        rect.anchoredPosition = start;
        rect.localScale = Vector3.one * 0.3f;
        coin.gameObject.SetActive(true);

        for (float t = 0f; t < BurstTime; t += Time.unscaledDeltaTime)
        {
            float k = t / BurstTime;
            rect.anchoredPosition = Vector2.Lerp(start, burst, 1f - (1f - k) * (1f - k));
            rect.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, k);
            yield return null;
        }

        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        // Політ по дузі: контрольна точка трохи вбік, щоб монети не летіли рівною лінією.
        Vector2 side = new Vector2(Random.Range(-120f, 120f), Random.Range(60f, 160f));
        for (float t = 0f; t < FlyTime; t += Time.unscaledDeltaTime)
        {
            float k = t / FlyTime;
            float e = k * k; // повільно на старті, швидко в кінці — ніби монету "притягує"
            Vector2 end = TargetPosition(); // щокадру, на випадок зміни розміру екрана
            Vector2 control = (burst + end) * 0.5f + side;
            rect.anchoredPosition = Bezier(burst, control, end, e);
            rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.7f, k);
            yield return null;
        }

        Release(coin);
        punchLeft = PunchTime;
    }

    private void Update()
    {
        if (target == null || punchLeft <= 0f) return;
        punchLeft -= Time.unscaledDeltaTime;
        float k = Mathf.Max(0f, punchLeft) / PunchTime;
        target.localScale = targetBaseScale * (1f + 0.25f * k);
    }

    private Vector2 TargetPosition()
    {
        // Canvas сцени — Screen Space Overlay, тож світова позиція UI-елемента вже в пікселях екрана.
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, target.position);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        return local;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    // Пул: монети не створюються й не знищуються на кожне вбивство (менше фризів від збирача сміття).
    private Image GetCoin()
    {
        while (pool.Count > 0)
        {
            Image cached = pool.Pop();
            if (cached != null) return cached;
        }

        RectTransform rect = UiFactory.CreateRect("Coin", canvasRect);
        rect.sizeDelta = Vector2.one * CoinSize;
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = coinSprite;
        image.preserveAspect = true;
        image.raycastTarget = false; // не заважає тапам по полю
        return image;
    }

    private void Release(Image coin)
    {
        coin.gameObject.SetActive(false);
        pool.Push(coin);
    }
}
