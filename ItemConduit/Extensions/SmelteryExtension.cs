using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Extensions
{
	/// <summary>
	/// Extension for Smelter objects with node notification
	/// </summary>
	public class SmelteryExtension : BaseExtension, IContainerInterface
	{
		private Smelter smelter;
		public Container m_container;
		public OutputSwitch m_outputSwitch;
		public bool autoOutput;
		private BoxCollider m_outputCollider;
		public bool blocking { get; set; } = false;

		protected override void Awake()
		{
			base.Awake();
			smelter = GetComponentInParent<Smelter>();
			if (smelter == null)
			{
				smelter = GetComponent<Smelter>();
				if (smelter == null) smelter = GetComponentInChildren<Smelter>();
			}

			if(smelter == null)
			{
				Logger.LogError($"[ItemConduit] SmelteryExtension could not find Smelter component!");
				return;
			}

			ZNetView znetView = smelter.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid())
			{
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Skipping container creation - invalid ZNetView");
				}
				return;
			}

			SetupContainer();
			SetupOutputSwitch();


			//m_inventory = new Inventory("Smelter Output", null, 1, 1);
			//base.InvokeRepeating("CheckForChanges", 0f, 1f);
		}

		private void SetupContainer()
		{
			m_container = smelter.gameObject.AddComponent<Container>();
			m_container.m_width = 1;
			m_container .m_height = 1;
			m_container.m_inventory = new Inventory("Smelter Output", null, 1, 1);
			m_container.name = "Smelter Output";
		}



		private void SetupOutputSwitch()
		{
			GameObject switchObject = new GameObject("OutputSwitch");

			if (smelter.m_outputPoint != null)
			{
				switchObject.transform.position = smelter.m_outputPoint.position + new Vector3(0, 0, 0);
				switchObject.transform.rotation = smelter.m_outputPoint.rotation;
				switchObject.transform.SetParent(smelter.transform);
			}
			else
			{
				switchObject.transform.SetParent(smelter.transform);
				switchObject.transform.localPosition = new Vector3(0, 1f, 1f); // Default value if m_outputPoint = null
			}

			m_outputSwitch = switchObject.AddComponent<OutputSwitch>();
			m_outputSwitch.m_onUse = (OutputSwitch.Callback)Delegate.Combine(m_outputSwitch.m_onUse, new OutputSwitch.Callback(OnOutputSwitch));
			m_outputSwitch.m_onHover = new OutputSwitch.hoverCallback(OnOutputHover);

			m_outputCollider = switchObject.AddComponent<BoxCollider>();
			m_outputCollider.size = new Vector3(0.5f, 0.5f, 0.5f); // Adjust the interaction size as needed
			m_outputCollider.isTrigger = true;

			if (DebugConfig.showDebug.Value)
			{
				CreateColliderWireframe(switchObject, m_outputCollider);
			}
			m_outputCollider.enabled = this.IsConnected;
		}

		protected void Update()
		{
			m_outputCollider.enabled = this.IsConnected;
		}



		private void CreateColliderWireframe(GameObject switchObject, BoxCollider collider)
		{
			// Create wireframe visualization
			GameObject wireframe = GameObject.CreatePrimitive(PrimitiveType.Cube);
			wireframe.name = "OutputSwitch_Wireframe";
			wireframe.transform.SetParent(switchObject.transform);
			wireframe.transform.localPosition = collider.center;
			wireframe.transform.localRotation = Quaternion.identity;
			wireframe.transform.localScale = collider.size;

			// Remove the collider from the visualization
			Destroy(wireframe.GetComponent<BoxCollider>());

			// Make it wireframe-only
			MeshRenderer renderer = wireframe.GetComponent<MeshRenderer>();
			if (renderer != null)
			{
				// Create a simple colored material
				Material wireMaterial = new Material(Shader.Find("Sprites/Default"));
				wireMaterial.color = new Color(0f, 1f, 0f, 0.3f); // Green, semi-transparent
				renderer.material = wireMaterial;
			}

			// Alternative: Create actual wireframe lines (more like your NodeVisualizer)
			CreateWireframeLines(switchObject, collider);
		}

		private void CreateWireframeLines(GameObject switchObject, BoxCollider collider)
		{
			// Create a parent for all wireframe lines
			GameObject wireframeParent = new GameObject("OutputSwitch_WireframeLines");
			wireframeParent.transform.SetParent(switchObject.transform);
			wireframeParent.transform.localPosition = Vector3.zero;

			Vector3 center = collider.center;
			Vector3 size = collider.size;

			// Calculate the 8 corners of the box
			Vector3[] corners = new Vector3[8];
			corners[0] = center + new Vector3(-size.x, -size.y, -size.z) * 0.5f;
			corners[1] = center + new Vector3(size.x, -size.y, -size.z) * 0.5f;
			corners[2] = center + new Vector3(size.x, -size.y, size.z) * 0.5f;
			corners[3] = center + new Vector3(-size.x, -size.y, size.z) * 0.5f;
			corners[4] = center + new Vector3(-size.x, size.y, -size.z) * 0.5f;
			corners[5] = center + new Vector3(size.x, size.y, -size.z) * 0.5f;
			corners[6] = center + new Vector3(size.x, size.y, size.z) * 0.5f;
			corners[7] = center + new Vector3(-size.x, size.y, size.z) * 0.5f;

			// Define the 12 edges of a box
			int[,] edges = new int[,] {
		{0,1}, {1,2}, {2,3}, {3,0},  // Bottom edges
        {4,5}, {5,6}, {6,7}, {7,4},  // Top edges
        {0,4}, {1,5}, {2,6}, {3,7}   // Vertical edges
    };

			// Create line renderers for each edge
			for (int i = 0; i < 12; i++)
			{
				GameObject edge = new GameObject($"Edge_{i}");
				edge.transform.SetParent(wireframeParent.transform);

				LineRenderer line = edge.AddComponent<LineRenderer>();
				line.material = new Material(Shader.Find("Sprites/Default"));
				line.startColor = line.endColor = new Color(0f, 1f, 1f, 0.8f); // Cyan color
				line.startWidth = line.endWidth = 0.01f;
				line.positionCount = 2;
				line.useWorldSpace = false;

				// Set the line positions
				line.SetPosition(0, corners[edges[i, 0]]);
				line.SetPosition(1, corners[edges[i, 1]]);
			}
		}

		private bool OnOutputSwitch(Humanoid user, bool hold, bool alt)
		{
			if (m_container == null || m_container.m_inventory == null)
			{
				Logger.LogWarning("[ItemConduit] Output container or inventory is null!");
				return false;
			}

			// Only show for local player
			if (user == Player.m_localPlayer)
			{
				// Use the existing container directly
				InventoryGui.instance.Show(m_container, 1);
			}

			return true;
		}


		private string OnOutputHover()
		{
			return Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>] Smelter Output");
		}

		public override void OnNodeDisconnected(BaseNode node)
		{
			base.OnNodeDisconnected(node);

			// If no more nodes are connected and we have processed ore, spawn them
			if (!IsConnected && m_container.m_inventory.GetAllItems().Count > 0)
			{
				foreach (var item in m_container.m_inventory.GetAllItems())
				{
					ItemDrop.DropItem(item, 0, smelter.m_outputPoint.transform.position, smelter.m_outputPoint.transform.rotation);
				}
				m_container.m_inventory.RemoveAll();
				blocking = false;
			}
		}

		#region IContainerInterface Implementation

		public int CalculateAcceptCapacity(ItemDrop.ItemData sourceItem, int desiredAmount)
		{
			if (sourceItem == null || desiredAmount <= 0 || smelter == null) return 0;
			if (!CanAddItem(sourceItem))
			{
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[SmelteryExtension] Cannot accept item - CanAddItem returned false");
				}
				return 0;
			}

			string type = ItemType(sourceItem);

			int acceptableAmount = 0;
			if (type == "Fuel")
			{
				var maxFuel = smelter.m_maxFuel;
				var currentFuel = smelter.GetFuel();
				acceptableAmount = (int)(maxFuel - currentFuel);

				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[SmelteryExtension] Fuel capacity: {currentFuel}/{maxFuel}, can accept: {acceptableAmount}");
				}
			}
			else if (type == "Ore" && smelter.m_maxOre > 0)
			{
				var maxOre = smelter.m_maxOre;
				var currentOreSize = smelter.GetQueueSize();
				acceptableAmount = maxOre - currentOreSize;
			}

			if (acceptableAmount <= 0) return 0;

			return Mathf.Min(desiredAmount, (int)acceptableAmount);
		}

		public bool CanAddItem(ItemDrop.ItemData item)
		{
			if (smelter == null || item == null) return false;

			// Debug logging to help diagnose
			if (DebugConfig.showDebug.Value)
			{
				string itemName = item.m_dropPrefab?.name ?? item.m_shared?.m_name ?? "Unknown";
				Logger.LogInfo($"[SmelteryExtension] Checking if can add item: {itemName}");

				string type = ItemType(item);
				Logger.LogInfo($"[SmelteryExtension] Item type detected: {type}");

				if (type == "Invalid")
				{
					Logger.LogInfo($"[SmelteryExtension] Item rejected - not valid fuel or ore");
					if (smelter.m_fuelItem != null)
					{
						Logger.LogInfo($"[SmelteryExtension] Expected fuel: {smelter.m_fuelItem.m_itemData.m_shared.m_name}");
					}
				}
			}

			string itemType = ItemType(item);
			return itemType == "Fuel" || itemType == "Ore";
		}

		public bool CanRemoveItem(ItemDrop.ItemData item)
		{
			List<Smelter.ItemConversion> itemConversions = smelter.m_conversion;
			foreach (var itemConversion in itemConversions) {
				if (itemConversion.m_to.m_itemData.m_dropPrefab.name == item.m_dropPrefab.name)
				{
					return true;
				}
			}
			
			return false;
		}

		public bool AddItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (!CanAddItem(item)) return false;


			int actualAmount = amount > 0 ? amount : item.m_stack;

			int addableAmount = CalculateAcceptCapacity(item, actualAmount);
			if (addableAmount <= 0) return false;


			string type = ItemType(item);

			if (DebugConfig.showDebug.Value)
			{
				Logger.LogInfo($"[SmelteryExtension] Adding {addableAmount}x {item.m_shared.m_name} as {type}");
			}

			if (type == "Fuel")
			{
				AddFuel(addableAmount);
				return true;
			}

			if (type == "Ore")
			{
				string oreName = item.m_dropPrefab?.name ?? "";
				if (!string.IsNullOrEmpty(oreName))
				{
					AddOre(item.m_dropPrefab.name, addableAmount);
					return true;
				}
			}

			return false;
		}

		public void AddFuel(int amount)
		{
			for (int i = 0; i < amount; i++) 
			{
				float currentFuel = smelter.GetFuel();
				smelter.SetFuel(currentFuel + 1);
			}
		}

		public void AddOre(string name,int amount)
		{
			if(!smelter.IsItemAllowed(name)) return;

			for (int i = 0;i < amount; i++)
			{
				smelter.QueueOre(name);
			}
		}

		public bool RemoveItem(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null) return false;
			if (amount <= 0 && item.m_stack <= 0) return false;

			if (m_container.m_inventory.RemoveItem(item, amount)) {
				return true;
			}

			return false;
		}

		public Inventory GetInventory()
		{
			return m_container.m_inventory;
		}

		public bool AddToInventory(ItemDrop.ItemData item, int amount = 0)
		{
			if (item == null) return false;
			if (amount <= 0 && item.m_stack <=0) return false;
			if (!m_container.m_inventory.CanAddItem(item)) return false;

			if (amount > 0 && item.m_stack != amount) {
				item.m_stack = amount;
			}

			if (m_container.m_inventory.AddItem(item))
			{
				return true; 
			}

			return false;
		}

		public string GetName()
		{
			return smelter?.m_name ?? "Smelter";
		}

		public Vector3 GetTransformPosition()
		{
			return transform.position;
		}

		private string ItemType(ItemDrop.ItemData item)
		{
			if (item == null || smelter == null) return "Invalid";

			// Use the smelter's built-in IsItemAllowed method for ores
			string itemName = item.m_dropPrefab?.name ?? "";
			if (!string.IsNullOrEmpty(itemName) && smelter.IsItemAllowed(itemName))
			{
				return "Ore";
			}

			// Check if it's fuel - compare using shared name which is more reliable
			if (smelter.m_fuelItem != null &&
				smelter.m_fuelItem.m_itemData != null &&
				item.m_shared != null &&
				smelter.m_fuelItem.m_itemData.m_shared.m_name == item.m_shared.m_name)
			{
				return "Fuel";
			}

			return "Invalid";
		}

		#endregion
	}

	public class OutputSwitch : MonoBehaviour, Interactable, Hoverable
	{
		public OutputSwitch.Callback m_onUse;
		public OutputSwitch.hoverCallback m_onHover;
		[TextArea(3, 20)]
		public string m_hoverText = "";
		public string m_name = "";
		public float m_holdRepeatInterval = -1f;
		public float m_lastUseTime;

		public delegate bool Callback(Humanoid user, bool hold, bool alt);
		public delegate string hoverCallback();
		
		public bool Interact(Humanoid character, bool hold, bool alt)
		{
			if(hold) return false;

			this.m_lastUseTime = Time.time;
			return this.m_onUse != null && this.m_onUse(character, hold, alt);
		}

		public bool UseItem(Humanoid user, ItemDrop.ItemData item)
		{
			return false;
		}

		public string GetHoverText()
		{
			if(this.m_onHover != null)
			{
				return this.m_onHover();
			}
			return this.m_hoverText;
		}

		public string GetHoverName()
		{
			return m_name;
		}
	}
}