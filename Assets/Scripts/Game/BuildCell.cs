using UnityEngine;
using UnityEngine.EventSystems;

public class BuildCell : MonoBehaviour, IPointerMoveHandler, IPointerClickHandler
{
    [SerializeField] private TurretSpawner turretSpawner;
    [SerializeField] private Cursor cursor;
    [SerializeField] private bool isActive = true;

    private void Awake()
    {
        turretSpawner = FindAnyObjectByType<TurretSpawner>();
        cursor = FindAnyObjectByType<Cursor>();
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
