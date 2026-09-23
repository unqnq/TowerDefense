using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    // One folder-scanning, thumbnail-caching palette backend, replacing the
    // line-for-line cloned Building/Painting Palette IO + Manager pairs.
    // Instantiated once for the prefab palette and once for the material
    // palette — what selecting an item MEANS stays in ToolState and the tabs.
    public class PaletteLibrary
    {
        private const string thumbnailFolderName = "_MAST_Thumbnails";

        private readonly string assetTypeFilter;   // AssetDatabase filter, e.g. "t:prefab"
        private readonly string allItemsLabel;     // e.g. "All Prefabs"
        private readonly System.Func<Object[], Texture2D[]> renderThumbnails;

        // Type the MAIN asset must be.  FindAssets("t:material") also matches
        // FBX files with EMBEDDED materials, whose main asset is a GameObject —
        // the palettes' casts used to throw on them
        private readonly System.Type mainAssetType;

        // A selectable folder: with direct assets it lists exactly those; a
        // structural folder "assets only in its subfolders" lists everything
        // beneath it, so kit-pack parents are pickable categories
        private class FolderEntry
        {
            public string path;
            public bool hasDirectAssets;
        }

        private string rootPath = "";
        private readonly List<FolderEntry> folderEntries = new List<FolderEntry>();
        private Object[] activeItems = new Object[0];
        private Texture2D[] activeThumbnails = new Texture2D[0];
        private GUIContent[] guiContent;

        public int selectedItemIndex = -1;
        public int SelectedFolderIndex { get; private set; }

        public PaletteLibrary(string assetTypeFilter, string allItemsLabel,
            System.Type mainAssetType, System.Func<Object[], Texture2D[]> renderThumbnails)
        {
            this.assetTypeFilter = assetTypeFilter;
            this.allItemsLabel = allItemsLabel;
            this.mainAssetType = mainAssetType;
            this.renderThumbnails = renderThumbnails;
        }

        // ------------------------------------------------------------------
        // Public surface
        // ------------------------------------------------------------------

        public void Load(string path, bool generateThumbnails, bool recreateAllThumbnails, int folderIndex)
        {
            // Accept absolute or project path; store project-relative "Assets/..."
            rootPath = (path ?? "").Replace(Application.dataPath, "Assets");

            folderEntries.Clear();
            cachedFolderNames = null;

            if (string.IsNullOrEmpty(rootPath) || !AssetDatabase.IsValidFolder(rootPath))
            {
                ClearItems();
                return;
            }

            BuildFolderPathList();

            if (folderEntries.Count == 0)
            {
                ClearItems();
                return;
            }

            if (generateThumbnails)
                PrepareThumbnails(recreateAllThumbnails);

            ChangeActiveFolder(Mathf.Clamp(folderIndex, 0, folderEntries.Count));
        }

        // Folder names shown in the palette folder picker: "All ..." + each
        // folder as its plain root-relative path.  The picker renders these as
        // an indented tree where every row is clickable — no name decoration
        // needed
        // Cached — the palette views poll this on a timer, and rebuilding the
        // array plus every string per tick was pure garbage
        private string[] cachedFolderNames;

        public string[] GetFolderNames()
        {
            if (cachedFolderNames != null && cachedFolderNames.Length == folderEntries.Count + 1)
                return cachedFolderNames;

            string[] folders = new string[folderEntries.Count + 1];
            folders[0] = allItemsLabel;

            for (int i = 0; i < folderEntries.Count; i++)
            {
                folders[i + 1] = folderEntries[i].path == rootPath
                    ? "Root"
                    : folderEntries[i].path.Replace(rootPath + "/", "");
            }

            cachedFolderNames = folders;
            return folders;
        }

        // folderIndex is the dropdown index: 0 = all folders, 1+ = folderEntries[index - 1]
        public void ChangeActiveFolder(int folderIndex)
        {
            SelectedFolderIndex = folderIndex;

            if (folderEntries.Count == 0)
            {
                ClearItems();
                return;
            }

            LoadItems(folderIndex - 1);
            BuildGUIContent();
        }

        public bool IsReady()
        {
            return guiContent != null && guiContent.Length > 0;
        }

        public GUIContent[] GetGUIContentArray()
        {
            return guiContent;
        }

        // Null for out-of-range indices: a remembered selection can outlive
        // the folder contents it was saved against
        public Object GetItem(int index)
        {
            if (index < 0 || index >= activeItems.Length)
                return null;

            return activeItems[index];
        }

        public int ItemCount => activeItems.Length;

        // ------------------------------------------------------------------
        // Item + thumbnail loading
        // ------------------------------------------------------------------

        private void ClearItems()
        {
            activeItems = new Object[0];
            activeThumbnails = new Texture2D[0];
            guiContent = null;
        }

        private void LoadItems(int folderIndex)
        {
            bool allFolders = folderIndex < 0;
            FolderEntry entry = allFolders ? null : folderEntries[folderIndex];
            string searchFolder = allFolders ? rootPath : entry.path;

            // Folders with direct assets list exactly those; a structural
            // folder lists everything in its subfolders
            bool includeSubfolders = allFolders || !entry.hasDirectAssets;

            string[] guids = AssetDatabase.FindAssets(assetTypeFilter, new[] { searchFolder });

            var items = new List<Object>(guids.Length);
            var thumbnails = new List<Texture2D>(guids.Length);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // When a folder with its own assets is chosen, skip subfolders
                if (!includeSubfolders &&
                    Path.GetDirectoryName(assetPath).Replace("\\", "/") != searchFolder)
                    continue;

                // Skip assets that merely CONTAIN a match "FBX files with
                // embedded materials" — the palette needs the main asset
                Object mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                if (!mainAssetType.IsInstanceOfType(mainAsset))
                    continue;

                items.Add(mainAsset);
                thumbnails.Add(AssetDatabase.LoadAssetAtPath<Texture2D>(GetThumbnailPath(assetPath)));
            }

            activeItems = items.ToArray();
            activeThumbnails = thumbnails.ToArray();
        }

        private void BuildGUIContent()
        {
            guiContent = new GUIContent[activeItems.Length];

            for (int i = 0; i < activeItems.Length; i++)
            {
                string tooltip = activeItems[i] != null
                    ? activeItems[i].name.Replace("_", "\n").Replace(" ", "\n")
                    : "";

                if (activeThumbnails[i] != null)
                    activeThumbnails[i].alphaIsTransparency = true;

                guiContent[i] = new GUIContent(activeThumbnails[i], tooltip);
            }
        }

        // ------------------------------------------------------------------
        // Folder scanning
        // ------------------------------------------------------------------

        // Collect every selectable folder: those that directly contain a
        // matching asset, plus structural folders whose SUBFOLDERS do "they
        // list everything beneath them when chosen"
        private void BuildFolderPathList()
        {
            var candidates = new List<string> { rootPath };
            AddSubfoldersRecursively(candidates, rootPath);

            // Which folders directly contain at least one matching asset.
            // ONE query over the whole root, bucketed by folder — the old
            // per-folder FindAssets ran hundreds of queries on big trees and
            // was most of the window-open busy dialog
            var foldersWithAssets = new HashSet<string>();
            foreach (string guid in AssetDatabase.FindAssets(assetTypeFilter, new[] { rootPath }))
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                // Main-asset type check without loading the asset
                if (!mainAssetType.IsAssignableFrom(AssetDatabase.GetMainAssetTypeAtPath(assetPath)))
                    continue;

                foldersWithAssets.Add(Path.GetDirectoryName(assetPath).Replace("\\", "/"));
            }

            foreach (string path in candidates)
            {
                bool hasDirectAssets = foldersWithAssets.Contains(path);

                // The root is only its own entry when it holds assets directly
                // — "All" already covers the whole tree
                if (path == rootPath)
                {
                    if (hasDirectAssets)
                        folderEntries.Add(new FolderEntry { path = path, hasDirectAssets = true });
                    continue;
                }

                if (hasDirectAssets)
                {
                    folderEntries.Add(new FolderEntry { path = path, hasDirectAssets = true });
                    continue;
                }

                // Structural folder: selectable when anything beneath it holds
                // assets
                string prefix = path + "/";
                foreach (string assetFolder in foldersWithAssets)
                {
                    if (assetFolder.StartsWith(prefix))
                    {
                        folderEntries.Add(new FolderEntry { path = path, hasDirectAssets = false });
                        break;
                    }
                }
            }
        }

        private static void AddSubfoldersRecursively(List<string> pathList, string searchPath)
        {
            foreach (string path in AssetDatabase.GetSubFolders(searchPath))
            {
                pathList.Add(path);
                AddSubfoldersRecursively(pathList, path);
            }
        }

        // ------------------------------------------------------------------
        // Thumbnail cache (_MAST_Thumbnails folder beside the assets)
        // ------------------------------------------------------------------

        private void PrepareThumbnails(bool recreateAllThumbnails)
        {
            List<string> missing = GetPathsOfItemsMissingThumbnails(recreateAllThumbnails);
            CreateMissingThumbnailFolders();
            CreateAndSaveThumbnails(missing);
        }

        private List<string> GetPathsOfItemsMissingThumbnails(bool recreateAllThumbnails)
        {
            var missing = new List<string>();

            foreach (FolderEntry entry in folderEntries)
            {
                // Structural folders hold no assets of their own — their
                // subfolders carry the thumbnails
                if (!entry.hasDirectAssets)
                    continue;

                string path = entry.path;
                bool hasThumbnailFolder = AssetDatabase.IsValidFolder(path + "/" + thumbnailFolderName);

                string[] guids = AssetDatabase.FindAssets(assetTypeFilter, new[] { path });

                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                    // Only assets directly in this folder "not in a subfolder"
                    if (Path.GetDirectoryName(assetPath).Replace("\\", "/") != path)
                        continue;

                    // Same main-asset type filter as LoadItems
                    if (!mainAssetType.IsInstanceOfType(AssetDatabase.LoadMainAssetAtPath(assetPath)))
                        continue;

                    if (hasThumbnailFolder && !recreateAllThumbnails)
                    {
                        if (AssetDatabase.LoadAssetAtPath<Texture2D>(GetThumbnailPath(assetPath)) == null)
                            missing.Add(assetPath);
                    }
                    else
                    {
                        missing.Add(assetPath);
                    }
                }
            }

            return missing;
        }

        private void CreateMissingThumbnailFolders()
        {
            foreach (FolderEntry entry in folderEntries)
            {
                if (!entry.hasDirectAssets)
                    continue;

                if (!AssetDatabase.IsValidFolder(entry.path + "/" + thumbnailFolderName))
                    AssetDatabase.CreateFolder(entry.path, thumbnailFolderName);
            }
        }

        private void CreateAndSaveThumbnails(List<string> assetPaths)
        {
            if (assetPaths.Count == 0)
                return;

            var items = new Object[assetPaths.Count];
            for (int i = 0; i < assetPaths.Count; i++)
                items[i] = AssetDatabase.LoadMainAssetAtPath(assetPaths[i]);

            // Render thumbnails with the caller-supplied camera routine
            // "prefab and material palettes pose the snapshot differently"
            Texture2D[] thumbnailImages = renderThumbnails(items);

            for (int i = 0; i < assetPaths.Count; i++)
            {
                // A failed render leaves gaps — skip them so one bad thumbnail
                // doesn't lose the rest, and retry them on the next load
                if (thumbnailImages == null || thumbnailImages[i] == null)
                    continue;

                byte[] bytes = thumbnailImages[i].EncodeToPNG();
                File.WriteAllBytes(GetThumbnailPath(assetPaths[i]), bytes);
            }

            AssetDatabase.Refresh();
        }

        private static string GetThumbnailPath(string assetPath)
        {
            string assetName = Path.GetFileNameWithoutExtension(assetPath);
            string folder = Path.GetDirectoryName(assetPath).Replace("\\", "/");
            return folder + "/" + thumbnailFolderName + "/" + assetName + ".png";
        }
    }
}
