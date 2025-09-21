using System.Collections.Generic;
using UnityEngine;
using ItemConduit.Core;

namespace ItemConduit.GUI
{
	/// <summary>
	/// Manages all GUI windows for the ItemConduit mod
	/// Handles GUI registration and rendering
	/// </summary>
	public class GUIManager : MonoBehaviour
	{
		#region Singleton

		private static GUIManager _instance;
		public static GUIManager Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("ItemConduit_GUIManager");
					_instance = go.AddComponent<GUIManager>();
					DontDestroyOnLoad(go);
				}
				return _instance;
			}
		}

		#endregion

		#region Fields

		/// <summary>Set of active GUI windows</summary>
		private HashSet<BaseNodeGUI> activeGUIs = new HashSet<BaseNodeGUI>();

		/// <summary>Whether any GUI is currently active</summary>
		private bool hasActiveGUI = false;

		/// <summary>Previous cursor lock state</summary>
		private CursorLockMode previousCursorLockMode;

		/// <summary>Previous cursor visibility</summary>
		private bool previousCursorVisible;

		#endregion

		#region GUI Registration

		/// <summary>
		/// Register a GUI window as active
		/// </summary>
		public void RegisterGUI(BaseNodeGUI gui)
		{
			if (gui == null) return;

			if (activeGUIs.Add(gui))
			{
				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Registered GUI: {gui.GetType().Name}");
				}

				// Update cursor state when first GUI is opened
				if (!hasActiveGUI)
				{
					EnableGUIMode();
				}

				hasActiveGUI = activeGUIs.Count > 0;
			}
		}

		/// <summary>
		/// Unregister a GUI window
		/// </summary>
		public void UnregisterGUI(BaseNodeGUI gui)
		{
			if (gui == null) return;

			if (activeGUIs.Remove(gui))
			{
				if (ItemConduitMod.ShowDebugInfo.Value)
				{
					Debug.Log($"[ItemConduit] Unregistered GUI: {gui.GetType().Name}");
				}

				hasActiveGUI = activeGUIs.Count > 0;

				// Restore cursor state when last GUI is closed
				if (!hasActiveGUI)
				{
					DisableGUIMode();
				}
			}
		}

		/// <summary>
		/// Close all active GUIs
		/// </summary>
		public void CloseAll()
		{
			var guisToClose = new List<BaseNodeGUI>(activeGUIs);

			foreach (var gui in guisToClose)
			{
				if (gui != null)
				{
					gui.Hide();
				}
			}

			activeGUIs.Clear();
			hasActiveGUI = false;
			DisableGUIMode();
		}

		#endregion

		#region Cursor Management

		/// <summary>
		/// Enable GUI mode (show cursor)
		/// Player input blocking is handled by Harmony patches
		/// </summary>
		private void EnableGUIMode()
		{
			// Store previous cursor state
			previousCursorLockMode = Cursor.lockState;
			previousCursorVisible = Cursor.visible;

			// Enable cursor for GUI interaction
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}

		/// <summary>
		/// Disable GUI mode (restore cursor)
		/// </summary>
		private void DisableGUIMode()
		{
			// Restore cursor state
			Cursor.lockState = previousCursorLockMode;
			Cursor.visible = previousCursorVisible;
		}

		#endregion

		#region Unity Events

		/// <summary>
		/// Render all active GUIs
		/// </summary>
		private void OnGUI()
		{
			if (!hasActiveGUI) return;

			// Draw background overlay
			if (activeGUIs.Count > 0)
			{
				DrawBackgroundOverlay();
			}

			// Draw each active GUI
			foreach (var gui in activeGUIs)
			{
				if (gui != null && gui.IsVisible)
				{
					try
					{
						gui.DrawGUI();
					}
					catch (System.Exception ex)
					{
						Debug.LogError($"[ItemConduit] Error drawing GUI: {ex.Message}");
					}
				}
			}
		}

		/// <summary>
		/// Draw semi-transparent background overlay
		/// </summary>
		private void DrawBackgroundOverlay()
		{
			UnityEngine.GUI.color = new Color(0, 0, 0, 0.5f);
			UnityEngine.GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
			UnityEngine.GUI.color = Color.white;
		}

		/// <summary>
		/// Handle escape key globally
		/// </summary>
		private void Update()
		{
			if (hasActiveGUI && Input.GetKeyDown(KeyCode.Escape))
			{
				CloseAll();
			}
		}

		/// <summary>
		/// Clean up on destruction
		/// </summary>
		private void OnDestroy()
		{
			CloseAll();
		}

		#endregion

		#region Utility Methods

		/// <summary>
		/// Check if any GUI is currently active
		/// Used by Harmony patches to block input
		/// </summary>
		public bool HasActiveGUI()
		{
			return hasActiveGUI;
		}

		/// <summary>
		/// Get count of active GUIs
		/// </summary>
		public int GetActiveGUICount()
		{
			return activeGUIs.Count;
		}

		#endregion
	}
}