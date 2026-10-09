using UnityEngine;
using UnityEngine.EventSystems;

public class BuildCell : MonoBehaviour, IPointerMoveHandler, IPointerClickHandler
{
    [SerializeField] private TurretSpawner turretSpawner;
    [SerializeField] private GameCursor cursor;
    [SerializeField] private bool isActive = true;

    private void Awake()
    {
        turretSpawner = FindAnyObjectByType<TurretSpawner>();
        cursor = FindAnyObjectByType<GameCursor>();
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        UpdateCursor();
    }

    private void UpdateCursor()
    {
        cursor.MoveTo(transform.position, isActive);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        UpdateCursor();
        turretSpawner.Spawn(transform.position);
        isActive = false;
    }
}
