using UnityEngine;
using UnityEngine.EventSystems;

// Клітинка, на якій можна поставити вежу.
//  - Тап/клік по вільній клітинці: меню будівництва. По зайнятій: меню з продажем.
//  - Перетягування вежі на таку саму вежу (той самий тип і рівень): merge у вежу наступного рівня.
//    Якщо відпустити деінде, вежа повертається на своє місце.
//  - Наведення мишею (ПК/itch.io) лише підсвічує клітинку курсором.
public class BuildCell : MonoBehaviour,
    IPointerMoveHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private TurretSpawner turretSpawner;
    [SerializeField] private GameCursor cursor;
    [SerializeField] private bool isActive = true;
    [Tooltip("На скільки вежа піднімається над полем, поки її тягнуть.")]
    [SerializeField] private float dragLift = 0.4f;

    // Клітинка, з якої зараз тягнуть вежу (одночасно тягнути можна лише одну).
    private static BuildCell dragSource;
    public static bool IsDragging => dragSource != null;

    private BuildMenu buildMenu;
    private GameManager gameManager;
    private Camera cam;
    private Collider[] draggedColliders;

    // Вежа на цій клітинці (null, якщо клітинка вільна або заблокована).
    public Turret PlacedTurret { get; private set; }
    public bool IsFree => isActive;

    private void Awake()
    {
        turretSpawner = FindAnyObjectByType<TurretSpawner>();
        cursor = FindAnyObjectByType<GameCursor>();
        buildMenu = FindAnyObjectByType<BuildMenu>();
        gameManager = FindAnyObjectByType<GameManager>();
    }

    private void OnDisable()
    {
        if (dragSource == this) CancelDrag();
    }

    // ---------- Наведення і тап ----------

    public void OnPointerMove(PointerEventData eventData)
    {
        // Поки меню відкрите або тягнуть вежу, курсором керують вони.
        if (IsDragging || (buildMenu != null && buildMenu.IsOpen)) return;
        UpdateCursor();
    }

    private void UpdateCursor()
    {
        cursor.MoveTo(transform.position, isActive);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Після перетягування Unity сам не надсилає клік, але про всяк випадок.
        if (eventData.dragging) return;

        UpdateCursor();

        if (buildMenu != null)
        {
            buildMenu.Open(this);
        }
        else if (isActive && turretSpawner.Towers.Count > 0)
        {
            // Запасний варіант, якщо на сцені немає меню: будуємо першу вежу зі списку, як раніше.
            TryBuild(turretSpawner.Towers[0]);
        }
    }

    // ---------- Перетягування для merge ----------

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (PlacedTurret == null || IsDragging) return;
        if (gameManager != null && gameManager.State != GameState.Playing) return;
        if (buildMenu != null && buildMenu.IsOpen) return;

        dragSource = this;
        cam = Camera.main;

        // Поки вежу тягнуть, вона не стріляє і не перекриває променем клітинки під пальцем.
        PlacedTurret.enabled = false;
        draggedColliders = PlacedTurret.GetComponentsInChildren<Collider>();
        foreach (Collider col in draggedColliders) col.enabled = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragSource != this || PlacedTurret == null) return;

        // Вежа йде за пальцем по площині поля.
        if (cam != null)
        {
            Plane ground = new Plane(Vector3.up, transform.position);
            Ray ray = cam.ScreenPointToRay(eventData.position);
            if (ground.Raycast(ray, out float distance))
                PlacedTurret.transform.position = ray.GetPoint(distance) + Vector3.up * dragLift;
        }

        // Курсор на клітинці під пальцем: зелений, якщо тут можна злити, червоний — якщо ні.
        BuildCell target = CellUnder(eventData);
        if (target != null && target != this)
            cursor.MoveTo(target.transform.position, CanMergeInto(target));
        else
            cursor.MoveTo(transform.position, false);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragSource != this) return;

        BuildCell target = CellUnder(eventData);
        if (target != null && target != this && CanMergeInto(target))
        {
            Turret source = PlacedTurret;
            RestoreDragged();
            ClearCell();
            target.ReceiveMerge(source);
        }
        else
        {
            CancelDrag();
        }
        dragSource = null;
    }

    private bool CanMergeInto(BuildCell target) =>
        turretSpawner.CanMerge(PlacedTurret, target.PlacedTurret);

    private static BuildCell CellUnder(PointerEventData eventData)
    {
        GameObject hit = eventData.pointerCurrentRaycast.gameObject;
        return hit != null ? hit.GetComponentInParent<BuildCell>() : null;
    }

    private void ReceiveMerge(Turret source)
    {
        Turret merged = turretSpawner.Merge(source, PlacedTurret);
        if (merged != null)
        {
            PlacedTurret = merged; // при зміні моделі це вже новий об'єкт
            Haptics.Medium();
        }
        UpdateCursor();
    }

    // Повертає вежу на місце, якщо merge не відбувся.
    private void CancelDrag()
    {
        if (PlacedTurret != null) PlacedTurret.transform.position = transform.position;
        RestoreDragged();
        if (dragSource == this) dragSource = null;
        UpdateCursor();
    }

    private void RestoreDragged()
    {
        if (PlacedTurret != null) PlacedTurret.enabled = true;
        if (draggedColliders != null)
            foreach (Collider col in draggedColliders)
                if (col != null) col.enabled = true;
        draggedColliders = null;
    }

    // ---------- Будівництво і продаж ----------

    // Клітинка стає зайнятою лише тоді, коли вежу справді поставили (вистачило грошей).
    public bool TryBuild(TowerData data)
    {
        if (!isActive) return false;

        Turret turret = turretSpawner.Spawn(data, transform.position);
        if (turret == null) return false;

        PlacedTurret = turret;
        isActive = false;
        UpdateCursor(); // курсор одразу червоніє
        Haptics.Light();
        return true;
    }

    public void Sell()
    {
        if (PlacedTurret == null) return;

        turretSpawner.Sell(PlacedTurret);
        ClearCell();
        Haptics.Light();
        UpdateCursor();
    }

    private void ClearCell()
    {
        PlacedTurret = null;
        isActive = true;
    }
}
