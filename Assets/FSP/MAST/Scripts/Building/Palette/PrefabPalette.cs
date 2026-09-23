using UnityEngine;

namespace MAST
{
    namespace Building
    {
        namespace Palette
        {
            // Prefab palette facade over the shared PaletteLibrary backend.
            // Kept as the stable call surface for placement code and the Build tab.
            public static class PrefabPalette
            {
                private static PaletteLibrary libraryInstance;
                private static PaletteLibrary Library
                {
                    get
                    {
                        if (libraryInstance == null)
                            libraryInstance = new PaletteLibrary("t:prefab", "All Prefabs", typeof(GameObject), RenderThumbnails);
                        return libraryInstance;
                    }
                }

                private static Texture2D[] RenderThumbnails(Object[] items)
                {
                    return MAST.ThumbnailRenderer.RenderPrefabThumbnails(
                        System.Array.ConvertAll(items, item => (GameObject)item));
                }

                public static int selectedItemIndex
                {
                    get => Library.selectedItemIndex;
                    set => Library.selectedItemIndex = value;
                }

                public static int selectedFolderIndex => Library.SelectedFolderIndex;

                public static void GenerateThumbnailsAndLoadPrefabs(string path, int folderIndex, bool recreateAllThumbnails)
                {
                    Library.Load(path, true, recreateAllThumbnails, folderIndex);
                }

                public static void LoadPrefabs(string path, int folderIndex)
                {
                    Library.Load(path, false, false, folderIndex);
                }

                public static void ChangeActivePaletteFolder(int folderIndex)
                {
                    Library.ChangeActiveFolder(folderIndex);
                }

                public static string[] GetFolderNameArray() => Library.GetFolderNames();

                public static bool IsReady() => Library.IsReady();

                public static GUIContent[] GetGUIContentArray() => Library.GetGUIContentArray();

                public static GameObject GetSelectedPrefab() => (GameObject)Library.GetItem(selectedItemIndex);

                public static GameObject[] GetPrefabArray()
                {
                    var prefabs = new GameObject[Library.ItemCount];
                    for (int i = 0; i < prefabs.Length; i++)
                        prefabs[i] = (GameObject)Library.GetItem(i);
                    return prefabs;
                }
            }
        }
    }
}
