using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;


namespace MAST
{
    namespace EditorUI
    {
        // UI Toolkit version of the Settings tab (Placement / GUI / Hotkeys sub-tabs).
        // Fields bind directly to the settings ScriptableObjects through SerializedObject,
        // which follows the editor theme and adds undo support for settings changes.
        public class SettingsTabView : VisualElement
        {
            // Persist sub-tab and foldout state for the session, like the IMGUI statics did
            private static int selectedSubTab = 0;
            private static bool occupancyFoldout = true;
            private static bool greebleFoldout = true;
            private static bool offsetFoldout = true;
            private static bool rotationFoldout = true;
            private static bool raycastFoldout = true;
            private static bool randomizerFoldout = true;
            private static bool toolbarFoldout = true;
            private static bool paletteFoldout = true;
            private static bool viewFoldout = true;
            private static bool gridFoldout = true;

            private Button[] subTabButtons;
            private VisualElement[] subTabPanels;
            private ObjectField targetParentField;

            public SettingsTabView()
            {
                // Find-only: opening the MAST window must not create objects in
                // the user's scene — the holder appears on first placement
                MAST.Building.Placement.FindExistingTargetParent();

                BuildContent();

                // The target parent can be deleted or recreated outside this view
                schedule.Execute(SyncTargetParent).Every(300);
            }

            private void BuildContent()
            {
                Clear();

                var subTabBar = new VisualElement();
                subTabBar.AddToClassList("mast-tabbar");
                Add(subTabBar);

                string[] captions = { "Placement", "GUI", "Hotkeys" };
                subTabButtons = new Button[captions.Length];
                for (int i = 0; i < captions.Length; i++)
                {
                    int index = i;
                    subTabButtons[i] = new Button(() => SelectSubTab(index)) { text = captions[i] };
                    subTabButtons[i].AddToClassList("mast-tab");
                    subTabBar.Add(subTabButtons[i]);
                }

                subTabPanels = new VisualElement[]
                {
                    BuildPlacementPanel(),
                    BuildGUIPanel(),
                    BuildHotkeyPanel()
                };

                foreach (var panel in subTabPanels)
                {
                    panel.AddToClassList("mast-tab-content");
                    Add(panel);
                }

                SelectSubTab(Mathf.Clamp(selectedSubTab, 0, subTabPanels.Length - 1));
            }

            private void SelectSubTab(int index)
            {
                selectedSubTab = index;
                for (int i = 0; i < subTabPanels.Length; i++)
                {
                    subTabPanels[i].style.display = (i == index) ? DisplayStyle.Flex : DisplayStyle.None;
                    subTabButtons[i].EnableInClassList("mast-tab--active", i == index);
                }
            }

            // ----------------------------------------------------------------------
            // Placement sub-tab
            // ----------------------------------------------------------------------
            private VisualElement BuildPlacementPanel()
            {
                var so = new SerializedObject(MAST.Settings.Data.placement);
                var scroll = new ScrollView(ScrollViewMode.Vertical);

                // Placement destination
                var destSection = NewSection(scroll);
                targetParentField = new ObjectField("Placement Destination")
                {
                    objectType = typeof(GameObject),
                    allowSceneObjects = true,
                    tooltip = "Drag a GameObject from the Hierarchy into this field.  It will be used as the parent of new placed models"
                };
                targetParentField.SetValueWithoutNotify(MAST.Building.Placement.targetParent);
                targetParentField.RegisterValueChangedCallback(evt =>
                {
                    MAST.Building.Placement.targetParent = evt.newValue as GameObject;
                    ApplyTargetParentTag();
                });
                destSection.Add(targetParentField);

                // Occupancy "overlap blocking and face-adjacent placement"
                var occupancyFold = NewFoldout(scroll, "Occupancy Settings", occupancyFoldout, value => occupancyFoldout = value);
                occupancyFold.Add(Prop(so, "enableOccupancy", "Enable Occupancy"));
                occupancyFold.Add(Prop(so, "placeOnFaces", "Place Against Faces"));
                occupancyFold.Add(Prop(so, "showOccupancyCells", "Show Occupancy Cells"));
                occupancyFold.Add(Prop(so, "shiftPlacement", "SHIFT Placement"));
                occupancyFold.Add(Prop(so, "occupancyMargin", "Cell Claim Margin"));

                // Greeble "face-lattice placement"
                var greebleFold = NewFoldout(scroll, "Greeble Settings", greebleFoldout, value => greebleFoldout = value);
                greebleFold.Add(Prop(so, "greeble.divisions", "Divisions"));
                greebleFold.Add(Prop(so, "greeble.outwardAxis", "Outward Axis"));

                // Offset
                var offsetFold = NewFoldout(scroll, "Offset Settings", offsetFoldout, value => offsetFoldout = value);
                offsetFold.Add(Prop(so, "overridePrefabOffset", "Override Prefab Component"));
                offsetFold.Add(Prop(so, "offset.pos", "Position Offset"));

                // Rotation
                var rotationFold = NewFoldout(scroll, "Rotation Settings", rotationFoldout, value => rotationFoldout = value);
                rotationFold.Add(Prop(so, "overridePrefabRotation", "Override Prefab Component"));
                rotationFold.Add(Prop(so, "rotation.step", "Rotation Step"));
                rotationFold.Add(Prop(so, "rotation.rotateWithHolder", "Rotate With Holder"));

                // Placement raycast
                var raycastFold = NewFoldout(scroll, "Raycast Settings", raycastFoldout, value => raycastFoldout = value);
                raycastFold.Add(Prop(so, "overridePrefabRaycast", "Override Prefab Component"));
                raycastFold.Add(Prop(so, "placementRaycast.useRaycast", "Use Placement Raycast"));
                raycastFold.Add(Prop(so, "placementRaycast.direction", "Direction Vector"));
                raycastFold.Add(Prop(so, "placementRaycast.startOffset", "Raycast Start Offset"));

                // Randomizer
                var randomizerFold = NewFoldout(scroll, "Randomizer Settings", randomizerFoldout, value => randomizerFoldout = value);
                // Section headers and tooltips come from the [Header]/[Tooltip]
                // attributes on the Randomizer data class; field order follows the
                // class so each header groups the fields it belongs to.
                randomizerFold.Add(Prop(so, "overridePrefabRandomizer", "Override Prefab Component"));
                randomizerFold.Add(Prop(so, "randomizer.useRandomizer", "Use Randomizer"));
                randomizerFold.Add(Prop(so, "randomizer.rotateMin", "Minimum"));
                randomizerFold.Add(Prop(so, "randomizer.rotateMax", "Maximum"));
                randomizerFold.Add(Prop(so, "randomizer.rotateStep", "Step (Degree increments)"));
                randomizerFold.Add(Prop(so, "randomizer.scaleMin", "Minimum (1 = no scaling)"));
                randomizerFold.Add(Prop(so, "randomizer.scaleMax", "Maximum"));
                randomizerFold.Add(Prop(so, "randomizer.scaleLock", "Lock"));
                randomizerFold.Add(Prop(so, "randomizer.flipX", "X"));
                randomizerFold.Add(Prop(so, "randomizer.flipY", "Y"));
                randomizerFold.Add(Prop(so, "randomizer.flipZ", "Z"));
                randomizerFold.Add(Prop(so, "randomizer.posMin", "Minimum (0 = no offset)"));
                randomizerFold.Add(Prop(so, "randomizer.posMax", "Maximum"));

                scroll.Bind(so);
                scroll.TrackSerializedObjectValue(so, _ => OnPreferencesChanged());
                return scroll;
            }

            // ----------------------------------------------------------------------
            // GUI sub-tab
            // ----------------------------------------------------------------------
            private VisualElement BuildGUIPanel()
            {
                var so = new SerializedObject(MAST.Settings.Data.gui);
                var scroll = new ScrollView(ScrollViewMode.Vertical);

                var toolbarFold = NewFoldout(scroll, "Toolbar Settings", toolbarFoldout, value => toolbarFoldout = value);
                toolbarFold.Add(Prop(so, "toolbar.position", "Position"));
                toolbarFold.Add(BoundSlider("Scale", 0.5f, 1f, "toolbar.scale"));
                toolbarFold.Add(Prop(so, "toolbar.iconColor", "Icon Color"));

                var paletteFold = NewFoldout(scroll, "Palette Settings", paletteFoldout, value => paletteFoldout = value);
                paletteFold.Add(Prop(so, "palette.bgColor", "Background Color"));
                var pitch = BoundSlider("Camera Pitch (0-360)", 0f, 360f, "palette.snapshotCameraPitch");
                pitch.tooltip = "Rotation around the Y axis";
                paletteFold.Add(pitch);
                var yaw = BoundSlider("Camera Yaw (0-90)", 0f, 90f, "palette.snapshotCameraYaw");
                yaw.tooltip = "Rotation around the X axis";
                paletteFold.Add(yaw);
                var lightPitch = BoundSlider("Light Pitch (0-360)", 0f, 360f, "palette.snapshotLightPitch");
                lightPitch.tooltip = "Key light rotation around the Y axis, relative to the camera view.  The light always aims at the model's center";
                paletteFold.Add(lightPitch);
                var lightYaw = BoundSlider("Light Yaw (0-90)", 0f, 90f, "palette.snapshotLightYaw");
                lightYaw.tooltip = "Key light rotation around the X axis, relative to the camera view";
                paletteFold.Add(lightYaw);
                paletteFold.Add(Prop(so, "palette.snapshotLightColor", "Light Color"));
                paletteFold.Add(Prop(so, "palette.snapshotAmbientColor", "Ambient Color"));
                paletteFold.Add(Prop(so, "palette.overwriteThumbnails", "Recreate Thumbnails when Loading from Folder"));
                var thumbnailSize = new SliderInt("Thumbnail Size", 32, 512)
                {
                    bindingPath = "palette.thumbnailSize",
                    showInputField = true,
                    tooltip = "Square resolution for prefab and material thumbnails.  Applies when thumbnails are (re)generated"
                };
                paletteFold.Add(thumbnailSize);

                var viewFold = NewFoldout(scroll, "View Settings", viewFoldout, value => viewFoldout = value);
                viewFold.Add(Prop(so, "view.hidePrefabsAbove", "Hide Prefabs Above Grid"));
                var hideOffset = Prop(so, "view.hideOffset", "Hide Offset");
                hideOffset.tooltip = "Prefabs with pivots above the current grid level plus this offset are hidden while the toggle is on";
                viewFold.Add(hideOffset);

                var gridFold = NewFoldout(scroll, "Grid Settings", gridFoldout, value => gridFoldout = value);
                gridFold.Add(BoldLabel("Grid Dimensions"));
                var xz = Prop(so, "grid.xzUnitSize", "X/Z Unit Size");
                xz.tooltip = "Size of an individual grid square for snapping";
                gridFold.Add(xz);
                var y = Prop(so, "grid.yUnitSize", "Y Unit Size");
                y.tooltip = "Y step for grid raising/lowering";
                gridFold.Add(y);
                var count = Prop(so, "grid.cellCount", "Count (Center to Edge)");
                count.tooltip = "Count of squares from center to each edge";
                gridFold.Add(count);
                var gridLayer = Prop(so, "grid.gridLayer", "Grid Layer");
                gridLayer.tooltip = "Layer used by MAST's grid plane — pick one your project doesn't use.  Toggle the grid off and on after changing";
                gridFold.Add(gridLayer);
                gridFold.Add(BoldLabel("Grid Cosmetics"));
                gridFold.Add(Prop(so, "grid.tintColor", "Tint Color"));

                scroll.Bind(so);
                scroll.TrackSerializedObjectValue(so, _ => OnPreferencesChanged());
                return scroll;
            }

            // ----------------------------------------------------------------------
            // Hotkeys sub-tab
            // ----------------------------------------------------------------------
            private VisualElement BuildHotkeyPanel()
            {
                var so = new SerializedObject(MAST.Settings.Data.hotkey);
                var scroll = new ScrollView(ScrollViewMode.Vertical);

                (string title, string keyProp, string modProp)[] hotkeys =
                {
                    ("Toggle Grid On/Off", "toggleGridKey", "toggleGridMod"),
                    ("Move Grid Up", "moveGridUpKey", "moveGridUpMod"),
                    ("Move Grid Down", "moveGridDownKey", "moveGridDownMod"),
                    ("Deselect Draw Tool and Palette Selection", "deselectPrefabKey", "deselectPrefabMod"),
                    ("Select Draw Single Tool", "drawSingleKey", "drawSingleMod"),
                    ("Select Draw Continuous Tool", "drawContinuousKey", "drawContinuousMod"),
                    ("Select Paint Square Tool", "paintSquareKey", "paintSquareMod"),
                    ("Select Randomizer Tool", "randomizerKey", "randomizerMod"),
                    ("Select Erase Tool", "eraseKey", "eraseMod"),
                    ("Select Greeble Tool", "greebleKey", "greebleMod"),
                    ("Toggle Hide Prefabs Above", "hideAboveKey", "hideAboveMod"),
                    ("Generate New Random(izer) Seed", "newRandomSeedKey", "newRandomSeedMod"),
                    ("Rotate Prefab", "rotatePrefabKey", "rotatePrefabMod"),
                    ("Flip Prefab", "flipPrefabKey", "flipPrefabMod"),
                    ("Paint Material", "paintMaterialKey", "paintMaterialMod"),
                    ("Restore Material", "restoreMaterialKey", "restoreMaterialMod")
                };

                foreach (var (title, keyProp, modProp) in hotkeys)
                {
                    var section = NewSection(scroll);
                    section.Add(BoldLabel(title));
                    section.Add(Prop(so, keyProp, "Key"));

                    // Flags enum — the dropdown multi-selects SHIFT/CTRL/ALT
                    var modifiers = Prop(so, modProp, "Modifiers");
                    modifiers.tooltip = "Any combination of SHIFT, CTRL, and ALT.  Matching is exact: the hotkey only fires when exactly these modifiers are held";
                    section.Add(modifiers);
                }

                scroll.Bind(so);
                scroll.TrackSerializedObjectValue(so, _ => MAST.Settings.Data.RequestSave());
                return scroll;
            }

            // ----------------------------------------------------------------------
            // Shared pieces
            // ----------------------------------------------------------------------

            // Same effect as the old IMGUI change check: update the grid and
            // repaint — plus a debounced save, so an editor crash no longer
            // loses every settings change since the window last closed
            private static void OnPreferencesChanged()
            {
                MAST.Building.GridManager.UpdateGridSettings();
                MAST.Building.ViewFilter.Apply();
                MAST.LoadingHelper.RefreshIconTint();
                MAST.Settings.Data.RequestSave();
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            private void SyncTargetParent()
            {
                // FIND an existing holder only — creating one here "and
                // resurrecting it on a 300 ms timer after the user deleted it"
                // dirtied scenes just for opening the window.  The holder is
                // created lazily on first placement instead
                if (MAST.Building.Placement.targetParent == null)
                    MAST.Building.Placement.FindExistingTargetParent();

                if (targetParentField != null &&
                    !ReferenceEquals(targetParentField.value, MAST.Building.Placement.targetParent))
                    targetParentField.SetValueWithoutNotify(MAST.Building.Placement.targetParent);
            }

            // Move the target-parent tag to the newly assigned GameObject
            private static void ApplyTargetParentTag()
            {
                if (MAST.Building.Placement.targetParent == null)
                    return;

                // Make sure the tag exists FIRST — the old broad catch created
                // it and returned, so the tag was never actually applied on the
                // first run and the chosen holder got abandoned later
                MAST.TagsAndLayers.AddTag(MAST.Const.Placement.defaultTargetParentTag);

                if (MAST.Building.Placement.targetParent.tag != MAST.Const.Placement.defaultTargetParentTag)
                {
                    GameObject[] taggedGameObjects =
                        GameObject.FindGameObjectsWithTag(MAST.Const.Placement.defaultTargetParentTag);
                    foreach (GameObject taggedGameObject in taggedGameObjects)
                        taggedGameObject.tag = "Untagged";

                    MAST.Building.Placement.targetParent.tag = MAST.Const.Placement.defaultTargetParentTag;
                }
            }

            private static PropertyField Prop(SerializedObject so, string propertyPath, string label)
            {
                return new PropertyField(so.FindProperty(propertyPath), label);
            }

            private static Slider BoundSlider(string label, float min, float max, string bindingPath)
            {
                var slider = new Slider(label, min, max) { bindingPath = bindingPath, showInputField = true };
                return slider;
            }

            private static Label BoldLabel(string text)
            {
                var label = new Label(text);
                label.AddToClassList("mast-bold-label");
                return label;
            }

            private static VisualElement NewSection(VisualElement parent)
            {
                var section = new VisualElement();
                section.AddToClassList("mast-section");
                parent.Add(section);
                return section;
            }

            private static Foldout NewFoldout(VisualElement parent, string title, bool startOpen,
                System.Action<bool> onToggled)
            {
                var section = NewSection(parent);
                var foldout = new Foldout { text = title, value = startOpen };
                foldout.RegisterValueChangedCallback(evt => onToggled(evt.newValue));
                section.Add(foldout);
                return foldout;
            }
        }
    }
}
