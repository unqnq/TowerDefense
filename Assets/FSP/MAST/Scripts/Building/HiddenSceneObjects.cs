using UnityEngine;
using UnityEditor;


namespace MAST
{
    namespace Building
    {
        // MAST's transient scene helpers "grid, visualizer, paint area" carry
        // HideFlags.DontSave, and Unity moves DontSave objects OUT of the
        // loaded scene — scene.GetRootGameObjects() and GameObject.Find no
        // longer see them.  These helpers search every in-memory GameObject
        // instead, excluding assets, so the objects stay findable/destroyable.
        public static class HiddenSceneObjects
        {
            public static GameObject FindNamed(string name)
            {
                foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (candidate.name != name)
                        continue;

                    // Assets "prefabs, models" are persistent; scene-side
                    // objects are not
                    if (EditorUtility.IsPersistent(candidate))
                        continue;

                    // MAST's helpers are always root objects
                    if (candidate.transform.parent != null)
                        continue;

                    return candidate;
                }

                return null;
            }

            public static void DestroyAllNamed(string name)
            {
                foreach (GameObject candidate in Resources.FindObjectsOfTypeAll<GameObject>())
                {
                    if (candidate.name != name)
                        continue;

                    if (EditorUtility.IsPersistent(candidate))
                        continue;

                    if (candidate.transform.parent != null)
                        continue;

                    Object.DestroyImmediate(candidate);
                }
            }
        }
    }
}
