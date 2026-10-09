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
        if (canBuild)
        {
            transform.position = new Vector3(buildPosition.x, 1.8f, buildPosition.z);
            cursorMaterial.color = validColor;
        }
        else
        {
            cursorMaterial.color = dontSpawnColor;
        }
    }

}
