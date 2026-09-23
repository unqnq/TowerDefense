using UnityEngine;


namespace MAST
{
    namespace Painting
    {
        namespace Palette
        {
            // Material palette facade over the shared PaletteLibrary backend.
            // Kept as the stable call surface for the Painter and the Paint tab.
            public static class MaterialPalette
            {
                private static PaletteLibrary libraryInstance;
                private static PaletteLibrary Library
                {
                    get
                    {
                        if (libraryInstance == null)
                            libraryInstance = new PaletteLibrary("t:material", "All Materials", typeof(Material), RenderThumbnails);
                        return libraryInstance;
                    }
                }

                private static Texture2D[] RenderThumbnails(Object[] items)
                {
                    return MAST.ThumbnailRenderer.RenderMaterialThumbnails(
                        System.Array.ConvertAll(items, item => (Material)item));
                }

                public static int selectedItemIndex
                {
                    get => Library.selectedItemIndex;
                    set => Library.selectedItemIndex = value;
                }

                public static int selectedFolderIndex => Library.SelectedFolderIndex;

                public static void GenerateThumbnailsAndLoadMaterials(string path, int folderIndex, bool recreateAllThumbnails)
                {
                    Library.Load(path, true, recreateAllThumbnails, folderIndex);
                }

                public static void LoadMaterials(string path, int folderIndex)
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

                public static Material GetSelectedMaterial() => (Material)Library.GetItem(selectedItemIndex);
            }
        }
    }
}
