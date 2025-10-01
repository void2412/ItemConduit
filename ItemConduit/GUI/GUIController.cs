using System.Collections.Generic;
using UnityEngine;
using ItemConduit.Config;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	/// <summary>
	/// Manages all GUI windows for the ItemConduit mod
	/// Renamed from GUIManager to avoid conflict with Jotunn.Managers.GUIManager
	/// </summary>
	public class GUIController : MonoBehaviour
	{
		#region Singleton

		private static GUIController _instance;
		public static GUIController Instance
		{
			get
			{
				if (_instance == null)
				{
					GameObject go = new GameObject("ItemConduit_GUIController");
					_instance = go.AddComponent<GUIController>();
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
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Registered GUI: {gui.GetType().Name}");
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
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Unregistered GUI: {gui.GetType().Name}");
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

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo("[ItemConduit] Enabled GUI mode");
			}
		}

		/// <summary>
		/// Disable GUI mode (restore cursor)
		/// </summary>
		private void DisableGUIMode()
		{
			// Restore cursor state
			Cursor.lockState = previousCursorLockMode;
			Cursor.visible = previousCursorVisible;

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo("[ItemConduit] Disabled GUI mode");
			}
		}

		#endregion

		#region Unity Events

		/// <summary>
		/// Handle escape key globally
		/// </summary>
		private void Update()
		{
			// Close all GUIs when ESC is pressed
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

		/// <summary>
		/// Get list of active GUIs (for debugging)
		/// </summary>
		public IEnumerable<BaseNodeGUI> GetActiveGUIs()
		{
			return new List<BaseNodeGUI>(activeGUIs);
		}

		#endregion
	}
}