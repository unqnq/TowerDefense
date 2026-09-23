using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Painting
    {
        // Persistent record of every material MAST has painted: which renderer,
        // which slot, what was there before, and what was applied.  Saved with
        // the rest of MAST's data in UserSettings, so it survives domain
        // reloads and editor restarts.
        //
        // This is what makes the Restore tool trustworthy: it puts back exactly
        // what the journal recorded — it works on plain scene objects "no
        // source prefab needed" and never reverts material overrides the user
        // made intentionally outside MAST.
        public class PaintJournal : ScriptableObject
        {
            [Serializable]
            public class Entry
            {
                // Survives domain reloads and scene reopens "scene objects
                // can't be referenced across sessions directly"
                public string rendererId;

                // Fast reference while the object is alive in this session
                public Renderer renderer;

                public int slot;
                public Material original;
                public Material painted;
            }

            [SerializeField] public List<Entry> entries = new List<Entry>();
        }

        // Static operations over the journal stored in Settings.Data
        public static class Journal
        {
            private static PaintJournal Data => MAST.Settings.Data.paintJournal;

            // Record a paint.  A repaint of the same slot keeps the FIRST
            // original "restore goes back to the pre-MAST material" and only
            // updates what is currently painted
            public static void RecordPaint(Renderer renderer, int slot, Material original, Material painted)
            {
                if (Data == null || renderer == null || original == painted)
                    return;

                Undo.RecordObject(Data, "Painted Material(s)");

                PaintJournal.Entry entry = FindEntry(renderer, slot);

                if (entry == null)
                {
                    Data.entries.Add(new PaintJournal.Entry
                    {
                        rendererId = GlobalObjectId.GetGlobalObjectIdSlow(renderer).ToString(),
                        renderer = renderer,
                        slot = slot,
                        original = original,
                        painted = painted
                    });
                }
                else if (entry.original == painted)
                {
                    // Painted back to the original by hand — nothing left to restore
                    Data.entries.Remove(entry);
                }
                else
                {
                    entry.painted = painted;
                }
            }

            // The material to restore for this slot, or null when MAST never
            // painted it "or the original asset no longer exists"
            public static Material GetOriginal(Renderer renderer, int slot)
            {
                PaintJournal.Entry entry = FindEntry(renderer, slot);
                return entry != null ? entry.original : null;
            }

            // A restore succeeded — the slot is back to its original
            public static void RemoveEntry(Renderer renderer, int slot)
            {
                if (Data == null)
                    return;

                PaintJournal.Entry entry = FindEntry(renderer, slot);
                if (entry == null)
                    return;

                Undo.RecordObject(Data, "Restored Material");
                Data.entries.Remove(entry);
            }

            private static PaintJournal.Entry FindEntry(Renderer renderer, int slot)
            {
                if (Data == null || renderer == null)
                    return null;

                string rendererId = null;

                foreach (PaintJournal.Entry entry in Data.entries)
                {
                    if (entry.slot != slot)
                        continue;

                    // Fast path: the in-session reference
                    if (entry.renderer == renderer)
                        return entry;

                    // Slow path: the reference died with a reload — match by
                    // GlobalObjectId and re-cache the live reference.  Entries
                    // for other scenes simply never match here
                    if (entry.renderer == null)
                    {
                        if (rendererId == null)
                            rendererId = GlobalObjectId.GetGlobalObjectIdSlow(renderer).ToString();

                        if (entry.rendererId == rendererId)
                        {
                            entry.renderer = renderer;
                            return entry;
                        }
                    }
                }

                return null;
            }
        }
    }
}
