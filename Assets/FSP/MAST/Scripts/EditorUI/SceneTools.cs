using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace MAST
{
    namespace EditorUI
    {
        // ---------------------------------------------------------------------------
        // Scene-view tool handlers: one class per tool, dispatched by
        // SceneToolRouter.  MASTWindow used to run every tool's event handling
        // inline in one giant switch — adding a tool meant growing the window.
        // Now a tool is a self-contained handler and one dictionary entry.
        // ---------------------------------------------------------------------------

        public interface ISceneTool
        {
            void HandleSceneEvent(Event evt);
        }

        // Mouse capture shared by every scene tool "keep a click on a MAST
        // tool from also box-selecting in the scene".  The hot control is
        // released on raw mouse-up by MASTWindow — it used to stay grabbed
        // forever, degrading later scene-view event handling
        public static class SceneInput
        {
            private static int capturedHotControl;

            public static void CaptureMouse()
            {
                capturedHotControl = GUIUtility.GetControlID(FocusType.Passive);
                GUIUtility.hotControl = capturedHotControl;
                Event.current.Use();
            }

            public static void ReleaseMouseIfCaptured()
            {
                if (capturedHotControl != 0 && GUIUtility.hotControl == capturedHotControl)
                    GUIUtility.hotControl = 0;

                capturedHotControl = 0;
            }
        }

        // ---------------------------------------------------------------------------
        // Router
        // ---------------------------------------------------------------------------
        public static class SceneToolRouter
        {
            private static readonly Dictionary<BuildMode, ISceneTool> buildTools =
                new Dictionary<BuildMode, ISceneTool>
                {
                    { BuildMode.DrawSingle, new DrawSingleTool() },
                    { BuildMode.DrawContinuous, new DrawContinuousTool() },
                    { BuildMode.PaintArea, new PaintAreaSceneTool() },
                    { BuildMode.Randomize, new RandomizeTool() },
                    { BuildMode.Greeble, new GreeblePlacementTool() },
                    { BuildMode.Erase, new EraserSceneTool() }
                };

            private static readonly Dictionary<PaintTool, ISceneTool> paintTools =
                new Dictionary<PaintTool, ISceneTool>
                {
                    { PaintTool.Paint, new PaintMaterialTool() },
                    { PaintTool.Restore, new RestoreMaterialTool() },
                    { PaintTool.ReplaceAll, new ReplaceAllSceneTool() }
                };

            public static void HandleBuildEvent(Event evt)
            {
                // Cancel any in-progress drag that no longer matches the active
                // tool "deselected by hotkey or toolbar mid-drag".  A draw drag
                // is only valid for Draw Continuous — Draw Single is strictly
                // one placement per click
                switch (ToolState.ActiveBuildDrag)
                {
                    case BuildDrag.Draw:
                        if (ToolState.ActiveBuildTool != BuildMode.DrawContinuous)
                            ToolState.CancelBuildDrag();
                        break;
                    case BuildDrag.PaintArea:
                        if (ToolState.ActiveBuildTool != BuildMode.PaintArea)
                            ToolState.CancelBuildDrag();
                        break;
                    case BuildDrag.Erase:
                        if (ToolState.ActiveBuildTool != BuildMode.Erase)
                            ToolState.CancelBuildDrag();
                        break;
                }

                // Placement needs a palette selection; the eraser doesn't
                if (ToolState.SelectedPrefabIndex <= -1 &&
                    ToolState.ActiveBuildTool != BuildMode.Erase)
                    return;

                if (buildTools.TryGetValue(ToolState.ActiveBuildTool, out ISceneTool tool))
                    tool.HandleSceneEvent(evt);
            }

            public static void HandlePaintEvent(Event evt)
            {
                if (paintTools.TryGetValue(ToolState.ActivePaintTool, out ISceneTool tool))
                    tool.HandleSceneEvent(evt);
            }
        }

        // ---------------------------------------------------------------------------
        // Build tools
        // ---------------------------------------------------------------------------

        // Shared ghost handling for the tools that place on the grid: aim the
        // visualizer, outline blocked placements, draw the claimed cells.
        // SHIFT deliberately passes through the modifier guard for these tools:
        // holding it places while IGNORING occupancy "fence posts and rails
        // share a cell" — alt stays camera orbit, ctrl stays selection
        // (thanks OverseerShenk!)
        public abstract class GridPlacementTool : ISceneTool
        {
            public void HandleSceneEvent(Event evt)
            {
                if (evt.type == EventType.MouseDown && (evt.alt || evt.control))
                    return;

                MAST.Building.Visualizer.UpdateVisualizerPosition();
                MAST.Building.Placement.DrawBlockedHighlight();
                MAST.Building.Placement.DrawOccupancyCells();

                // Track the highlight as the mouse moves
                if (evt.type == EventType.MouseMove)
                    HandleUtility.Repaint();

                HandleInput(evt);
            }

            protected abstract void HandleInput(Event evt);
        }

        // Exactly one placement per click — no drag loop.  Only Draw
        // Continuous places while the mouse is held; it has a same-grid-cell
        // guard in Placement that Draw Single does not, so looping Draw
        // Single floods prefabs
        public sealed class DrawSingleTool : GridPlacementTool
        {
            protected override void HandleInput(Event evt)
            {
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    SceneInput.CaptureMouse();
                    MAST.Building.Placement.PlacePrefabInScene();
                }
            }
        }

        // Place while the mouse is held
        public sealed class DrawContinuousTool : GridPlacementTool
        {
            protected override void HandleInput(Event evt)
            {
                if (ToolState.ActiveBuildDrag != BuildDrag.Draw)
                    if (evt.type == EventType.MouseDown && evt.button == 0)
                    {
                        ToolState.ActiveBuildDrag = BuildDrag.Draw;
                        SceneInput.CaptureMouse();
                    }

                if (ToolState.ActiveBuildDrag == BuildDrag.Draw)
                {
                    MAST.Building.Placement.PlacePrefabInScene();

                    if (evt.type == EventType.MouseUp && evt.button == 0)
                        ToolState.CancelBuildDrag();
                }
            }
        }

        // Drag out an area, fill it on release
        public sealed class PaintAreaSceneTool : GridPlacementTool
        {
            protected override void HandleInput(Event evt)
            {
                if (ToolState.ActiveBuildDrag != BuildDrag.PaintArea)
                    if (evt.type == EventType.MouseDown && evt.button == 0)
                    {
                        ToolState.ActiveBuildDrag = BuildDrag.PaintArea;
                        MAST.Building.PaintArea.StartPaintArea();
                        SceneInput.CaptureMouse();
                    }

                if (ToolState.ActiveBuildDrag == BuildDrag.PaintArea)
                {
                    MAST.Building.PaintArea.UpdatePaintArea();

                    if (evt.type == EventType.MouseUp && evt.button == 0)
                    {
                        // Completing the area consumes it, so end the drag without
                        // the cancel path "which would delete the finished area"
                        MAST.Building.PaintArea.CompletePaintArea();
                        ToolState.ActiveBuildDrag = BuildDrag.None;
                    }
                }
            }
        }

        // One randomized placement per click "Placement rolls the next seed"
        public sealed class RandomizeTool : GridPlacementTool
        {
            protected override void HandleInput(Event evt)
            {
                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    SceneInput.CaptureMouse();
                    MAST.Building.Placement.PlacePrefabInScene();
                }
            }
        }

        // Greeble: snap to the face lattice under the pointer, one placement
        // per click.  Occupancy is ignored entirely "greebles attach to faces,
        // not cells" and the placed piece is tagged so it stays exempt
        public sealed class GreeblePlacementTool : ISceneTool
        {
            public void HandleSceneEvent(Event evt)
            {
                if (evt.type == EventType.MouseDown && (evt.alt || evt.control))
                    return;

                MAST.Building.GreebleTool.UpdateVisualizer();

                if (evt.type == EventType.MouseMove)
                    HandleUtility.Repaint();

                if (evt.type == EventType.MouseDown && evt.button == 0)
                {
                    SceneInput.CaptureMouse();

                    GameObject placedGreeble =
                        MAST.Building.Placement.PlacePrefabInScene(ignoreOccupancy: true);
                    if (placedGreeble != null)
                        MAST.Building.GreebleTool.MarkAsGreeble(placedGreeble);
                }
            }
        }

        // Eraser: raycast under the pointer, outline the target in red, delete
        // on click or while dragging "no grid involved".  SHIFT is a selection
        // guard here, unlike the placing tools
        public sealed class EraserSceneTool : ISceneTool
        {
            public void HandleSceneEvent(Event evt)
            {
                if (evt.type == EventType.MouseDown && (evt.alt || evt.control || evt.shift))
                    return;

                MAST.Building.Eraser.UpdateTarget(evt.mousePosition);
                MAST.Building.Eraser.DrawHighlight();

                if (evt.type == EventType.MouseMove)
                    HandleUtility.Repaint();

                if (ToolState.ActiveBuildDrag != BuildDrag.Erase)
                    if (evt.type == EventType.MouseDown && evt.button == 0)
                    {
                        // A fresh click may erase what the last erase revealed
                        // "the shield only tames single clicks and drags"
                        MAST.Building.Eraser.ClearShield();

                        ToolState.ActiveBuildDrag = BuildDrag.Erase;
                        SceneInput.CaptureMouse();
                    }

                if (ToolState.ActiveBuildDrag == BuildDrag.Erase)
                {
                    MAST.Building.Eraser.EraseTarget();

                    if (evt.type == EventType.MouseUp && evt.button == 0)
                        ToolState.CancelBuildDrag();
                }
            }
        }

        // ---------------------------------------------------------------------------
        // Paint tools
        // ---------------------------------------------------------------------------

        // Shared mouse-drag lifecycle for the paint and restore tools
        public abstract class PaintDragTool : ISceneTool
        {
            protected abstract PaintDrag Drag { get; }

            public abstract void HandleSceneEvent(Event evt);

            protected void HandlePaintDrag(Event evt)
            {
                // Don't start painting while a camera or selection modifier is held
                if (evt.type == EventType.MouseDown &&
                    (evt.alt || evt.shift || evt.control))
                    return;

                // If not already painting and the left mouse button pressed, start
                if (ToolState.ActivePaintDrag != Drag)
                    if (evt.type == EventType.MouseDown && evt.button == 0)
                    {
                        ToolState.ActivePaintDrag = Drag;
                        MAST.Painting.Painter.StartPainting();
                        SceneInput.CaptureMouse();
                    }

                // If painting and the left mouse button released, stop
                if (ToolState.ActivePaintDrag == Drag)
                {
                    if (evt.type == EventType.MouseUp && evt.button == 0)
                    {
                        MAST.Painting.Painter.StopPainting();
                        ToolState.CancelPaintDrag();
                    }
                }
            }
        }

        // Paint the selected material onto the face under the pointer
        public sealed class PaintMaterialTool : PaintDragTool
        {
            protected override PaintDrag Drag => PaintDrag.Paint;

            public override void HandleSceneEvent(Event evt)
            {
                // If no material is selected, exit without attempting to paint
                if (ToolState.SelectedMaterialIndex == -1)
                    return;

                // A stale index can resolve to no material "folder contents
                // changed, asset deleted" — passing null would silently flip
                // the tool into RESTORE mode
                Material selectedMaterial = MAST.Painting.Palette.MaterialPalette.GetSelectedMaterial();
                if (selectedMaterial == null)
                {
                    ToolState.DeselectMaterial();
                    return;
                }

                MAST.Painting.Painter.PreviewPaint(selectedMaterial);
                HandlePaintDrag(evt);
            }
        }

        // Restore the original material from the paint journal
        public sealed class RestoreMaterialTool : PaintDragTool
        {
            protected override PaintDrag Drag => PaintDrag.Restore;

            public override void HandleSceneEvent(Event evt)
            {
                // The restore tool works without a material selection
                if (ToolState.SelectedMaterialIndex != -1)
                    MAST.Painting.Palette.MaterialPalette.selectedItemIndex = -1;

                // Providing a null material triggers restore mode
                MAST.Painting.Painter.PreviewPaint((Material)null);
                HandlePaintDrag(evt);
            }
        }

        // Replace-all: click a face to swap every instance of its material
        // across the same MAST Holder "click, no drag"
        public sealed class ReplaceAllSceneTool : ISceneTool
        {
            public void HandleSceneEvent(Event evt)
            {
                if (ToolState.SelectedMaterialIndex == -1)
                    return;

                Material replacement = MAST.Painting.Palette.MaterialPalette.GetSelectedMaterial();
                if (replacement == null)
                {
                    ToolState.DeselectMaterial();
                    return;
                }

                // Same hover preview as painting — shows the target slot
                MAST.Painting.Painter.PreviewPaint(replacement);

                if (evt.type == EventType.MouseDown && evt.button == 0 &&
                    !evt.alt && !evt.shift && !evt.control)
                {
                    SceneInput.CaptureMouse();
                    MAST.Painting.Painter.ReplaceAllHoveredInHolder(replacement);
                }
            }
        }
    }
}
