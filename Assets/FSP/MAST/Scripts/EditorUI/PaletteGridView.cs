using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;


namespace MAST
{
    namespace EditorUI
    {
        // UI Toolkit palette view: folder dropdown + thumbnail selection grid + column slider.
        // This is only the shared *mechanics* — what selecting an item means (prefab placement
        // vs material painting) is supplied by the owning tab through Config callbacks.
        public class PaletteGridView : VisualElement
        {
            public class Config
            {
                public string emptyMessage;
                public Func<bool> isReady;
                public Func<string[]> getFolderNames;
                public Func<int> getFolderIndex;
                public Action<int> onFolderChanged;
                public Func<GUIContent[]> getItems; // image = thumbnail, tooltip = item name
                public Func<int> getSelectedIndex;
                public Action<int> onItemClicked;   // domain decides select vs deselect
                public Func<int> getColumnCount;
                public Action<int> setColumnCount;
                public Func<Color> getBGColor;
            }

            private readonly Config config;

            private readonly Label placeholder;
            private readonly Button folderButton;
            private readonly ScrollView scroll;
            private readonly VisualElement grid;
            private readonly SliderInt columnSlider;

            // Open folder popup "null when closed" and the element it overlays
            private VisualElement folderPopup;
            private VisualElement popupHost;

            // Cached state used to detect external changes on the sync tick
            private GUIContent[] lastItems;
            private int lastSelectedIndex = int.MinValue;
            private int lastColumnCount = -1;
            private Color lastBGColor = Color.clear;
            private bool suppressCallbacks;

            public PaletteGridView(Config config)
            {
                this.config = config;

                AddToClassList("mast-palette");

                placeholder = new Label(config.emptyMessage);
                placeholder.AddToClassList("mast-palette-placeholder");
                Add(placeholder);

                // The folder picker is a popup MAST renders itself: an indented
                // folder tree where EVERY row is a click target — parents,
                // leaves, Root, and All.  No Unity menu implementation is
                // involved "both stock menus made parent folders unclickable",
                // and living inside the MAST window means it inherits the same
                // dark-theme stylesheet as every other control
                folderButton = new Button(ToggleFolderPopup);
                folderButton.AddToClassList("mast-palette-folder");
                folderButton.AddToClassList("mast-folder-button");
                Add(folderButton);

                // The popup overlays the window root; close it if this palette
                // leaves the panel while it is open
                RegisterCallback<DetachFromPanelEvent>(_ => CloseFolderPopup());

                scroll = new ScrollView(ScrollViewMode.Vertical);
                scroll.AddToClassList("mast-palette-scroll");
                grid = new VisualElement();
                grid.AddToClassList("mast-palette-grid");
                scroll.Add(grid);
                Add(scroll);

                columnSlider = new SliderInt(1, 10);
                columnSlider.AddToClassList("mast-palette-slider");
                columnSlider.RegisterValueChangedCallback(evt =>
                {
                    if (suppressCallbacks)
                        return;
                    config.setColumnCount(evt.newValue);
                    UpdateItemSizes();
                });
                Add(columnSlider);

                // Reflow thumbnails when the palette is resized
                grid.RegisterCallback<GeometryChangedEvent>(_ => UpdateItemSizes());

                // Selection, folder list, and items can all change outside this view
                // (hotkeys, scene events, play mode) — poll and reconcile.
                schedule.Execute(Sync).Every(150);
                Sync();
            }

            // ------------------------------------------------------------------
            // Folder popup
            // ------------------------------------------------------------------

            private void ToggleFolderPopup()
            {
                if (folderPopup != null)
                {
                    CloseFolderPopup();
                    return;
                }

                OpenFolderPopup();
            }

            private void OpenFolderPopup()
            {
                string[] folderNames = config.getFolderNames();
                int currentIndex = config.getFolderIndex();

                // Host the popup on the MAST window root, so it draws above the
                // whole window and sits inside the theme scope "mast-theme-dark"
                popupHost = this;
                while (popupHost.parent != null && !popupHost.ClassListContains("mast-root"))
                    popupHost = popupHost.parent;

                folderPopup = new VisualElement();
                folderPopup.AddToClassList("mast-folder-popup");
                folderPopup.style.position = Position.Absolute;

                Rect buttonBounds = popupHost.WorldToLocal(folderButton.worldBound);
                folderPopup.style.left = buttonBounds.x;
                folderPopup.style.top = buttonBounds.yMax;
                folderPopup.style.minWidth = buttonBounds.width;

                var rowScroll = new ScrollView(ScrollViewMode.Vertical);
                rowScroll.AddToClassList("mast-folder-popup-scroll");
                folderPopup.Add(rowScroll);

                // "All ..." first, then the root folder when it holds assets
                AddFolderRow(rowScroll, folderNames[0], 0, currentIndex == 0, 0);

                int firstTreeEntry = 1;
                if (folderNames.Length > 1 && folderNames[1] == "Root")
                {
                    AddFolderRow(rowScroll, "Root", 0, currentIndex == 1, 1);
                    firstTreeEntry = 2;
                }

                // Build the FULL folder tree.  Folders with no assets directly
                // in them aren't selectable entries, but they still get a row —
                // without it their children would indent under the wrong
                // ancestor.  SortedDictionary keeps every level alphabetical
                var treeRoot = new FolderNode();
                for (int i = firstTreeEntry; i < folderNames.Length; i++)
                {
                    FolderNode node = treeRoot;
                    foreach (string segment in folderNames[i].Split('/'))
                    {
                        if (!node.children.TryGetValue(segment, out FolderNode child))
                        {
                            child = new FolderNode { name = segment };
                            node.children[segment] = child;
                        }
                        node = child;
                    }
                    node.index = i;
                }

                AddTreeRows(rowScroll, treeRoot, 0, currentIndex);

                popupHost.Add(folderPopup);

                // Any click outside the popup closes it "the button click closes
                // it through its own toggle"
                popupHost.RegisterCallback<PointerDownEvent>(OnPointerDownWhilePopupOpen, TrickleDown.TrickleDown);
            }

            private void OnPointerDownWhilePopupOpen(PointerDownEvent evt)
            {
                var target = evt.target as VisualElement;

                if (target != null &&
                    (target == folderPopup || folderPopup.Contains(target) ||
                     target == folderButton || folderButton.Contains(target)))
                    return;

                CloseFolderPopup();
            }

            private void CloseFolderPopup()
            {
                if (folderPopup == null)
                    return;

                popupHost.UnregisterCallback<PointerDownEvent>(OnPointerDownWhilePopupOpen, TrickleDown.TrickleDown);
                folderPopup.RemoveFromHierarchy();
                folderPopup = null;
                popupHost = null;
            }

            // One node per folder in the tree; index is -1 for structural rows
            // "folders that hold no assets directly and can't be selected"
            private class FolderNode
            {
                public string name;
                public int index = -1;
                public readonly SortedDictionary<string, FolderNode> children =
                    new SortedDictionary<string, FolderNode>(StringComparer.OrdinalIgnoreCase);
            }

            private void AddTreeRows(ScrollView rowScroll, FolderNode parent, int depth, int currentIndex)
            {
                foreach (FolderNode child in parent.children.Values)
                {
                    AddFolderRow(rowScroll, child.name, depth,
                        child.index >= 0 && child.index == currentIndex, child.index);
                    AddTreeRows(rowScroll, child, depth + 1, currentIndex);
                }
            }

            private void AddFolderRow(ScrollView rowScroll, string text, int depth, bool isCurrent, int index)
            {
                var row = new Label((isCurrent ? "✓ " : "    ") + text);
                row.AddToClassList("mast-folder-popup-row");
                row.style.paddingLeft = 6 + depth * 16;

                if (isCurrent)
                    row.AddToClassList("mast-folder-popup-row--current");

                if (index >= 0)
                {
                    row.RegisterCallback<ClickEvent>(_ =>
                    {
                        if (index != config.getFolderIndex())
                            config.onFolderChanged(index);
                        CloseFolderPopup();
                    });
                }
                else
                {
                    row.AddToClassList("mast-folder-popup-row--structural");
                    row.tooltip = "No items directly in this folder";
                }

                rowScroll.Add(row);
            }

            private void Sync()
            {
                bool ready = config.isReady();

                placeholder.style.display = ready ? DisplayStyle.None : DisplayStyle.Flex;
                folderButton.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
                scroll.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;
                columnSlider.style.display = ready ? DisplayStyle.Flex : DisplayStyle.None;

                if (!ready)
                {
                    lastItems = null;
                    grid.Clear();
                    CloseFolderPopup();
                    return;
                }

                suppressCallbacks = true;
                try
                {
                    // Folder button shows the current folder's path.  The popup
                    // itself is built fresh every time it opens, so it can never
                    // go stale
                    string[] folderNames = config.getFolderNames();
                    int folderIndex = Mathf.Clamp(config.getFolderIndex(), 0, folderNames.Length - 1);
                    string folderText = folderNames[folderIndex] + "  ▾";
                    if (folderButton.text != folderText)
                        folderButton.text = folderText;

                    // Thumbnail grid (rebuild only when the item array itself changed)
                    GUIContent[] items = config.getItems();
                    if (!ReferenceEquals(items, lastItems))
                    {
                        lastItems = items;
                        RebuildGrid(items);
                    }

                    // Column count
                    int columns = Mathf.Clamp(config.getColumnCount(), 1, 10);
                    if (columnSlider.value != columns)
                        columnSlider.SetValueWithoutNotify(columns);
                    if (columns != lastColumnCount)
                    {
                        lastColumnCount = columns;
                        UpdateItemSizes();
                    }

                    // Selection highlight
                    int selected = config.getSelectedIndex();
                    if (selected != lastSelectedIndex)
                    {
                        lastSelectedIndex = selected;
                        for (int i = 0; i < grid.childCount; i++)
                            grid[i].EnableInClassList("mast-palette-item--selected", i == selected);
                    }

                    // Palette background preference "any color, straight from
                    // Settings — no more three preset shades"
                    Color bgColor = config.getBGColor();
                    if (bgColor != lastBGColor)
                    {
                        lastBGColor = bgColor;
                        scroll.style.backgroundColor = bgColor;
                    }
                }
                finally
                {
                    suppressCallbacks = false;
                }
            }

            private void RebuildGrid(GUIContent[] items)
            {
                grid.Clear();
                lastSelectedIndex = int.MinValue;

                if (items == null)
                    return;

                for (int i = 0; i < items.Length; i++)
                {
                    int index = i;
                    var item = new VisualElement();
                    item.AddToClassList("mast-palette-item");
                    if (items[i] != null)
                    {
                        if (items[i].image != null)
                            item.style.backgroundImage = new StyleBackground((Texture2D)items[i].image);
                        item.tooltip = items[i].tooltip?.Replace("\n", " ");
                    }
                    item.RegisterCallback<ClickEvent>(_ => config.onItemClicked(index));
                    grid.Add(item);
                }

                UpdateItemSizes();
            }

            private void UpdateItemSizes()
            {
                float gridWidth = grid.resolvedStyle.width;
                if (float.IsNaN(gridWidth) || gridWidth <= 0)
                    return;

                int columns = Mathf.Clamp(config.getColumnCount(), 1, 10);
                float itemSize = Mathf.Floor(gridWidth / columns) - 6; // margin + border

                if (itemSize < 8)
                    return;

                for (int i = 0; i < grid.childCount; i++)
                {
                    grid[i].style.width = itemSize;
                    grid[i].style.height = itemSize;
                }
            }

        }
    }
}
