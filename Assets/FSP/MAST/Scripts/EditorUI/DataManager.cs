using System.IO;
using UnityEditor;
using UnityEngine;


namespace MAST
{
    namespace EditorUI
    {
        public static class DataManager
        {
            public static MAST.EditorUI.ScriptObj.InterfaceState state;
            
            // ---------------------------------------------------------------------------
            // Called during Interface OnEnable
            // ---------------------------------------------------------------------------
            public static void Initialize()
            {
                Get_Reference_To_Scriptable_Object();
            }
            
            // ---------------------------------------------------------------------------
            // The interface state lives in the same UserSettings file as the
            // rest of MAST's saved data "no more asset in the MAST folder"
            // ---------------------------------------------------------------------------
            private static void Get_Reference_To_Scriptable_Object()
            {
                MAST.Settings.Data.Initialize();
                state = MAST.Settings.Data.interfaceState;
            }
            
            // ---------------------------------------------------------------------------
            // Save preferences to state scriptable object
            // ---------------------------------------------------------------------------
            
            public static void Save_Palette_Items(bool forceSave = false)
            {
                // Get or create a scriptable object to store the interface state data
                Get_Reference_To_Scriptable_Object();

                state.selectedPrefabPaletteFolderIndex = MAST.Building.Palette.PrefabPalette.selectedFolderIndex;

                // The material palette's folder was never saved outside a
                // folder load — dropdown changes silently reverted
                state.selectedMaterialPaletteFolderIndex = MAST.Painting.Palette.MaterialPalette.selectedFolderIndex;

                Save_Changes_To_Disk();
            }
            
            public static void Restore_Palette_Items()
            {
                // Get or create a scriptable object to store the interface state data
                Get_Reference_To_Scriptable_Object();
                
                MAST.Building.Palette.PrefabPalette.LoadPrefabs(MAST.LoadingHelper.ConvertProjectPathToAbsolutePath(state.prefabPath),
                    state.selectedPrefabPaletteFolderIndex);
                
                MAST.Painting.Palette.MaterialPalette.LoadMaterials(MAST.LoadingHelper.ConvertProjectPathToAbsolutePath(state.materialPath),
                    state.selectedMaterialPaletteFolderIndex);
                
            }
            
            // ---------------------------------------------------------------------------
            // Save grid state preferences to state scriptable object
            // ---------------------------------------------------------------------------
            public static void Save_Interface_State()
            {
                // Get or create a scriptable object to store the interface state data
                Get_Reference_To_Scriptable_Object();
                
                // Save grid exists state
                state.gridExists = MAST.Building.GridManager.gridExists;

                // Save selected draw tool and palette
                state.selectedBuildToolIndex = MAST.Settings.Data.gui.toolbar.selectedDrawToolIndex;
                state.selectedPrefabIndex = MAST.Building.Palette.PrefabPalette.selectedItemIndex;

                // Save selected paint tool and material — the paint tool used
                // to restore ARMED with no material, silently doing nothing
                state.selectedPaintToolIndex = MAST.Settings.Data.gui.toolbar.selectedPaintToolIndex;
                state.selectedMaterialIndex = MAST.Painting.Palette.MaterialPalette.selectedItemIndex;

                // Save state changes to disk
                Save_Changes_To_Disk();
            }
            
            // ---------------------------------------------------------------------------
            // Load grid state preferences from state scriptable object
            // ---------------------------------------------------------------------------
            public static void Load_Interface_State()
            {
                // Get or scriptable object to store the interface state data
                Get_Reference_To_Scriptable_Object();
                
                // -----------------------------------------------
                // If there is no saved scriptable object
                // -----------------------------------------------
                if (state == null)
                {
                    // Set grid exists to false
                    MAST.Building.GridManager.gridExists = false;
                    
                    // Make sure no palette item and build tool is selected
                    MAST.EditorUI.ToolState.DeselectPrefab();
                }
                
                // -----------------------------------------------
                // If there is a scriptable object
                // -----------------------------------------------
                else
                {
                    // Load grid exists state
                    MAST.Building.GridManager.gridExists = state.gridExists;

                    // Load selected draw tool and palette
                    MAST.Settings.Data.gui.toolbar.selectedDrawToolIndex = state.selectedBuildToolIndex;
                    MAST.Building.Palette.PrefabPalette.selectedItemIndex = state.selectedPrefabIndex;

                    // Load selected paint tool and material together
                    MAST.Settings.Data.gui.toolbar.selectedPaintToolIndex = state.selectedPaintToolIndex;
                    MAST.Painting.Palette.MaterialPalette.selectedItemIndex = state.selectedMaterialIndex;
                }
            }
            
            public static void Save_Changes_To_Disk()
            {
                // One save writes the whole UserSettings file "settings + state"
                MAST.Settings.Data.Save_Settings();
            }
        }
    }
}
