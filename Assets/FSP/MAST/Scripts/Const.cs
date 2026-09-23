
namespace MAST
{
    public static class Const
    {
        // Grid
        public static class Grid
        {
            public static string defaultName = "MAST_Grid";
            public static string defaultParentName = "MAST_Grid_Parent";
            public static float yOffsetToAvoidTearing = -0.001f;

            // Configurable in Settings > GUI > Grid.  The old hardcoded layer 4
            // is Unity's built-in WATER layer: user content there hijacked grid
            // snapping and got locked while the grid was on
            public static int gridLayer =>
                MAST.Settings.Data.gui != null ? MAST.Settings.Data.gui.grid.gridLayer : 31;
        }
        //public static Grid_Class grid = new Grid_Class();
        
        // Placement
        public static class Placement
        {
            public static string defaultTargetParentName = "MAST_Holder";
            public static string defaultTargetParentTag = "MAST_Holder";
            public static string greebleTag = "MAST_Greeble";
            public static int visualizerLayer = 2;
        }
        //public static Placement_Class placement = new Placement_Class();
        
        
    }
}
