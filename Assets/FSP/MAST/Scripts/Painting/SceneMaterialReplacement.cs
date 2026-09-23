using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Painting
    {
        // Replace every instance of one material with another across the
        // GameObjects in the SAME MAST HOLDER as the clicked object.  This is
        // deliberately different from the Replace Materials tool, which edits
        // prefab assets: nothing here touches an asset, only the renderers of
        // placed instances.  Every changed slot is journaled and the whole
        // sweep is one undo step.
        public static class SceneMaterialReplacement
        {
            // Returns the number of changed slots, or -1 when the clicked
            // object isn't under a MAST Holder
            public static int ReplaceAllUnderHolder(Renderer clickedRenderer, Material from, Material to)
            {
                if (clickedRenderer == null || from == null || to == null || from == to)
                    return 0;

                Transform holder = FindHolderOf(clickedRenderer.transform);
                if (holder == null)
                    return -1;

                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Replace Material In Holder");
                int undoGroup = Undo.GetCurrentGroup();

                int changedSlots = 0;

                foreach (Renderer renderer in holder.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] sharedMaterials = renderer.sharedMaterials;
                    bool rendererChanged = false;

                    for (int slot = 0; slot < sharedMaterials.Length; slot++)
                    {
                        if (sharedMaterials[slot] != from)
                            continue;

                        if (!rendererChanged)
                            Undo.RecordObject(renderer, "Replace Material In Holder");

                        sharedMaterials[slot] = to;
                        rendererChanged = true;
                        changedSlots++;

                        Journal.RecordPaint(renderer, slot, from, to);
                    }

                    if (rendererChanged)
                        renderer.sharedMaterials = sharedMaterials;
                }

                Undo.CollapseUndoOperations(undoGroup);
                return changedSlots;
            }

            // Walk up from the clicked object to its MAST Holder "the active
            // holder or anything carrying the holder tag"
            private static Transform FindHolderOf(Transform start)
            {
                for (Transform current = start; current != null; current = current.parent)
                {
                    if (MAST.Building.Placement.targetParent != null &&
                        current.gameObject == MAST.Building.Placement.targetParent)
                        return current;

                    try
                    {
                        if (current.CompareTag(MAST.Const.Placement.defaultTargetParentTag))
                            return current;
                    }
                    catch
                    {
                        // Tag not defined in this project yet — reference
                        // comparison above still works
                    }
                }

                return null;
            }
        }
    }
}
