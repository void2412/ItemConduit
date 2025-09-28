using UnityEngine;
using ItemConduit.Nodes;
using ItemConduit.Core;
using Logger = Jotunn.Logger;

namespace ItemConduit.GUI
{
	/// <summary>
	/// GUI for configuring insert nodes
	/// Allows setting channel ID and priority
	/// </summary>
	public class InsertNodeGUI : BaseNodeGUI
	{
		#region Fields

		/// <summary>The insert node being configured</summary>
		private InsertNode node;

		/// <summary>Channel ID input field</summary>
		private string channelInput = "";

		/// <summary>Priority input field</summary>
		private string priorityInput = "0";

		/// <summary>Priority slider value</summary>
		private float prioritySlider = 0f;

		#endregion

		#region Initialization

		/// <summary>
		/// Initialize the GUI with an insert node
		/// </summary>
		/// <param name="insertNode">The node to configure</param>
		public void Initialize(InsertNode insertNode)
		{
			node = insertNode;

			// Load current settings
			channelInput = node.ChannelId ?? "None";
			priorityInput = node.Priority.ToString();
			prioritySlider = node.Priority;

			// Set window title
			windowTitle = "Insert Node Configuration";

			// Set window size (smaller than extract since less options)
			windowRect = new Rect(Screen.width / 2 - 200, Screen.height / 2 - 150, 400, 350);
		}

		#endregion

		#region GUI Drawing

		/// <summary>
		/// Draw the configuration GUI
		/// </summary>
		public override void DrawGUI()
		{
			if (!isVisible || node == null) return;

			// Draw window
			windowRect = UnityEngine.GUI.Window(
				GetInstanceID(),
				windowRect,
				DrawWindow,
				windowTitle,
				windowStyle ?? UnityEngine.GUI.skin.window
			);
		}

		/// <summary>
		/// Draw window contents
		/// </summary>
		private void DrawWindow(int windowID)
		{
			// Close button
			DrawCloseButton();

			GUILayout.BeginVertical();

			// Spacing
			GUILayout.Space(10);

			// Channel configuration section
			DrawChannelSection();

			GUILayout.Space(15);

			// Priority configuration section
			DrawPrioritySection();

			GUILayout.Space(15);

			// Buttons section
			DrawButtons();

			GUILayout.Space(10);

			// Info section
			DrawInfoSection();

			GUILayout.EndVertical();

			// Make window draggable
			MakeWindowDraggable();
		}

		/// <summary>
		/// Draw channel configuration section
		/// </summary>
		private void DrawChannelSection()
		{
			GUILayout.Label("Channel ID:", labelStyle ?? UnityEngine.GUI.skin.label);
			GUILayout.Label("Will only receive items from Extract nodes with matching channel",
				new GUIStyle(UnityEngine.GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Italic });

			channelInput = GUILayout.TextField(channelInput ?? "", GUILayout.Height(25));

			// Quick channel buttons (same as extract for consistency)
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("None", GUILayout.Width(60)))
			{
				channelInput = "None";
			}
			if (GUILayout.Button("Ore", GUILayout.Width(60)))
			{
				channelInput = "Ore";
			}
			if (GUILayout.Button("Food", GUILayout.Width(60)))
			{
				channelInput = "Food";
			}
			if (GUILayout.Button("Materials", GUILayout.Width(80)))
			{
				channelInput = "Materials";
			}
			GUILayout.EndHorizontal();
		}

		/// <summary>
		/// Draw priority configuration section
		/// </summary>
		private void DrawPrioritySection()
		{
			GUILayout.Label("Priority:", labelStyle ?? UnityEngine.GUI.skin.label);
			GUILayout.Label("Higher priority containers are filled first (range: -100 to 100)",
				new GUIStyle(UnityEngine.GUI.skin.label) { fontSize = 10, fontStyle = FontStyle.Italic });

			// Priority slider
			GUILayout.BeginHorizontal();
			GUILayout.Label("-100", GUILayout.Width(30));
			prioritySlider = GUILayout.HorizontalSlider(prioritySlider, -100f, 100f);
			GUILayout.Label("100", GUILayout.Width(30));
			GUILayout.EndHorizontal();

			// Priority text input
			GUILayout.BeginHorizontal();
			GUILayout.Label("Value:", GUILayout.Width(50));
			priorityInput = GUILayout.TextField(priorityInput ?? "0", GUILayout.Width(60));

			// Sync slider and text
			if (Event.current.type == EventType.Repaint)
			{
				// Update text from slider
				int sliderValue = Mathf.RoundToInt(prioritySlider);
				if (priorityInput != sliderValue.ToString())
				{
					priorityInput = sliderValue.ToString();
				}
			}

			// Parse text to update slider
			if (int.TryParse(priorityInput, out int parsedPriority))
			{
				parsedPriority = Mathf.Clamp(parsedPriority, -100, 100);
				prioritySlider = parsedPriority;
			}

			GUILayout.EndHorizontal();

			// Priority presets
			GUILayout.Label("Quick set:", new GUIStyle(UnityEngine.GUI.skin.label) { fontSize = 10 });
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Lowest\n(-100)", GUILayout.Width(70), GUILayout.Height(35)))
			{
				SetPriority(-100);
			}
			if (GUILayout.Button("Low\n(-50)", GUILayout.Width(70), GUILayout.Height(35)))
			{
				SetPriority(-50);
			}
			if (GUILayout.Button("Normal\n(0)", GUILayout.Width(70), GUILayout.Height(35)))
			{
				SetPriority(0);
			}
			if (GUILayout.Button("High\n(50)", GUILayout.Width(70), GUILayout.Height(35)))
			{
				SetPriority(50);
			}
			if (GUILayout.Button("Highest\n(100)", GUILayout.Width(70), GUILayout.Height(35)))
			{
				SetPriority(100);
			}
			GUILayout.EndHorizontal();
		}

		/// <summary>
		/// Set priority value
		/// </summary>
		private void SetPriority(int value)
		{
			priorityInput = value.ToString();
			prioritySlider = value;
		}

		/// <summary>
		/// Draw action buttons
		/// </summary>
		private void DrawButtons()
		{
			GUILayout.BeginHorizontal();

			// Apply button
			if (GUILayout.Button("Apply", buttonStyle ?? UnityEngine.GUI.skin.button, GUILayout.Height(30)))
			{
				ApplySettings();
			}

			// Reset button
			if (GUILayout.Button("Reset", buttonStyle ?? UnityEngine.GUI.skin.button, GUILayout.Height(30)))
			{
				ResetSettings();
			}

			// Cancel button
			if (GUILayout.Button("Cancel", buttonStyle ?? UnityEngine.GUI.skin.button, GUILayout.Height(30)))
			{
				Hide();
			}

			GUILayout.EndHorizontal();
		}

		/// <summary>
		/// Draw information section
		/// </summary>
		private void DrawInfoSection()
		{
			// Network info
			string networkInfo = node.NetworkId != null
				? $"Network: {node.NetworkId.Substring(0, 8)}..."
				: "Network: Not Connected";
			GUILayout.Label(networkInfo, UnityEngine.GUI.skin.box);

			// Container info
			Container container = node.GetTargetContainer();
			if (container != null)
			{
				Inventory inv = container.GetInventory();
				if (inv != null)
				{
					int emptySlots = inv.GetEmptySlots();
					int totalSlots = inv.GetWidth() * inv.GetHeight();
					string containerInfo = $"Container: {container.m_name} ({emptySlots} empty slots of {totalSlots})";
					GUILayout.Label(containerInfo, UnityEngine.GUI.skin.box);
				}
				else
				{
					GUILayout.Label("Container: Connected (No Inventory)", UnityEngine.GUI.skin.box);
				}
			}
			else
			{
				GUILayout.Label("Container: Not Connected", UnityEngine.GUI.skin.box);
			}
		}

		#endregion

		#region Settings Management

		/// <summary>
		/// Apply the current settings to the node
		/// </summary>
		private void ApplySettings()
		{
			// Set channel
			string channel = string.IsNullOrWhiteSpace(channelInput) ? "None" : channelInput.Trim();
			node.SetChannel(channel);

			// Set priority
			if (int.TryParse(priorityInput, out int priority))
			{
				priority = Mathf.Clamp(priority, -100, 100);
				node.SetPriority(priority);
			}

			// Log if debug mode
			if (ItemConduitMod.ShowDebugInfo.Value)
			{
				Logger.LogInfo($"[ItemConduit] Applied settings to insert node: Channel={channel}, Priority={priority}");
			}

			// Close GUI
			Hide();
		}

		/// <summary>
		/// Reset settings to current node values
		/// </summary>
		private void ResetSettings()
		{
			channelInput = node.ChannelId ?? "None";
			priorityInput = node.Priority.ToString();
			prioritySlider = node.Priority;
		}

		#endregion
	}
}