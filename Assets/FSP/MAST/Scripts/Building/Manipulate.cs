using UnityEditor;
using UnityEngine;


namespace MAST
{
    namespace Building
    {
        public static class Manipulate
        {
            // Used to register whether flip or rotate is being done to an existing object
            private static bool allowUndoRegistering = false;
            
            // Rotate and Flip axis
            private static Axis rotateAxis = Axis.Y;
            private static Axis flipAxis = Axis.X;
            
            // Remembered rotation of last prefab, stored GRID-RELATIVE so
            // placements stay aligned however the MAST Holder is rotated
            private static Quaternion currentRotation;
            
            // ---------------------------------------------------------------------------
            #region Rotate
            // ---------------------------------------------------------------------------
            
            // Toggle Rotate Axis
            public static void ToggleRotateAxis()
            {
                // Switch to next axis
                switch (rotateAxis)
                {
                    case Axis.X:
                        rotateAxis = Axis.Y;
                        break;
                    case Axis.Y:
                        rotateAxis = Axis.Z;
                        break;
                    case Axis.Z:
                        rotateAxis = Axis.X;
                        break;
                }
            }
            
            // Get Current Rotate Axis
            public static Axis GetCurrentRotateAxis()
            {
                return rotateAxis;
            }
            
            // Rotate the visualizer or whatever object is selected
            public static GameObject RotateObject()
            {
                // Greeble mode spins the piece around the targeted face's
                // normal instead of the grid axes
                if (MAST.EditorUI.ToolState.ActiveBuildTool == BuildMode.Greeble &&
                    Visualizer.GetGameObject() != null)
                {
                    GreebleTool.Spin(PrefabProfile.GetRotationStep().y);
                    return Visualizer.GetGameObject();
                }

                GameObject gameObject = GetObjectToManipulate(Visualizer.GetGameObject());
                
                if (gameObject != null)
                {
                    // Make this an Undo point, just before rotating the existing object
                    if (allowUndoRegistering)
                    {
                        Undo.RegisterCompleteObjectUndo(gameObject.transform, "Rotated GameObject");
                        allowUndoRegistering = false;
                    }
                    
                    
                    // TODO:  Add code to see if local space rotation allows this rotation
                    //        This is different from world space
                    
                    
                    // Rotate around the PLACEMENT frame's axes: the grid's axes
                    // when Rotate With Holder is on, world axes when it is off
                    switch (rotateAxis)
                    {
                        case Axis.X:
                            gameObject.transform.Rotate(
                                GridSpace.PlacementRotation * Vector3.right, PrefabProfile.GetRotationStep().x, Space.World);
                            break;
                        case Axis.Y:
                            gameObject.transform.Rotate(
                                GridSpace.PlacementRotation * Vector3.up, PrefabProfile.GetRotationStep().y, Space.World);
                            break;
                        case Axis.Z:
                            gameObject.transform.Rotate(
                                GridSpace.PlacementRotation * Vector3.forward, PrefabProfile.GetRotationStep().z, Space.World);
                            break;
                    }

                    // Remember this rotation for future prefab placement,
                    // relative to the placement frame
                    currentRotation = Quaternion.Inverse(GridSpace.PlacementRotation) * gameObject.transform.rotation;
                }

                // Return rotated GameObject
                return gameObject;
            }
            
            // Return current rotation.  A never-set static quaternion is all
            // zeros "invalid, NOT identity" — normalize it so callers can
            // compose and compare it safely
            public static Quaternion GetCurrentRotation()
            {
                if (currentRotation.x == 0f && currentRotation.y == 0f &&
                    currentRotation.z == 0f && currentRotation.w == 0f)
                    return Quaternion.identity;

                return currentRotation;
            }
            #endregion
            
            // Set rotation factor
            public static Vector3 SetRotationFactor(Vector3 rotationFactor)
            {
                return rotationFactor;
            }
            // ---------------------------------------------------------------------------
            
            // ---------------------------------------------------------------------------
            #region Flip
            // ---------------------------------------------------------------------------
            
            // Toggle Flip Axis
            public static void ToggleFlipAxis()
            {
                // Switch to next axis
                switch (flipAxis)
                {
                    case Axis.X:
                        flipAxis = Axis.Y;
                        break;
                    case Axis.Y:
                        flipAxis = Axis.Z;
                        break;
                    case Axis.Z:
                        flipAxis = Axis.X;
                        break;
                }
            }
            
            // Get Current Flip Axis
            public static Axis GetCurrentFlipAxis()
            {
                return flipAxis;
            }
            
            // Flip the visualizer or whatever object is selected
            public static GameObject FlipObject()
            {
                GameObject gameObject = GetObjectToManipulate(Visualizer.visualizerGameObject);
                
                if (gameObject != null)
                {
                    // Make this an Undo point, just before flipping the existing object
                    if (allowUndoRegistering)
                    {
                        Undo.RegisterCompleteObjectUndo(gameObject.transform, "Flipped GameObject");
                        allowUndoRegistering = false;
                    }
                    
                    // Get local scale
                    float xScale = gameObject.transform.localScale.x;
                    float yScale = gameObject.transform.localScale.y;
                    float zScale = gameObject.transform.localScale.z;
                    
                    // Flip along target axis
                    switch (flipAxis)
                    {
                        case Axis.X:
                            xScale = -xScale;
                            break;
                        case Axis.Y:
                            yScale = -yScale;
                            break;
                        case Axis.Z:
                            zScale = -zScale;
                            break;
                    }
                    
                    // Save new local scale
                    gameObject.transform.localScale = new Vector3(xScale, yScale, zScale);
                }
                
                // Return flipped GameObject
                return gameObject;
            }
            
            #endregion
            // ---------------------------------------------------------------------------
            
            // Get target gameobject - If no visualizer exists, then use whatever object is selected
            private static GameObject GetObjectToManipulate(GameObject targetGameObject)
            {
                // If no visualizer exists
                if (targetGameObject == null)
                {
                    // If a GameObject is selected in the Hierarchy/Scene
                    if (Selection.activeGameObject != null)
                    {
                        // Make it the target GameObject
                        targetGameObject = Selection.activeGameObject;
                        
                        // Allow Undo registering
                        allowUndoRegistering = true;
                    }
                }
                
                // Return the target GameObject
                return targetGameObject;
            }
        }
    }
}

