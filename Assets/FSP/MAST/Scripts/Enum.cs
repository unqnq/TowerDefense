namespace MAST
{
    // Toolbar position
    public enum ToolbarPos { Left, Right } // Later add top and bottom for horizontal palette

    // Modifiers used in conjunction with each hotkey — any combination.
    // NONE/SHIFT keep their old serialized values, so existing user
    // bindings migrate untouched
    [System.Flags]
    public enum HotkeyModifier
    {
        NONE = 0,
        SHIFT = 1,
        CTRL = 2,
        ALT = 4
    }

    // Draw Tool Selection "Greeble appended last so saved tool indices migrate"
    public enum BuildMode { None, DrawSingle, DrawContinuous, PaintArea, Randomize, Erase, Greeble }

    // What holding SHIFT does to placement while occupancy is enabled.
    // OccupiedCell is deliberately the zero value: a missing or reset
    // settings file lands on the default behavior
    public enum ShiftPlacement
    {
        [UnityEngine.InspectorName("Place inside pointed cell")] OccupiedCell = 0,
        [UnityEngine.InspectorName("Place on Grid (ignore occupancy)")] Grid = 1
    }

    // Prefab axis that points away from the greebled surface
    public enum GreebleAxis
    {
        [UnityEngine.InspectorName("Y+ (Up)")] YPlus = 0,
        [UnityEngine.InspectorName("Y-")] YMinus = 1,
        [UnityEngine.InspectorName("X+")] XPlus = 2,
        [UnityEngine.InspectorName("X-")] XMinus = 3,
        [UnityEngine.InspectorName("Z+")] ZPlus = 4,
        [UnityEngine.InspectorName("Z-")] ZMinus = 5
    }

    // Paint Tool Selection "explicit values match the serialized selectedPaintToolIndex"
    public enum PaintTool { None = -1, Paint = 0, Restore = 1, ReplaceAll = 2 }

    // In-progress mouse drags "explicit values match the serialized previous*ToolIndex fields"
    public enum BuildDrag { None = -1, Draw = 1, PaintArea = 2, Erase = 4 }
    public enum PaintDrag { None = -1, Paint = 1, Restore = 2 }

    // Merge Meshes window: per-material action, edge handling, and pivot choice
    public enum MergeAction { MergeWithAll = 0, MergeSeparately = 1, Delete = 2 }
    public enum EdgeMode { Keep = 0, Soften = 1, Harden = 2 }
    public enum MergePivot { SourceRoot = 0, WorldOrigin = 1 }

    // Axis for rotating and flipping Prefabs
    public enum Axis { X = 0, Y = 1, Z = 2 }

    // Randomizer scale axis lock
    public enum ScaleAxisLock { NONE = 0, XZ = 1, XYZ = 2 }

    // Raycast direction for Prefab placement
    public enum DirectionVector { Up = 0, Down = 1, Left = 2, Right = 3, Forward = 4, Back = 5 }
}
