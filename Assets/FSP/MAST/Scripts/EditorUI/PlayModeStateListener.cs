using System;
using UnityEditor;
    

namespace MAST
{
	namespace EditorUI
	{
		[InitializeOnLoad]
		class PlayModeStateListener
		{
		#pragma warning disable 649
			public static Action onEnterPlayMode;
			public static Action onExitPlayMode;
			public static Action onEnterEditMode;
			public static Action onExitEditMode;
		#pragma warning restore 649
			
			static PlayModeStateListener()
			{
				EditorApplication.playModeStateChanged += (x) =>
				{
					if (x == PlayModeStateChange.EnteredEditMode && onEnterEditMode != null)
						onEnterEditMode();
					else if (x == PlayModeStateChange.ExitingEditMode && onExitEditMode != null)
						onExitEditMode();
					else if (x == PlayModeStateChange.EnteredPlayMode && onEnterPlayMode != null)
						onEnterPlayMode();
					else if (x == PlayModeStateChange.ExitingPlayMode && onExitPlayMode != null)
						onExitPlayMode();
				};
			}
		}
	}
}
