using UnityEngine;

// Фарбує модель через MaterialPropertyBlock: матеріали не дублюються і не змінюються на диску.
// Білий колір знімає фарбування і повертає оригінальний вигляд.
public static class Tint
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
    private static readonly int ColorId = Shader.PropertyToID("_Color");         // Built-in
    private static MaterialPropertyBlock block;

    public static void Apply(GameObject target, Color color)
    {
        block ??= new MaterialPropertyBlock();
        bool clear = color == Color.white;

        foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            // Частинки і лінії не чіпаємо, лише моделі.
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

            if (clear)
            {
                renderer.SetPropertyBlock(null);
                continue;
            }
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }
    }
}
