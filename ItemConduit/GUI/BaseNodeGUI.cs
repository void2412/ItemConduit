using Jotunn.Managers;
using UnityEngine;

namespace ItemConduit.GUI
{
	/// <summary>
	/// Base class for node configuration GUIs
	/// Provides common functionality for all node GUI windows
	/// </summary>
	public abstract class BaseNodeGUI : MonoBehaviour
	{
		#region Fields

		/// <summary>Whether the GUI window is currently visible</summary>
		protected bool isVisible = false;

		/// <summary>Window position and size</summary>
		protected Rect windowRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 150, 400, 300);

		/// <summary>Window title</summary>
		protected string windowTitle = "Node Configuration";

		/// <summary>GUI style for window</summary>
		protected GUIStyle windowStyle;

		/// <summary>GUI style for labels</summary>
		protected GUIStyle labelStyle;

		/// <summary>GUI style for buttons</summary>
		protected GUIStyle buttonStyle;

		#endregion

		#region Properties

		/// <summary>Check if GUI is currently visible</summary>
		public bool IsVisible => isVisible;

		#endregion

		#region Initialization

		/// <summary>
		/// Initialize GUI styles
		/// </summary>
		protected virtual void InitializeStyles()
		{
			// Window style
			windowStyle = new GUIStyle(UnityEngine.GUI.skin.window);
			windowStyle.normal.textColor = Color.white;
			windowStyle.fontSize = 14;
			windowStyle.fontStyle = FontStyle.Bold;

			// Label style
			labelStyle = new GUIStyle(UnityEngine.GUI.skin.label);
			labelStyle.normal.textColor = Color.white;
			labelStyle.fontSize = 12;

			// Button style
			buttonStyle = new GUIStyle(UnityEngine.GUI.skin.button);
			buttonStyle.fontSize = 12;
		}

		#endregion

		#region Visibility Management

		/// <summary>
		/// Show the GUI window
		/// </summary>
		public virtual void Show()
		{
			isVisible = true;
			GUIManager.Instance.RegisterGUI(this);

			// Initialize styles if needed
			if (windowStyle == null)
			{
				InitializeStyles();
			}

			// Pause game while configuring
			if (Player.m_localPlayer != null)
			{
				// Note: This is a simplified approach. 
				// In a full implementation, you'd properly handle game pause
			}
		}

		/// <summary>
		/// Hide the GUI window
		/// </summary>
		public virtual void Hide()
		{
			isVisible = false;
			GUIManager.Instance.UnregisterGUI(this);

			// Resume game
			if (Player.m_localPlayer != null)
			{
				// Resume game logic
			}
		}

		/// <summary>
		/// Toggle GUI visibility
		/// </summary>
		public void Toggle()
		{
			if (isVisible)
				Hide();
			else
				Show();
		}

		#endregion

		#region GUI Drawing

		/// <summary>
		/// Draw the GUI (called by GUIManager)
		/// </summary>
		public abstract void DrawGUI();

		/// <summary>
		/// Draw the close button in the window
		/// </summary>
		protected void DrawCloseButton()
		{
			if (UnityEngine.GUI.Button(new Rect(windowRect.width - 25, 5, 20, 20), "X"))
			{
				Hide();
			}
		}

		/// <summary>
		/// Make window draggable
		/// </summary>
		protected void MakeWindowDraggable()
		{
			// Make the window draggable by its title bar
			UnityEngine.GUI.DragWindow(new Rect(0, 0, windowRect.width, 20));
		}

		#endregion

		#region Input Handling

		/// <summary>
		/// Check for escape key to close window
		/// </summary>
		protected virtual void Update()
		{
			if (isVisible && Input.GetKeyDown(KeyCode.Escape))
			{
				Hide();
			}
		}

		#endregion
	}
}