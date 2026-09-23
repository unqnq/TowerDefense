using System;
using System.IO;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    public static class LoadingHelper
    {
        const string imagePath = "/Images/";
        const string styleSheetPath = "/Etc/MAST.uss";
        const string gridMaterialPath = "/Etc/Material_Grid.mat";
        const string paintAreaMaterialPath = "/Etc/Material_PaintArea.mat";
        
        public static string ConvertAbsolutePathToProjectPath(string path)
        {
            // If this absolute path is for this project, replace application data path with "Assets"
            if (path.StartsWith(Application.dataPath))
                return path.Replace(Application.dataPath, "Assets");
            
            // If this absolute path isn't for this project, then return the top-level path
            else
                return "Assets";
        }
        
        public static string ConvertProjectPathToAbsolutePath(string path)
        {
            // If the project path is valid, replace "Assets" with application data path
            if (path.StartsWith("Assets"))
                return path.Replace("Assets", Application.dataPath);
            
            // If the project path is not valid, return an empty string
            else
                return "";
        }
        
        private static string cachedRootFolder;

        // MAST works from any folder: the root is located through the assembly
        // definition asset "a unique file that always travels with MAST" instead
        // of the old scan of every folder in Assets for a name ending in MAST.
        public static string GetMASTRootFolder()
        {
            // Cached and revalidated, so moving or renaming the folder just works.
            // Directory.Exists instead of AssetDatabase.IsValidFolder: it is legal
            // in every context "AssetDatabase calls throw during ScriptableObject
            // construction" and relative Assets paths resolve from the project root
            if (!string.IsNullOrEmpty(cachedRootFolder) && Directory.Exists(cachedRootFolder))
                return cachedRootFolder;

            cachedRootFolder = null;

            string[] guids = AssetDatabase.FindAssets("FSP.MAST.Scripts t:AssemblyDefinitionAsset");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("/FSP.MAST.Scripts.asmdef"))
                {
                    // .../<MAST root>/Scripts/FSP.MAST.Scripts.asmdef
                    cachedRootFolder = Path.GetDirectoryName(Path.GetDirectoryName(path)).Replace("\\", "/");
                    return cachedRootFolder;
                }
            }

            Debug.LogError("MAST could not locate its own folder (FSP.MAST.Scripts.asmdef not found).");
            return "";
        }
        
        // The thumbnail camera prefab is gone: thumbnails render offscreen
        // through ThumbnailRenderer "PreviewRenderUtility" now.
        
        // ---------------------------------------------------------------------------
        #region Icons (with user-recolorable accent)
        // ---------------------------------------------------------------------------

        // The icon set's cyan accent tones "sampled from the shipped icons"
        private static readonly Color defaultAccent = new Color(79f / 255f, 179f / 255f, 194f / 255f, 1f);

        // Hue window that counts as "the accent" — wide enough to catch
        // anti-aliased blends, narrow enough to leave true grays/whites alone
        private const float accentHueMin = 0.42f;
        private const float accentHueMax = 0.63f;
        private const float accentSaturationMin = 0.12f;

        // Per-file display copies handed to the UI.  Retinting rewrites their
        // pixels IN PLACE, so every button already holding a reference updates
        // without the views knowing anything happened
        private static readonly System.Collections.Generic.Dictionary<string, Color[]> iconSourcePixels =
            new System.Collections.Generic.Dictionary<string, Color[]>();
        private static readonly System.Collections.Generic.Dictionary<string, Texture2D> iconDisplays =
            new System.Collections.Generic.Dictionary<string, Texture2D>();
        private static Color appliedAccent = new Color(0, 0, 0, 0);   // forces first tint

        public static Texture2D GetImage(string fileName)
        {
            // A settings change since the last call retints everything
            RefreshIconTint();

            if (iconDisplays.TryGetValue(fileName, out Texture2D existing) && existing != null)
                return existing;

            var asset = (Texture2D)AssetDatabase.LoadAssetAtPath(
                GetMASTRootFolder() + imagePath + fileName, typeof(Texture2D));
            if (asset == null)
                return null;

            // The icon assets aren't imported readable — copy through a
            // temporary RenderTexture to get at the pixels
            RenderTexture temporary = RenderTexture.GetTemporary(
                asset.width, asset.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(asset, temporary);
            RenderTexture.active = temporary;

            var display = new Texture2D(asset.width, asset.height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = asset.filterMode
            };
            display.ReadPixels(new Rect(0, 0, asset.width, asset.height), 0, 0);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            iconSourcePixels[fileName] = display.GetPixels();
            iconDisplays[fileName] = display;

            TintIcon(fileName, CurrentAccent());
            return display;
        }

        private static Color CurrentAccent()
        {
            return MAST.Settings.Data.gui != null
                ? MAST.Settings.Data.gui.toolbar.iconColor
                : defaultAccent;
        }

        // Rewrite every cached icon with the current accent color when the
        // setting changed.  Called lazily from GetImage and directly by the
        // Settings tab when the user edits the color
        public static void RefreshIconTint()
        {
            Color accent = CurrentAccent();
            if (appliedAccent == accent)
                return;

            appliedAccent = accent;
            foreach (string fileName in iconSourcePixels.Keys)
                TintIcon(fileName, accent);
        }

        // Remap the accent-hued pixels onto the chosen color: the bright cyan
        // maps exactly to the chosen color, darker/blended tones scale their
        // saturation and value proportionally — white, black, and the black
        // drop shadows are untouched
        private static void TintIcon(string fileName, Color accent)
        {
            Texture2D display = iconDisplays[fileName];
            if (display == null)
                return;

            Color.RGBToHSV(defaultAccent, out _, out float referenceS, out float referenceV);
            Color.RGBToHSV(accent, out float accentH, out float accentS, out float accentV);

            Color[] source = iconSourcePixels[fileName];
            var pixels = new Color[source.Length];

            for (int i = 0; i < source.Length; i++)
            {
                Color pixel = source[i];
                Color.RGBToHSV(pixel, out float h, out float s, out float v);

                if (s >= accentSaturationMin && h >= accentHueMin && h <= accentHueMax)
                {
                    Color tinted = Color.HSVToRGB(accentH,
                        Mathf.Clamp01(s * (accentS / referenceS)),
                        Mathf.Clamp01(v * (accentV / referenceV)));
                    tinted.a = pixel.a;
                    pixels[i] = tinted;
                }
                else
                {
                    pixels[i] = pixel;
                }
            }

            display.SetPixels(pixels);
            display.Apply();
        }

        #endregion
        // ---------------------------------------------------------------------------
        
        public static UnityEngine.UIElements.StyleSheet GetStyleSheet()
        {
            return (UnityEngine.UIElements.StyleSheet)AssetDatabase.LoadAssetAtPath(
                GetMASTRootFolder() + styleSheetPath, typeof(UnityEngine.UIElements.StyleSheet));
        }
        
        public static Material GetGridMaterial()
        {
            return (Material)AssetDatabase.LoadAssetAtPath(GetMASTRootFolder()
                + gridMaterialPath, typeof(Material));
        }
        
        public static Material GetPaintAreaMaterial()
        {
            return (Material)AssetDatabase.LoadAssetAtPath(GetMASTRootFolder()
                + paintAreaMaterialPath, typeof(Material));
        }

        // ---------------------------------------------------------------------------
        #region Get GameObjects from Selected Folder
        // ---------------------------------------------------------------------------
        public static GameObject[] GetPrefabsInFolder(string prefabFolder)
        {
            // Get the GUID of every file in that folder that is of the file type prefab
            string[] GUIDOfAllPrefabsInFolder =
                AssetDatabase.FindAssets("t:prefab", new[] { prefabFolder });
            
            // Create array to store the gameObjects
            GameObject[] allPrefabsAtPath = new GameObject[GUIDOfAllPrefabsInFolder.Length];
            
            // Create the string outside the loop so it is not recreated every loop
            string pathToPrefab;
            
            for (int index = GUIDOfAllPrefabsInFolder.Length - 1; index >= 0; index--)
            {
                // Convert GUID at current index to path string
                pathToPrefab = AssetDatabase.GUIDToAssetPath(GUIDOfAllPrefabsInFolder[index]);

                // Get gameObject at path "main asset = the prefab ROOT; loading
                // by type can return a child object instead"
                allPrefabsAtPath[index] = AssetDatabase.LoadMainAssetAtPath(pathToPrefab) as GameObject;
            }
            
            return allPrefabsAtPath;
        }
        
        // ---------------------------------------------------------------------------
        // Used by GetAllGameObjectsInSelectedFolder
        // ---------------------------------------------------------------------------
        public static string GetPathOfSelectedFolder()
        {
            // This code was entirely taken from https://gist.github.com/allanolivei/9260107
            string path = "Assets";
            foreach (UnityEngine.Object obj in Selection.GetFiltered(typeof(UnityEngine.Object), SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    path = Path.GetDirectoryName(path);
                    break;
                }
            }
            
            return path;
        }
        #endregion
        // ---------------------------------------------------------------------------
        
        // ---------------------------------------------------------------------------
        // Get path of the selected object
        // ---------------------------------------------------------------------------
        public static string GetPathOfSelectedObjectTypeOf(Type type)
        {
            // Get selected objects as object array
            UnityEngine.Object[] objs = Selection.GetFiltered(type, SelectionMode.Assets);
            
            // If correct object type was selected, return the first selected item's path
            if (objs.Length > 0)
                return AssetDatabase.GetAssetPath(objs[0]);
            
            // If no object of this type was selected, return an empty string
            else
                return "";
        }
        
        // ---------------------------------------------------------------------------
        // Choose a folder dialog
        // ---------------------------------------------------------------------------
        public static void Show_Choose_Folder_Dialog(string dialogTitle, string defaultPath)
        {
            string path = EditorUtility.OpenFolderPanel(dialogTitle, defaultPath, "");
        }
    }
}
