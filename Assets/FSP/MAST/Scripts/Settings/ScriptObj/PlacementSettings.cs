using System;
using UnityEngine;


namespace MAST
{
    namespace Settings
    {
        namespace ScriptObj
        {
            public class PlacementSettings : ScriptableObject
            {
                //[SerializeField] public string targetParentName = MAST.Const.Placement.defaultTargetParentName;
                //[SerializeField] public int targetParentInstanceID = 0;
                
                [SerializeField] public bool snapToGrid = true;

                // Occupancy "cell-accurate overlap blocking + face-adjacent placement"
                [Tooltip("Master switch for the occupancy system: overlap blocking and face-adjacent placement.  Hold SHIFT while placing to bypass it for a single placement (fence posts and rails sharing a cell)")]
                [SerializeField] public bool enableOccupancy = true;
                [Tooltip("Point at a placed model to build against the face under the pointer")]
                [SerializeField] public bool placeOnFaces = true;
                [Tooltip("Draw the cells the current placement claims (green = free, red = blocked)")]
                [SerializeField] public bool showOccupancyCells = true;

                [Tooltip("What holding SHIFT does.  Place on Grid: occupancy is ignored and the piece targets the grid plane.  Place inside pointed cell: the piece embeds into the occupied cell under the pointer — overlap placement at any height without moving the grid")]
                [SerializeField] public ShiftPlacement shiftPlacement = ShiftPlacement.OccupiedCell;
                [Tooltip("How deep geometry must reach into a grid cell to claim it (fraction of the cell, 0.15 = 15%)")]
                [Range(0f, 0.45f)]
                [SerializeField] public float occupancyMargin = 0.15f;

                [SerializeField] public bool overridePrefabOffset = false;
                [SerializeField] public Offset offset;
                [Serializable] public class Offset
                {
                    [SerializeField] public Vector3 pos = new Vector3(0.0f, 0.0f, 0.0f);
                }
                
                [SerializeField] public bool overridePrefabRotation = false;
                [SerializeField] public Rotation rotation;
                [Serializable] public class Rotation
                {
                    [SerializeField] public Vector3 step = new Vector3(0.0f, 90.0f, 0.0f);

                    [Tooltip("Rotate placed models with the MAST Holder.  On: pieces stay squared with the rotated grid.  Off: pieces keep their world rotation (props stay upright on a tilted holder) while still snapping to the rotated grid's cells")]
                    [SerializeField] public bool rotateWithHolder = false;
                }
                
                [SerializeField] public Greeble greeble;
                [Serializable] public class Greeble
                {
                    [Tooltip("Snap points per face axis, spaced evenly between the face edges.  1 = center, 2 = 33% and 66%, 3 = 25%, 50%, and 75%")]
                    [Range(1, 9)]
                    [SerializeField] public int divisions = 1;

                    [Tooltip("Prefab axis that points away from the greebled surface")]
                    [SerializeField] public GreebleAxis outwardAxis = GreebleAxis.YPlus;
                }

                [SerializeField] public bool overridePrefabRaycast = false;
                [SerializeField] public MAST.DataClass.PlacementRaycast placementRaycast;
                
                [SerializeField] public bool overridePrefabRandomizer = false;
                [SerializeField] public MAST.DataClass.Randomizer randomizer;
            }
        }
    }
}
