using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MAST
{
    namespace Building
    {
        // Hide Prefabs Above: hides placed prefabs whose pivot sits above the
        // cutoff height (work on interiors under a roof).  Uses the editor's
        // SceneVisibilityManager, so nothing is ever written into the scene.
        public static class ViewFilter
        {
            private static readonly List<GameObject> hiddenObjects = new List<GameObject>();

            public static bool IsActive =>
                MAST.Settings.Data.gui != null && MAST.Settings.Data.gui.view.hidePrefabsAbove;

            public static void Toggle()
            {
                MAST.Settings.Data.gui.view.hidePrefabsAbove = !MAST.Settings.Data.gui.view.hidePrefabsAbove;
                Apply();
            }

            // Re-evaluate every placed prefab against the cutoff: everything above
            // the CURRENT GRID LEVEL "plus a small offset" hides, so raising and
            // lowering the grid walks through the floors
            public static void Apply()
            {
                ShowAll();

                if (!IsActive || Placement.targetParent == null)
                    return;

                float cutoff = MAST.Settings.Data.gui.grid.gridHeight * MAST.Settings.Data.gui.grid.yUnitSize
                    + MAST.Settings.Data.gui.view.hideOffset;

                foreach (Transform child in Placement.targetParent.transform)
                {
                    // Height is measured along the HOLDER'S local up, so floors
                    // stay floors however the holder is rotated
                    if (GridSpace.WorldToGrid(child.position).y > cutoff)
                    {
                        SceneVisibilityManager.instance.Hide(child.gameObject, true);
                        hiddenObjects.Add(child.gameObject);
                    }
                }

                SceneView.RepaintAll();
            }

            // Restore visibility "window closing, play mode, toggle off"
            public static void ShowAll()
            {
                foreach (GameObject hidden in hiddenObjects)
                {
                    if (hidden != null)
                        SceneVisibilityManager.instance.Show(hidden, true);
                }

                hiddenObjects.Clear();
            }
        }
    }
}
