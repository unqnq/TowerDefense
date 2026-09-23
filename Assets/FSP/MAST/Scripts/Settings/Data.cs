using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;


namespace MAST
{
    namespace Settings
    {
        // MAST's saved data lives in ONE file in the project's UserSettings
        // folder — per user, per project, outside Assets — so nothing
        // user-specific ships with MAST, version control stays clean, and the
        // old path-healing machinery "CoreSettings tracking asset paths" is
        // gone.  The settings classes stay ScriptableObjects: SerializedObject
        // bindings keep working, and the objects save together with the same
        // serialization Unity's own ScriptableSingleton uses.
        public static class Data
        {
            public static ScriptObj.GUISettings gui;
            public static ScriptObj.PlacementSettings placement;
            public static ScriptObj.HotkeySettings hotkey;
            public static MAST.EditorUI.ScriptObj.InterfaceState interfaceState;
            public static MAST.Painting.PaintJournal paintJournal;

            private const string saveFilePath = "UserSettings/MASTSettings.asset";

            // ---------------------------------------------------------------------------
            // When class is enabled
            // ---------------------------------------------------------------------------
            public static void Initialize()
            {
                // Reload after domain reloads "statics don't survive them"
                if (gui == null || placement == null || hotkey == null ||
                    interfaceState == null || paintJournal == null)
                    Load_Settings();
            }

            // ---------------------------------------------------------------------------
            #region Load / Save
            // ---------------------------------------------------------------------------

            public static void Load_Settings()
            {
                // Everything saved in one file; match the objects up by type
                if (File.Exists(saveFilePath))
                {
                    foreach (Object loaded in InternalEditorUtility.LoadSerializedFileAndForget(saveFilePath))
                    {
                        if (gui == null && loaded is ScriptObj.GUISettings loadedGUI)
                            gui = loadedGUI;
                        else if (placement == null && loaded is ScriptObj.PlacementSettings loadedPlacement)
                            placement = loadedPlacement;
                        else if (hotkey == null && loaded is ScriptObj.HotkeySettings loadedHotkey)
                            hotkey = loadedHotkey;
                        else if (interfaceState == null && loaded is MAST.EditorUI.ScriptObj.InterfaceState loadedState)
                            interfaceState = loadedState;
                        else if (paintJournal == null && loaded is MAST.Painting.PaintJournal loadedJournal)
                            paintJournal = loadedJournal;
                        else
                            Object.DestroyImmediate(loaded);
                    }
                }

                // Anything still missing starts from defaults.  The one-time
                // migration from the old in-Assets settings assets shipped,
                // ran, and was removed along with the legacy assets
                if (gui == null)
                    gui = CreateDefault<ScriptObj.GUISettings>();
                if (placement == null)
                    placement = CreateDefault<ScriptObj.PlacementSettings>();
                if (hotkey == null)
                    hotkey = CreateDefault<ScriptObj.HotkeySettings>();
                if (interfaceState == null)
                    interfaceState = CreateDefault<MAST.EditorUI.ScriptObj.InterfaceState>();
                if (paintJournal == null)
                    paintJournal = CreateDefault<MAST.Painting.PaintJournal>();

                // Not assets and not scene objects — never saved by Unity
                // itself.  DontSave, NOT HideAndDontSave: the latter includes
                // NotEditable, which turned every bound settings field read-only
                gui.hideFlags = HideFlags.DontSave;
                placement.hideFlags = HideFlags.DontSave;
                hotkey.hideFlags = HideFlags.DontSave;
                interfaceState.hideFlags = HideFlags.DontSave;
                paintJournal.hideFlags = HideFlags.DontSave;
            }

            // Debounced save for high-frequency callers "slider drags fire a
            // change per frame".  Settings also flush before assembly reloads,
            // so an editor crash after a recompile can no longer lose them
            private static double saveDueTime = -1;

            public static void RequestSave()
            {
                if (saveDueTime < 0)
                    EditorApplication.update += SaveWhenDue;

                saveDueTime = EditorApplication.timeSinceStartup + 0.5;
            }

            private static void SaveWhenDue()
            {
                if (EditorApplication.timeSinceStartup < saveDueTime)
                    return;

                EditorApplication.update -= SaveWhenDue;
                saveDueTime = -1;
                Save_Settings();
            }

            [InitializeOnLoadMethod]
            private static void RegisterReloadSave()
            {
                AssemblyReloadEvents.beforeAssemblyReload += Save_Settings;
            }

            public static void Save_Settings()
            {
                if (gui == null || placement == null || hotkey == null ||
                    interfaceState == null || paintJournal == null)
                    return;

                Directory.CreateDirectory(Path.GetDirectoryName(saveFilePath));

                InternalEditorUtility.SaveToSerializedFileAndForget(
                    new Object[] { gui, placement, hotkey, interfaceState, paintJournal },
                    saveFilePath,
                    true);
            }

            private static T CreateDefault<T>() where T : ScriptableObject
            {
                T instance = ScriptableObject.CreateInstance<T>();
                instance.name = typeof(T).Name;
                return instance;
            }

            #endregion
            // ---------------------------------------------------------------------------
        }
    }
}
