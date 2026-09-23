using UnityEngine;
using UnityEditor;

namespace MAST
{
    namespace Building
    {
        // Redesigned eraser: raycast from the mouse pointer, outline exactly what
        // will be deleted in red, delete on click or while dragging.  Anything
        // that is a child of the MAST Holder is erasable — no grid involved.
        public static class Eraser
        {
            private static GameObject target;
            private static Vector2 pointerPosition;

            // The prefab an erase revealed: it only became hittable because the
            // prefab in front of it was deleted, so it can't be erased "or even
            // highlighted" until the pointer leaves it and re-enters it by
            // actually dragging — or a fresh click targets it deliberately.
            // One click erases one prefab; sweeps only erase what the pointer
            // moves onto
            private static GameObject shieldedTarget;

            public static bool HasTarget => target != null;

            public static void UpdateTarget(Vector2 mousePosition)
            {
                pointerPosition = mousePosition;
                target = FindTargetAt(mousePosition);

                if (shieldedTarget != null)
                {
                    // Still hovering what the last erase revealed — hands off
                    if (target == shieldedTarget)
                    {
                        target = null;
                        return;
                    }

                    // The pointer moved off it "onto empty space or another
                    // prefab" — it's fair game once the pointer re-enters it
                    shieldedTarget = null;
                }
            }

            // Find the placed unit under the mouse: the nearest raycast hit that
            // belongs under the MAST Holder, resolved to the holder's direct child
            private static GameObject FindTargetAt(Vector2 mousePosition)
            {
                if (Placement.targetParent == null)
                    return null;

                Ray ray = HandleUtility.GUIPointToWorldRay(mousePosition);
                float bestDistance = float.MaxValue;
                GameObject bestTarget = null;

                foreach (RaycastHit hit in Physics.RaycastAll(ray, 100000f))
                {
                    GameObject unit = GetHolderChild(hit.transform);
                    if (unit != null && hit.distance < bestDistance)
                    {
                        bestDistance = hit.distance;
                        bestTarget = unit;
                    }
                }

                return bestTarget;
            }

            // Walk up from a hit collider to the direct child of the MAST Holder
            private static GameObject GetHolderChild(Transform hitTransform)
            {
                Transform holder = Placement.targetParent.transform;

                for (Transform current = hitTransform; current != null; current = current.parent)
                {
                    if (current.parent == holder)
                        return current.gameObject;
                }

                return null;
            }

            // Red wire outline around exactly what will be deleted
            public static void DrawHighlight()
            {
                if (target == null)
                    return;

                Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0)
                    return;

                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    bounds.Encapsulate(renderers[i].bounds);

                Color previousColor = Handles.color;
                Handles.color = Color.red;
                Handles.DrawWireCube(bounds.center, bounds.size);
                Handles.color = previousColor;
            }

            public static void EraseTarget()
            {
                if (target == null)
                    return;

                Undo.DestroyObjectImmediate(target);
                target = null;

                // Re-raycast the same spot right away: whatever it finds was
                // revealed by this erase, not entered by dragging — shield it
                shieldedTarget = FindTargetAt(pointerPosition);
            }

            // A new click is new intent: clicking the same spot again erases
            // the prefab the last erase revealed
            public static void ClearShield()
            {
                shieldedTarget = null;
            }

            public static void ClearTarget()
            {
                target = null;
                shieldedTarget = null;
            }
        }
    }
}
