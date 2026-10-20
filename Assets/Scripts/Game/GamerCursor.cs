using UnityEngine;

public class GameCursor : MonoBehaviour
{
    private Material cursorMaterial;
    [SerializeField] private Color dontSpawnColor = Color.red;
    private Color validColor;

    private void Awake()
    {
        cursorMaterial = GetComponentInChildren<Renderer>().material;
        validColor = cursorMaterial.color;
    }

    public void MoveTo(Vector3 buildPosition, bool canBuild)
    {
        // Курсор завжди переїжджає на клітинку під вказівником, а колір показує, чи можна там будувати.
        transform.position = new Vector3(buildPosition.x, 1.8f, buildPosition.z);
        cursorMaterial.color = canBuild ? validColor : dontSpawnColor;
    }
}
