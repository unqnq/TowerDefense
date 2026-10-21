using UnityEngine;

// Коло радіусу вежі на землі. Показується, поки відкрите меню вежі.
// Текстура кільця генерується з коду, а малюється звичайним SpriteRenderer:
// не потрібні ні окремий спрайт, ні шейдер, який міг би зникнути з білду.
public class RangeIndicator : MonoBehaviour
{
    private const int TextureSize = 256;
    private static Sprite ringSprite;

    private SpriteRenderer spriteRenderer;

    public static RangeIndicator Create(Transform parent)
    {
        var go = new GameObject("RangeIndicator");
        go.transform.SetParent(parent, false);
        var indicator = go.AddComponent<RangeIndicator>();
        indicator.spriteRenderer = go.AddComponent<SpriteRenderer>();
        indicator.spriteRenderer.sprite = GetRing();
        indicator.spriteRenderer.color = new Color(1f, 1f, 1f, 0.6f);
        go.SetActive(false);
        return indicator;
    }

    public void Show(Vector3 center, float radius)
    {
        // Кладемо спрайт на землю і трохи піднімаємо, щоб не мерехтів разом із плитками.
        transform.SetPositionAndRotation(center + Vector3.up * 0.05f, Quaternion.Euler(90f, 0f, 0f));
        transform.localScale = Vector3.one * radius * 2f; // спрайт має розмір 1×1 одиницю
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    private static Sprite GetRing()
    {
        if (ringSprite != null) return ringSprite;

        var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float center = (TextureSize - 1) * 0.5f;
        float outer = TextureSize * 0.5f - 1f;
        float inner = outer - 6f;
        var pixels = new Color32[TextureSize * TextureSize];
        for (int y = 0; y < TextureSize; y++)
        for (int x = 0; x < TextureSize; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
            byte ring = (byte)(Mathf.Clamp01(Mathf.Min(outer - d, d - inner)) * 255f); // згладжені краї кільця
            byte fill = d < inner ? (byte)35 : (byte)0;                                 // ледь помітна заливка
            pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)Mathf.Max(ring, fill));
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        ringSprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize),
                                   new Vector2(0.5f, 0.5f), TextureSize);
        return ringSprite;
    }
}
