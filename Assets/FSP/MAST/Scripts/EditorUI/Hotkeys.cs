using System;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace EditorUI
    {
        // MAST hotkeys, redesigned:
        // - Modifiers are FLAGS — any combination of SHIFT, CTRL, and ALT
        // - Matching is EXACT: SHIFT+W fires only when shift alone is held,
        //   never on CTRL+SHIFT+W "the old matcher only looked at shift"
        // - A matched key is CONSUMED, so Unity's own scene-view bindings
        //   don't also fire "F no longer frames the selection when it's
        //   bound to Flip"
        // - One declarative binding table instead of a copy-pasted block per
        //   hotkey; the first matching binding wins
        //
        // Hotkeys fire while the Scene view or the MAST window has focus.
        //
        // Scene-view keys are processed through the editor's GLOBAL event
        // hook, not the scene GUI pass: keys reaching duringSceneGui proved
        // unreliable "overlays, tools, and focus changes could consume them
        // first, and delivery even changed with console activity".  The global
        // hook sees every key before window-level dispatch can eat it
        public class Hotkeys
        {
            // ------------------------------------------------------------------
            // Global hook (scene-view scope)
            // ------------------------------------------------------------------

            private static readonly Hotkeys globalInstance = new Hotkeys();

            public static bool GlobalHookInstalled { get; private set; }

            [InitializeOnLoadMethod]
            private static void InstallGlobalHook()
            {
                // globalEventHandler is internal but stable across editor
                // versions.  If it ever disappears, MAST falls back to the
                // duringSceneGui path automatically
                System.Reflection.FieldInfo field = typeof(EditorApplication).GetField(
                    "globalEventHandler",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                if (field == null)
                    return;

                var handler = (EditorApplication.CallbackFunction)field.GetValue(null);
                handler += ProcessGlobalEvent;
                field.SetValue(null, handler);
                GlobalHookInstalled = true;
            }

            private static void ProcessGlobalEvent()
            {
                // Scene-view scope only: the MAST window handles its own keys
                // "UI Toolkit path", and every other window keeps its keys
                var sceneView = EditorWindow.focusedWindow as SceneView;
                if (sceneView == null)
                    return;

                // Typing guard, done DETERMINISTICALLY: suppress only while
                // the scene view's focused element really is a text field
                // "its search box".  The IMGUI editingTextField flag is NOT
                // consulted here — it latches on after unrelated text
                // interactions and silently classified every key as typing
                var focused = sceneView.rootVisualElement?.focusController?.focusedElement
                    as UnityEngine.UIElements.VisualElement;
                if (focused != null && IsInsideTextInput(focused))
                    return;

                // Pass the event explicitly: the supplied-event path skips the
                // IMGUI typing flags entirely
                if (globalInstance.ProcessHotkeys(Event.current))
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            }

            // Is this element part of an editable text field?  Every text-based
            // field "TextField, IntegerField, FloatField…" carries the
            // unity-base-text-field class on its root
            public static bool IsInsideTextInput(UnityEngine.UIElements.VisualElement element)
            {
                for (var current = element; current != null; current = current.parent)
                {
                    if (current.ClassListContains("unity-base-text-field"))
                        return true;
                }

                return false;
            }
            private struct Binding
            {
                public Func<KeyCode> key;
                public Func<HotkeyModifier> modifiers;
                public Func<bool> execute;   // returns true when it changed something

                public Binding(Func<KeyCode> key, Func<HotkeyModifier> modifiers, Func<bool> execute)
                {
                    this.key = key;
                    this.modifiers = modifiers;
                    this.execute = execute;
                }
            }

            private Binding[] bindings;

            // The table reads the settings through delegates, so rebinds in the
            // Settings tab apply immediately
            private Binding[] Bindings
            {
                get
                {
                    if (bindings != null)
                        return bindings;

                    var hotkey = new Func<MAST.Settings.ScriptObj.HotkeySettings>(() => MAST.Settings.Data.hotkey);

                    bindings = new[]
                    {
                        new Binding(() => hotkey().toggleGridKey, () => hotkey().toggleGridMod, () =>
                        {
                            MAST.Building.GridManager.gridExists = !MAST.Building.GridManager.gridExists;
                            MAST.Building.GridManager.ChangeGridVisibility();
                            return true;
                        }),

                        new Binding(() => hotkey().moveGridUpKey, () => hotkey().moveGridUpMod, () =>
                        {
                            MAST.Building.GridManager.MoveGridUp();
                            return true;
                        }),

                        new Binding(() => hotkey().moveGridDownKey, () => hotkey().moveGridDownMod, () =>
                        {
                            MAST.Building.GridManager.MoveGridDown();
                            return true;
                        }),

                        new Binding(() => hotkey().deselectPrefabKey, () => hotkey().deselectPrefabMod, () =>
                        {
                            switch (MAST.EditorUI.DataManager.state.selectedInterfaceTab)
                            {
                                case 0:
                                    MAST.EditorUI.ToolState.DeselectPrefab();
                                    return true;
                                case 1:
                                    MAST.EditorUI.ToolState.DeselectMaterial(clearAnyPaintTool: true);
                                    return true;
                            }
                            return false;
                        }),

                        // Toggle draw tools "selecting the active tool again deselects it"
                        new Binding(() => hotkey().drawSingleKey, () => hotkey().drawSingleMod, () =>
                        {
                            MAST.EditorUI.ToolState.ToggleBuildTool(BuildMode.DrawSingle);
                            return true;
                        }),

                        new Binding(() => hotkey().drawContinuousKey, () => hotkey().drawContinuousMod, () =>
                        {
                            MAST.EditorUI.ToolState.ToggleBuildTool(BuildMode.DrawContinuous);
                            return true;
                        }),

                        new Binding(() => hotkey().paintSquareKey, () => hotkey().paintSquareMod, () =>
                        {
                            MAST.EditorUI.ToolState.ToggleBuildTool(BuildMode.PaintArea);
                            return true;
                        }),

                        new Binding(() => hotkey().randomizerKey, () => hotkey().randomizerMod, () =>
                        {
                            MAST.EditorUI.ToolState.ToggleBuildTool(BuildMode.Randomize);
                            return true;
                        }),

                        new Binding(() => hotkey().eraseKey, () => hotkey().eraseMod, () =>
                        {
                            MAST.EditorUI.ToolState.ToggleBuildTool(BuildMode.Erase);
                            return true;
                        }),

                        new Binding(() => hotkey().greebleKey, () => hotkey().greebleMod, () =>
                        {
                            MAST.EditorUI.ToolState.ToggleBuildTool(BuildMode.Greeble);
                            return true;
                        }),

                        new Binding(() => hotkey().hideAboveKey, () => hotkey().hideAboveMod, () =>
                        {
                            MAST.Building.ViewFilter.Toggle();
                            return true;
                        }),

                        new Binding(() => hotkey().newRandomSeedKey, () => hotkey().newRandomSeedMod, () =>
                        {
                            MAST.Building.Randomizer.GenerateNewRandomSeed();
                            return true;
                        }),

                        new Binding(() => hotkey().rotatePrefabKey, () => hotkey().rotatePrefabMod, () =>
                        {
                            MAST.Building.Manipulate.RotateObject();
                            return true;
                        }),

                        new Binding(() => hotkey().flipPrefabKey, () => hotkey().flipPrefabMod, () =>
                        {
                            MAST.Building.Manipulate.FlipObject();
                            return true;
                        }),

                        new Binding(() => hotkey().paintMaterialKey, () => hotkey().paintMaterialMod, () =>
                        {
                            MAST.EditorUI.ToolState.TogglePaintTool(PaintTool.Paint);
                            return true;
                        }),

                        new Binding(() => hotkey().restoreMaterialKey, () => hotkey().restoreMaterialMod, () =>
                        {
                            MAST.EditorUI.ToolState.TogglePaintTool(PaintTool.Restore);
                            return true;
                        })
                    };

                    return bindings;
                }
            }

            // suppliedEvent lets non-IMGUI callers (the UI Toolkit window shell) pass the
            // event explicitly; IMGUI callers omit it and Event.current is used as before.
            public bool ProcessHotkeys(Event suppliedEvent = null)
            {
                Event currentEvent = suppliedEvent ?? Event.current;

                EventType eventType;

                if (suppliedEvent != null)
                {
                    eventType = suppliedEvent.type;
                }
                else
                {
                    // Only suppress hotkeys while the user is genuinely TYPING
                    // in an IMGUI text field "scene-view search box".  Checking
                    // keyboardControl != 0 was too blunt: the scene view itself
                    // can hold keyboard control after ordinary clicks, which
                    // silenced every hotkey — the same state also broke the old
                    // GetTypeForControl filtering.
                    // The editingTextField flag alone isn't trustworthy either:
                    // it can STICK ON after popup windows with text fields close
                    // "the color picker's hex field" — genuine typing also holds
                    // a nonzero keyboardControl, so require both
                    if (EditorGUIUtility.editingTextField && GUIUtility.keyboardControl != 0)
                        return false;

                    eventType = currentEvent.type;

                    // Something earlier in the scene view's GUI pass "built-in
                    // tools, overlays, focus grabs after a click" may have Used
                    // the key event before MAST sees it, which reported Used
                    // instead of KeyDown and silently skipped the hotkey —
                    // the unreliability depended on what happened to be active.
                    // rawType still carries the original type: a key the user
                    // deliberately bound in MAST fires no matter who else
                    // glanced at the event
                    if (eventType == EventType.Used)
                        eventType = currentEvent.rawType;
                }

                if (eventType != EventType.KeyDown || currentEvent.keyCode == KeyCode.None)
                    return false;

                if (MAST.Settings.Data.hotkey == null)
                    return false;

                foreach (Binding binding in Bindings)
                {
                    if (!Matches(currentEvent, binding.key(), binding.modifiers()))
                        continue;

                    bool changeMade = binding.execute();

                    // Consume the key so Unity's own bindings don't also react —
                    // but only when MAST actually did something with it: a no-op
                    // match "deselect on the Settings tab" must not eat the key
                    if (changeMade)
                        currentEvent.Use();

                    return changeMade;
                }

                return false;
            }

            // Exact modifier matching: what is held must equal what is bound
            private static bool Matches(Event currentEvent, KeyCode key, HotkeyModifier boundModifiers)
            {
                if (currentEvent.keyCode != key)
                    return false;

                HotkeyModifier held = HotkeyModifier.NONE;
                if (currentEvent.shift) held |= HotkeyModifier.SHIFT;
                if (currentEvent.control) held |= HotkeyModifier.CTRL;
                if (currentEvent.alt) held |= HotkeyModifier.ALT;

                return held == boundModifiers;
            }
        }
    }
}
