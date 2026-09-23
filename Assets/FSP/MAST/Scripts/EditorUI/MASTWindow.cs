using System;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;


namespace MAST
{
    namespace EditorUI
    {
        [Serializable]
        public class MASTWindow : EditorWindow
        {
            // ---------------------------------------------------------------------------
            // Add menu named "Open MAST Palette" to the Window menu
            // ---------------------------------------------------------------------------
            [MenuItem("Tools/MAST/Open MAST Window", false, 16)]
            private static void ShowWindow()
            {
                // Get existing open window or if none, make a new one:
                EditorWindow.GetWindow(typeof(MASTWindow), false, "MAST").Show();
            }
            
        // ---------------------------------------------------------------------------
        #region Variable Declaration
        // ---------------------------------------------------------------------------
            
            // ------------------------------
            // Persisent Class Variables
            // ------------------------------
            
            // Initialize Hotkeys Class if needed and return HotKeysClass
            [SerializeField] private MAST.EditorUI.Hotkeys HotKeysClass;
            private MAST.EditorUI.Hotkeys HotKeys
            {
                get
                {
                    if(HotKeysClass == null)
                        HotKeysClass = new MAST.EditorUI.Hotkeys();
                    return HotKeysClass;
                }
            }
            
            // ------------------------------
            // Editor Window Variables
            // ------------------------------
            [SerializeField] private bool inPlayMode = false;
            [SerializeField] private bool isCleanedUp = false;

            // ------------------------------
            // UI Toolkit Tab Variables
            // ------------------------------
            private Button[] tabButtons;
            private VisualElement[] tabContents;

        #endregion
            
            // ---------------------------------------------------------------------------
            // Perform Initializations
            // ---------------------------------------------------------------------------
            void Awake() // This runs on the first time open
            {
                //Debug.Log("Interface - Awake");
            }
            
            void OnFocus()
            {
                // Re-arm window hotkeys: keyboard events need a focused element,
                // and regaining window focus doesn't always restore one
                if (rootVisualElement != null && rootVisualElement.focusable &&
                    rootVisualElement.focusController?.focusedElement == null)
                    rootVisualElement.Focus();
            }
            
            // ---------------------------------------------------------------------------
            // MAST Window is Enabled
            // ---------------------------------------------------------------------------
            private void OnEnable()
            {
                
                //Debug.Log("Interface - On Enable");
                
                // Initialize Preference Manager
                MAST.Settings.Data.Initialize();

                // Initialize the MAST State Manager
                MAST.EditorUI.DataManager.Initialize();

                // Point MAST's editor materials at the shaders for the active
                // render pipeline "URP by default, Built-in adapted automatically"
                MAST.RenderPipelineUtil.ApplyPipelineShaders();
                
                // Set up delegates so that OnScene is called automatically
                SceneView.duringSceneGui -= this.OnScene;
                SceneView.duringSceneGui += this.OnScene;
                
                // Set up deletegates for exiting editor mode and returning to editor mode from play mode
                MAST.EditorUI.PlayModeStateListener.onExitEditMode -= this.ExitEditMode;
                MAST.EditorUI.PlayModeStateListener.onExitEditMode += this.ExitEditMode;
                MAST.EditorUI.PlayModeStateListener.onEnterEditMode -= this.EnterEditMode;
                MAST.EditorUI.PlayModeStateListener.onEnterEditMode += this.EnterEditMode;
                
                // Set scene to be updated by mousemovement
                wantsMouseMove = true;

                // inPlayMode is a serialized field that defaults to false, so a
                // window opened DURING play would wrongly take the edit-mode
                // restore path — ask the editor, not the field
                inPlayMode = EditorApplication.isPlayingOrWillChangePlaymode;

                // If Enabled in Editor Mode
                if (!inPlayMode)
                {
                    // Load interface data back from saved state
                    MAST.EditorUI.DataManager.Load_Interface_State();
                    
                    // Create a new grid if needed
                    if (MAST.EditorUI.DataManager.state.gridExists)
                    {
                        MAST.Building.GridManager.gridExists = true;
                        MAST.Building.GridManager.ChangeGridVisibility();
                    }
                    
                    // Defer the palette restore one editor tick: the full
                    // synchronous folder scan used to hold the "Hold on" busy
                    // dialog through every window open and recompile.  Palettes
                    // restore BEFORE the placement mode — the saved mode may
                    // reference a selected palette item
                    EditorApplication.delayCall += () =>
                    {
                        // The window can close before the delayed call runs
                        if (this == null)
                            return;

                        MAST.EditorUI.DataManager.Restore_Palette_Items();

                        // Change placement mode back to what was saved.  ToolState maps the
                        // saved index to a BuildMode "the old direct cast was off by one"
                        MAST.Building.ToolTransitions.ApplyBuildToolChange(ToolState.ActiveBuildTool);
                    };
                }
                
                // If Enabled in Run Mode
                else
                {
                    // Nothing so far, because everything is being triggered in ExitEditMode event method
                }
            }
            
            // ---------------------------------------------------------------------------
            // Save and Restore MAST Interface variables to keep state on play
            // ---------------------------------------------------------------------------
            private void ExitEditMode()
            {
                //Debug.Log("Interface - Exit Edit Mode");

                // Don't allow this method to run twice
                if (inPlayMode)
                    return;

                // Do the FULL save + cleanup here, not in OnDisable: with
                // "Reload Domain" disabled, entering play mode never fires
                // OnDisable, and the visualizer used to survive into the game
                bool restoreGrid = MAST.Building.GridManager.gridExists;

                MAST.EditorUI.DataManager.Save_Interface_State();
                MAST.EditorUI.DataManager.Save_Palette_Items();

                CleanUpInterface();

                // Make sure the grid is restored after returning to editor
                MAST.Building.GridManager.gridExists = restoreGrid;

                inPlayMode = true;
                isCleanedUp = true;
            }

            private void EnterEditMode()
            {
                //Debug.Log("Interface - Enter Edit Mode");
                
                // Don't allow this method to run twice
                if (!inPlayMode)
                    return;
                
                // Load interface data back from saved state
                MAST.EditorUI.DataManager.Load_Interface_State();
                MAST.Building.GridManager.ChangeGridVisibility();
                
                // Restore the interface saved state
                MAST.EditorUI.DataManager.Restore_Palette_Items();
                
                // Change placement mode back to what was saved.  ToolState maps the
                // saved index to a BuildMode "the old direct cast was off by one"
                MAST.Building.ToolTransitions.ApplyBuildToolChange(ToolState.ActiveBuildTool);

                // Repaint all views
                UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                
                inPlayMode = false;
            }
            
            // ---------------------------------------------------------------------------
            // Perform Cleanup when MAST Window is Disabled
            // ---------------------------------------------------------------------------
            private void OnDisable()
            {
                //Debug.Log("Interface - On Disable");
                
                // Save MAST Settings to Scriptable Objects
                MAST.Settings.Data.Save_Settings();
                
                // If OnDisable triggered by going fullscreen, closing MAST, or changing scenes
                if (!inPlayMode)
                {
                    MAST.EditorUI.DataManager.Save_Interface_State();

                    // Folder choices used to survive only the play-mode path,
                    // silently reverting after a normal close and reopen
                    MAST.EditorUI.DataManager.Save_Palette_Items();
                }
                
                // If OnDisable is triggered by the user hitting play button
                else
                {
                    // If cleanup hasn't already ocurred
                    if (!isCleanedUp)
                    {
                        // Load interface and palette data state
                        MAST.EditorUI.DataManager.Save_Interface_State();
                        MAST.EditorUI.DataManager.Save_Palette_Items();
                        
                        CleanUpInterface();
                        
                        isCleanedUp = true;
                    }
                }
                
                // Remove SceneView delegate
                SceneView.duringSceneGui -= this.OnScene;

                // Unsubscribe the play-mode listeners "static events — a closed
                // window's handlers used to keep firing forever and could even
                // respawn a visualizer with no window open"
                MAST.EditorUI.PlayModeStateListener.onExitEditMode -= this.ExitEditMode;
                MAST.EditorUI.PlayModeStateListener.onEnterEditMode -= this.EnterEditMode;
            }

            // ---------------------------------------------------------------------------
            // Perform Cleanup when MAST Window is Closed
            // ---------------------------------------------------------------------------
            private void OnDestroy()
            {
                // A real close "not a dock/maximize toggle, which only fires
                // OnDisable" removes everything MAST put in the scene: the
                // visualizer used to keep rendering "and saving" invisibly
                CleanUpInterface();
            }
            
            // ---------------------------------------------------------------------------
            // Clean-up
            // ---------------------------------------------------------------------------
            private void CleanUpInterface()
            {
                //Debug.Log("Cleaning Up Interface");

                // Delete placement grid
                MAST.Building.GridManager.DestroyGrid();

                // Deselect tools and palette items, cancel drags, remove visualizer/paint area
                ToolState.ClearAll();

                // Restore anything hidden by the hide-above view filter
                MAST.Building.ViewFilter.ShowAll();
            }
            
            // ---------------------------------------------------------------------------
            // Runs every frame
            // ---------------------------------------------------------------------------
            private void Update()
            {
                
            }
            
        // ---------------------------------------------------------------------------
        #region SceneView
        // ---------------------------------------------------------------------------
            private void OnScene(SceneView sceneView)
            {
                // MAST is an edit-mode tool: during play the scene hotkeys and
                // placement pipeline are off "clicking used to place prefabs
                // into the running game, silently lost on Stop"
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    return;

                // The grid is a plane of the MAST Holder's local space — keep it
                // glued to the holder as it moves or rotates
                MAST.Building.GridManager.FollowHolder();

                // Handle SceneView GUI
                SceneviewGUI(sceneView);
                
                // Handle view focus
                ProcessMouseEnterLeaveSceneview();

                // Release the mouse capture a tool click grabbed "rawType also
                // sees the MouseUp when the event was consumed"
                if (Event.current.rawType == EventType.MouseUp)
                    SceneInput.ReleaseMouseIfCaptured();
                
                // Scene-view hotkeys run through the global editor event hook
                // "reliable regardless of what consumes events inside the scene
                // GUI pass".  This inline path is only the fallback for editor
                // versions without the hook — never both, or keys double-fire
                if (!Hotkeys.GlobalHookInstalled && HotKeys.ProcessHotkeys())
                {
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
                
                // Route the event to the active tool's handler "one class per
                // tool in SceneTools.cs — the window stays a thin event pump"
                switch (MAST.EditorUI.DataManager.state.selectedInterfaceTab)
                {
                    case 0:
                        SceneToolRouter.HandleBuildEvent(Event.current);
                        break;
                    case 1:
                        SceneToolRouter.HandlePaintEvent(Event.current);
                        break;
                }
            }
            
            // Handle SceneView GUI
            private void SceneviewGUI(SceneView sceneView)
            {
                bool scrollWheelUsed = false;

                // If SHIFT key is held down, and the BUILD tab is active "cycling
                // used to hijack shift+scroll mid-painting and arm a build tool"
                if (Event.current.shift && MAST.EditorUI.DataManager.state.selectedInterfaceTab == 0)
                {
                    // If mouse scrollwheel was used, cycle the palette selection
                    if (Event.current.type == EventType.ScrollWheel && Event.current.delta.y != 0)
                    {
                        ToolState.CyclePrefabSelection(Event.current.delta.y > 0 ? 1 : -1);
                        scrollWheelUsed = true;
                    }
                }

                // If successfully scrolled wheel
                if (scrollWheelUsed)
                {
                    // Repaint all views
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

                    // Consume the scroll so the scene view doesn't also zoom
                    Event.current.Use();
                }
                
                
            }
            
            // Handle events when mouse point enter or leaves the SceneView
            void ProcessMouseEnterLeaveSceneview()
            {
                // If mouse enters SceneView window, show visualizer
                if (Event.current.type == EventType.MouseEnterWindow)
                    MAST.Building.Visualizer.SetVisualizerVisibility(true);
                
                // If mouse leaves SceneView window
                else if (Event.current.type == EventType.MouseLeaveWindow)
                {
                    // Hide visualizer
                    MAST.Building.Visualizer.SetVisualizerVisibility(false);

                    // Stop any in-progress drawing, area painting, or erasing
                    ToolState.CancelBuildDrag();

                    // End any paint session and take the hover preview off the
                    // scene "it used to stay really-applied while the mouse was
                    // over other panels, then bake in on a save or recompile"
                    MAST.Painting.Painter.StopPainting();
                    ToolState.CancelPaintDrag();
                    MAST.Painting.Painter.ClearCurrentMaterialPaintPreview();
                }
            }
            
        #endregion
            
        // ---------------------------------------------------------------------------
        #region Custom Editor Window Interface (UI Toolkit)
        // ---------------------------------------------------------------------------
            private void CreateGUI()
            {
                // CreateGUI normally runs after OnEnable, but initialize defensively
                MAST.Settings.Data.Initialize();
                MAST.EditorUI.DataManager.Initialize();

                var root = rootVisualElement;
                WindowChrome.InitializeRoot(root);

                // ----------------------------------------------
                // Tab bar
                // ----------------------------------------------
                var tabBar = new VisualElement();
                tabBar.AddToClassList("mast-tabbar");
                root.Add(tabBar);

                string[] tabCaptions = { "Build", "Paint", "Settings", "Tools" };
                tabButtons = new Button[tabCaptions.Length];
                for (int i = 0; i < tabCaptions.Length; i++)
                {
                    int index = i;
                    tabButtons[i] = new Button(() => SelectTab(index)) { text = tabCaptions[i] };
                    tabButtons[i].AddToClassList("mast-tab");
                    tabBar.Add(tabButtons[i]);
                }

                // ----------------------------------------------
                // Tab contents: Build and Paint are native UI Toolkit;
                // Settings and Tools still run their IMGUI code inside the new shell
                // ----------------------------------------------
                // Divisor tracks the Build toolbar's total column height in
                // icon units — bumped from 15.3 when the Greeble tool button
                // was added
                float IconSize() => (position.height / 16.85f) * MAST.Settings.Data.gui.toolbar.scale;

                tabContents = new VisualElement[]
                {
                    new BuildTabView(IconSize),
                    new PaintTabView(IconSize),
                    new SettingsTabView(),
                    new ToolsTabView()
                };

                foreach (var content in tabContents)
                {
                    content.AddToClassList("mast-tab-content");
                    root.Add(content);
                }

                // Keyboard events only dispatch to a FOCUSED element.  Clicking
                // a non-focusable element "a palette item" clears focus, which
                // silently disabled every window hotkey until something
                // focusable was clicked — so the root takes focus whenever
                // nothing else holds it
                root.focusable = true;
                root.RegisterCallback<PointerDownEvent>(_ =>
                {
                    if (root.focusController?.focusedElement == null)
                        root.Focus();
                });
                root.Focus();

                // The keyboard belongs to MAST's hotkeys: UI Toolkit "submits"
                // a focused Button on Space/Enter, and toolbar buttons keep
                // focus after a click — so pressing Space to rotate used to
                // toggle the last-clicked tool instead.  Buttons are
                // click-only now; the suppression is at the root so buttons
                // created later "palette popup rows" are covered too
                root.RegisterCallback<NavigationSubmitEvent>(evt =>
                {
                    if (evt.target is Button)
                        evt.StopPropagation();
                }, TrickleDown.TrickleDown);

                // Window-focused hotkeys; SceneView hotkeys are handled in OnScene
                root.RegisterCallback<KeyDownEvent>(evt =>
                {
                    // Never fire hotkeys while typing in a text input "typing G
                    // in a settings field used to toggle the grid".  This must
                    // check for a text FIELD specifically: UI Toolkit's Button
                    // derives from TextElement, so an is-TextElement check
                    // silenced every hotkey after any button click
                    if (evt.target is VisualElement element && Hotkeys.IsInsideTextInput(element))
                        return;

                    // Some key events arrive without a backing IMGUI event —
                    // build one so hotkey processing never silently skips
                    Event keyEvent = evt.imguiEvent ?? new Event
                    {
                        type = EventType.KeyDown,
                        keyCode = evt.keyCode,
                        modifiers = evt.modifiers
                    };

                    if (HotKeys.ProcessHotkeys(keyEvent))
                    {
                        // The key did MAST work — don't also deliver it onward
                        evt.StopPropagation();
                        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                    }
                }, TrickleDown.TrickleDown);

                SelectTab(Mathf.Clamp(MAST.EditorUI.DataManager.state.selectedInterfaceTab, 0, tabContents.Length - 1));
            }

            private void SelectTab(int index)
            {
                // Leaving the Paint tab takes any hover preview off the scene —
                // OnScene stops driving the painter, so nothing else would
                if (index != 1)
                    MAST.Painting.Painter.ClearCurrentMaterialPaintPreview();

                MAST.EditorUI.DataManager.state.selectedInterfaceTab = index;

                for (int i = 0; i < tabContents.Length; i++)
                {
                    tabContents[i].style.display = (i == index) ? DisplayStyle.Flex : DisplayStyle.None;
                    tabButtons[i].EnableInClassList("mast-tab--active", i == index);
                }
            }
        #endregion
        }
    }
}
