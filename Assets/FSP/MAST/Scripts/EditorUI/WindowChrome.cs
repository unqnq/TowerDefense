using UnityEditor;
using UnityEngine.UIElements;


namespace MAST
{
    namespace EditorUI
    {
        // Shared UI Toolkit scaffolding for every MAST window: theme + style
        // sheet setup and the section/label building blocks.  One place to
        // change the chrome — the tool windows used to each carry their own
        // copies of these helpers
        public static class WindowChrome
        {
            // Theme class + MAST style sheet on a window root.  Call first
            // thing in CreateGUI
            public static void InitializeRoot(VisualElement root)
            {
                root.Clear();
                root.AddToClassList("mast-root");
                root.AddToClassList(EditorGUIUtility.isProSkin ? "mast-theme-dark" : "mast-theme-light");

                StyleSheet styleSheet = MAST.LoadingHelper.GetStyleSheet();
                if (styleSheet != null)
                    root.styleSheets.Add(styleSheet);
            }

            // Boxed section "the helpBox look of the main window's panels"
            public static VisualElement Section(VisualElement parent)
            {
                var section = new VisualElement();
                section.AddToClassList("mast-section");
                parent.Add(section);
                return section;
            }

            public static Label BoldLabel(string text)
            {
                var label = new Label(text);
                label.AddToClassList("mast-bold-label");
                return label;
            }

            public static Label WrapLabel(string text)
            {
                var label = new Label(text);
                label.AddToClassList("mast-wrap-label");
                return label;
            }

            // Horizontal container for side-by-side controls
            public static VisualElement Row(VisualElement parent)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                parent.Add(row);
                return row;
            }
        }
    }
}
