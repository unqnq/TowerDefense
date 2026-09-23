using System;
using UnityEngine;


namespace MAST
{
    namespace Settings
    {
        namespace ScriptObj
        {
            [Serializable]
            public class GUISettings : ScriptableObject
            {
                [SerializeField] public Grid grid;
                [Serializable] public class Grid
                {
                    [SerializeField] public float xzUnitSize = 1f;
                    [SerializeField] public float yUnitSize = 1f;
                    
                    [SerializeField] public int cellCount = 50;

                    [SerializeField] public int gridHeight = 0;

                    [Tooltip("Layer used by MAST's grid plane.  Pick a layer your project doesn't use — it is locked while the grid is visible.  (The old default, 4, is Unity's built-in Water layer)")]
                    [Range(0, 31)]
                    [SerializeField] public int gridLayer = 31;
                    
                    [SerializeField] public Color tintColor = new Color32(255, 255, 255, 127);
                    
                    public float yPos()
                    {
                        return gridHeight * yUnitSize;
                    }
                }
                
                [SerializeField] public Palette palette;
                [Serializable] public class Palette
                {
                    // Any color "was three preset shades"; the default matches
                    // the old Dark preset
                    [SerializeField] public Color bgColor = new Color(42f / 255f, 42f / 255f, 42f / 255f, 1f);
                    [SerializeField] public float snapshotCameraPitch = 225f;
                    [SerializeField] public float snapshotCameraYaw = 30f;

                    // Thumbnail key light angles, RELATIVE TO THE CAMERA VIEW so
                    // the default top-left key holds at any camera orbit.  The
                    // light always aims through the subject's center
                    [SerializeField] public float snapshotLightPitch = 30f;
                    [SerializeField] public float snapshotLightYaw = 40f;
                    [SerializeField] public Color snapshotLightColor = Color.white;

                    // Ambient for thumbnail renders: its color IS the brightness
                    // "a darker color means dimmer ambient"
                    [SerializeField] public Color snapshotAmbientColor = new Color(0.3f, 0.3f, 0.3f, 1f);

                    [SerializeField] public bool overwriteThumbnails = false;
                    [SerializeField] public int thumbnailSize = 128;
                }

                [SerializeField] public View view;
                [Serializable] public class View
                {
                    [SerializeField] public bool hidePrefabsAbove = false;

                    // Added to the current grid level's world height; prefabs with
                    // pivots above "grid level + offset" hide while the toggle is on
                    [SerializeField] public float hideOffset = 0.5f;
                }
                
                [SerializeField] public Toolbar toolbar;
                [Serializable] public class Toolbar
                {
                    [SerializeField] public int selectedDrawToolIndex = -1;
                    [SerializeField] public int selectedPaintToolIndex = -1;
                    [SerializeField] public ToolbarPos position = ToolbarPos.Left;
                    [SerializeField] public float scale = 1f;

                    [Tooltip("Accent color for the toolbar icons.  The icons' cyan tones are redrawn as light and dark versions of this color — white, black, and shadows stay as they are")]
                    [SerializeField] public Color iconColor = new Color(79f / 255f, 179f / 255f, 194f / 255f, 1f);
                }
            }
        }
    }
}
