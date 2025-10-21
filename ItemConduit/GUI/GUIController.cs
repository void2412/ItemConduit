using System.Collections.Generic;
using UnityEngine;
using ItemConduit.Config;
using HarmonyLib;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	/// <summary>
	/// Manages all GUI windows for the ItemConduit mod
	/// Tracks input field focus and directly manages input blocking
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

		/// <summary>Whether an input field currently has focus (user is typing)</summary>
		private bool hasInputFieldFocus = false;

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

				// Clear input field focus when last GUI closes
				if (!hasActiveGUI)
				{
					hasInputFieldFocus = false;
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
			hasInputFieldFocus = false;
		}

		#endregion

		#region Input Field Focus Tracking

		/// <summary>
		/// Called when an input field gains focus (user starts typing)
		/// </summary>
		public void OnInputFieldFocused()
		{
			hasInputFieldFocus = true;
		}

		/// <summary>
		/// Called when an input field loses focus (user stops typing)
		/// </summary>
		public void OnInputFieldUnfocused()
		{
			hasInputFieldFocus = false;
		}

		/// <summary>
		/// Check if an input field currently has focus
		/// Used by Harmony patches to block input appropriately
		/// </summary>
		public bool HasInputFieldFocus()
		{
			return hasInputFieldFocus;
		}

		#endregion

		#region Unity Events

		/// <summary>
		/// Handle global input and enforce input blocking every frame
		/// </summary>
		private void Update()
		{
			// Ensure cursor is always visible and unlocked when GUI is active
			if (hasActiveGUI)
			{
				Cursor.visible = true;
				Cursor.lockState = CursorLockMode.None;

				// DIRECTLY disable camera mouse capture every frame
				if (GameCamera.instance != null)
				{
					// Use Traverse to access private field
					Traverse.Create(GameCamera.instance).Field("m_mouseCapture").SetValue(false);
				}
			}

			// Close all GUIs when ESC is pressed (unless typing in input field)
			if (hasActiveGUI && Input.GetKeyDown(KeyCode.Escape) && !hasInputFieldFocus)
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