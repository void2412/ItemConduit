using BepInEx;
using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ItemConduit.Nodes;
using ItemConduit.Network;
using ItemConduit.GUI;

namespace ItemConduit.Core
{
	/// <summary>
	/// Main plugin class for ItemConduit mod
	/// Handles initialization, configuration, and piece registration
	/// </summary>
	[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	[BepInDependency(Jotunn.Main.ModGuid)]
	[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
	public class ItemConduitMod : BaseUnityPlugin
	{
		// Plugin metadata
		public const string PluginGUID = "com.yourname.itemconduit";
		public const string PluginName = "ItemConduit";
		public const string PluginVersion = "1.0.0";

		// Singleton instance for global access
		private static ItemConduitMod _instance;
		public static ItemConduitMod Instance => _instance;

		// Configuration entries accessible throughout the mod
		public static ConfigEntry<float> TransferRate;
		public static ConfigEntry<int> MaxNetworkSize;
		public static ConfigEntry<float> TransferInterval;
		public static ConfigEntry<bool> ShowDebugInfo;
		public static ConfigEntry<bool> EnableVisualEffects;
		public static ConfigEntry<float> ConnectionRange;
		public static ConfigEntry<float> EndpointConnectionThreshold;

		// Harmony instance for runtime patching
		private Harmony harmony;

		/// <summary>
		/// Plugin initialization - called by BepInEx
		/// </summary>
		private void Awake()
		{
			// Set singleton instance
			_instance = this;

			// Load mod configuration from file
			LoadConfiguration();

			// Initialize Harmony patches for game integration
			harmony = new Harmony(PluginGUID);
			harmony.PatchAll();

			// Register callback for when vanilla prefabs are loaded
			PrefabManager.OnVanillaPrefabsAvailable += RegisterPieces;

			// Initialize the network management system
			ItemConduit.Network.NetworkManager.Instance.Initialize();


			Jotunn.Logger.LogInfo($"{PluginName} v{PluginVersion} initialized successfully!");
		}

		/// <summary>
		/// Register console commands for debugging
		/// </summary>


		/// <summary>
		/// Load and bind configuration values from BepInEx config file
		/// Removed MaxNetworkSize to make networks unlimited
		/// </summary>
		private void LoadConfiguration()
		{
			// General Settings
			TransferRate = Config.Bind(
				"General",
				"TransferRate",
				5f,
				new ConfigDescription(
					"Number of items transferred per second",
					new AcceptableValueRange<float>(0.1f, 100f)
				)
			);

			TransferInterval = Config.Bind(
				"General",
				"TransferInterval",
				0.5f,
				new ConfigDescription(
					"Interval between transfer operations in seconds",
					new AcceptableValueRange<float>(0.1f, 5f)
				)
			);

			ConnectionRange = Config.Bind(
				"General",
				"ConnectionRange",
				1f,
				new ConfigDescription(
					"Additional range for node connections in meters",
					new AcceptableValueRange<float>(0.1f, 2f)
				)
			);

			// Visual Settings
			EnableVisualEffects = Config.Bind(
				"Visual",
				"EnableVisualEffects",
				true,
				"Enable visual effects for item transfers"
			);

			// Debug Settings
			ShowDebugInfo = Config.Bind(
				"Debug",
				"ShowDebugInfo",
				false,
				"Show debug information in console and game"
			);

			// Performance Settings
			var maxFrameTime = Config.Bind(
				"Performance",
				"MaxFrameTime",
				2f,
				new ConfigDescription(
					"Maximum time per frame for network operations in milliseconds",
					new AcceptableValueRange<float>(0.5f, 10f)
				)
			);

			var rebuildInterval = Config.Bind(
				"Performance",
				"RebuildCheckInterval",
				0.1f,
				new ConfigDescription(
					"How often to check for network rebuilds in seconds",
					new AcceptableValueRange<float>(0.05f, 1f)
				)
			);
		}

		/// <summary>
		/// Register all node pieces with the game's building system
		/// </summary>
		private void RegisterPieces()
		{
			try
			{
				// Register each type of node
				RegisterConduitNodes();
				RegisterExtractNodes();
				RegisterInsertNodes();

				// Unsubscribe after successful registration
				PrefabManager.OnVanillaPrefabsAvailable -= RegisterPieces;

				Jotunn.Logger.LogInfo($"Successfully registered all {PluginName} pieces");
			}
			catch (System.Exception ex)
			{
				Jotunn.Logger.LogError($"Failed to register pieces: {ex.Message}");
			}
		}

		/// <summary>
		/// Register conduit node variants (1m, 2m)
		/// </summary>
		private void RegisterConduitNodes()
		{
			// 1 meter conduit
			RegisterNode(
				"wood_beam_1",
				"node_conduit_1m",
				"Conduit Node (1m)",
				"Connects extract and insert nodes\nLength: 1 meter",
				1f,
				NodeType.Conduit,
				2, 1 // Wood and Bronze requirements
			);

			// 2 meter conduit
			RegisterNode(
				"wood_beam",
				"node_conduit_2m",
				"Conduit Node (2m)",
				"Connects extract and insert nodes\nLength: 2 meters",
				2f,
				NodeType.Conduit,
				3, 1
			);
		}

		/// <summary>
		/// Register extract node variants (1m, 2m)
		/// </summary>
		private void RegisterExtractNodes()
		{
			// 1 meter extract node
			RegisterNode(
				"wood_beam_1",
				"node_extract_1m",
				"Extract Node (1m)",
				"Pulls items from containers\n[E] to configure\nLength: 1 meter",
				1f,
				NodeType.Extract,
				3, 2
			);

			// 2 meter extract node
			RegisterNode(
				"wood_beam",
				"node_extract_2m",
				"Extract Node (2m)",
				"Pulls items from containers\n[E] to configure\nLength: 2 meters",
				2f,
				NodeType.Extract,
				4, 2
			);
		}

		/// <summary>
		/// Register insert node variants (1m, 2m)
		/// </summary>
		private void RegisterInsertNodes()
		{
			// 1 meter insert node
			RegisterNode(
				"wood_beam_1",
				"node_insert_1m",
				"Insert Node (1m)",
				"Pushes items into containers\n[E] to configure\nLength: 1 meter",
				1f,
				NodeType.Insert,
				3, 2
			);

			// 2 meter insert node
			RegisterNode(
				"wood_beam",
				"node_insert_2m",
				"Insert Node (2m)",
				"Pushes items into containers\n[E] to configure\nLength: 2 meters",
				2f,
				NodeType.Insert,
				4, 2
			);
		}

		/// <summary>
		/// Register a single node piece with the game's building system
		/// </summary>
		private void RegisterNode(string prefabClone, string prefabName, string displayName, string description,
			float length, NodeType nodeType, int woodCost, int bronzeCost)
		{
			// Clone wood_beam as base prefab for our nodes
			GameObject beamPrefab = PrefabManager.Instance.GetPrefab(prefabClone);
			if (beamPrefab == null)
			{
				Jotunn.Logger.LogError($"Could not find {prefabClone} prefab for cloning");
				return;
			}

			// Create cloned prefab with our name
			GameObject nodePrefab = PrefabManager.Instance.CreateClonedPrefab(prefabName, beamPrefab);
			if (nodePrefab == null)
			{
				Jotunn.Logger.LogError($"Failed to create {prefabName} prefab");
				return;
			}

			// Remove any existing node components (in case of re-registration)
			foreach (var existingNode in nodePrefab.GetComponents<BaseNode>())
			{
				DestroyImmediate(existingNode);
			}

			// Add appropriate component based on node type
			BaseNode nodeComponent = null;
			switch (nodeType)
			{
				case NodeType.Conduit:
					nodeComponent = nodePrefab.AddComponent<ConduitNode>();
					break;
				case NodeType.Extract:
					nodeComponent = nodePrefab.AddComponent<ExtractNode>();
					break;
				case NodeType.Insert:
					nodeComponent = nodePrefab.AddComponent<InsertNode>();
					break;
			}

			// Configure node component properties BEFORE prefab is instantiated
			if (nodeComponent != null)
			{
				nodeComponent.NodeLength = length;
				nodeComponent.NodeType = nodeType;

				Debug.Log($"[ItemConduit] Set node {prefabName} length to {length}m (verification: {nodeComponent.NodeLength}m)");
			}

			// Configure piece component for building system
			Piece piece = nodePrefab.GetComponent<Piece>();
			if (piece != null)
			{
				piece.m_name = displayName;
				piece.m_description = description;
				piece.m_category = Piece.PieceCategory.Misc;

				// Add connection snap points for proper alignment
				AddSnapPoints(nodePrefab, length);

				// Add visual customization based on node type
				CustomizeNodeVisuals(nodePrefab, nodeType);
			}

			// Create piece configuration for registration
			PieceConfig pieceConfig = new PieceConfig
			{
				Name = displayName,
				Description = description,
				PieceTable = "Hammer",
				Category = "Misc",
				Requirements = new[]
				{
					new RequirementConfig
					{
						Item = "Wood",
						Amount = woodCost,
						Recover = true
					},
					new RequirementConfig
					{
						Item = "Bronze",
						Amount = bronzeCost,
						Recover = true
					}
				}
			};

			// Register with Jotunn
			CustomPiece customPiece = new CustomPiece(nodePrefab, false, pieceConfig);
			PieceManager.Instance.AddPiece(customPiece);

			// Add a post-registration hook to ensure values persist
			PieceManager.OnPiecesRegistered += () =>
			{
				var registeredPrefab = PrefabManager.Instance.GetPrefab(prefabName);
				if (registeredPrefab != null)
				{
					var registeredNode = registeredPrefab.GetComponent<BaseNode>();
					if (registeredNode != null)
					{
						registeredNode.NodeLength = length;
						registeredNode.NodeType = nodeType;
						Debug.Log($"[ItemConduit] Post-registration: Set {prefabName} length to {length}m, type to {nodeType}");
					}
				}
			};

			if (ShowDebugInfo.Value)
			{
				Jotunn.Logger.LogInfo($"Registered {displayName} [{prefabName}] with length {length}m");
			}
		}

		/// <summary>
		/// Add snap points to node prefab for connections
		/// Snap points are placed at the front and back of the node
		/// </summary>
		private void AddSnapPoints(GameObject prefab, float length)
		{
			// Clear any existing snap points
			foreach (Transform child in prefab.transform)
			{
				if (child.name.Contains("snappoint"))
				{
					DestroyImmediate(child.gameObject);
				}
			}

			// Wood beams in Valheim extend along the Z axis (forward/back)
			Vector3 forwardAxis = Vector3.forward;

			// Create snap point at the FRONT of node (positive Z)
			GameObject snapPointFront = new GameObject("snappoint_front");
			snapPointFront.transform.SetParent(prefab.transform);
			snapPointFront.transform.localPosition = forwardAxis * (length / 2f);
			snapPointFront.tag = "snappoint";

			// Add visual indicator in debug mode for front (cyan)
			if (ShowDebugInfo.Value)
			{
				GameObject sphereFront = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				sphereFront.transform.SetParent(snapPointFront.transform);
				sphereFront.transform.localPosition = Vector3.zero;
				sphereFront.transform.localScale = Vector3.one * 0.2f;

				Destroy(sphereFront.GetComponent<Collider>());
				var rendererFront = sphereFront.GetComponent<Renderer>();
				if (rendererFront != null)
				{
					rendererFront.material.color = Color.cyan;
				}
			}

			// Create snap point at the BACK of node (negative Z)
			GameObject snapPointBack = new GameObject("snappoint_back");
			snapPointBack.transform.SetParent(prefab.transform);
			snapPointBack.transform.localPosition = -forwardAxis * (length / 2f);
			snapPointBack.tag = "snappoint";

			// Add visual indicator in debug mode for back (magenta)
			if (ShowDebugInfo.Value)
			{
				GameObject sphereBack = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				sphereBack.transform.SetParent(snapPointBack.transform);
				sphereBack.transform.localPosition = Vector3.zero;
				sphereBack.transform.localScale = Vector3.one * 0.2f;

				Destroy(sphereBack.GetComponent<Collider>());
				var rendererBack = sphereBack.GetComponent<Renderer>();
				if (rendererBack != null)
				{
					rendererBack.material.color = Color.magenta;
				}
			}

			Debug.Log($"[ItemConduit] Added snap points - Front: {snapPointFront.transform.localPosition}, Back: {snapPointBack.transform.localPosition} for {prefab.name}");
		}

		/// <summary>
		/// Customize visual appearance based on node type
		/// </summary>
		private void CustomizeNodeVisuals(GameObject prefab, NodeType nodeType)
		{
			// Get renderer to modify material
			MeshRenderer renderer = prefab.GetComponentInChildren<MeshRenderer>();
			if (renderer == null) return;

			// Apply tint based on node type
			Color tint = nodeType switch
			{
				NodeType.Conduit => Color.gray,        // Gray for conduits
				NodeType.Extract => Color.green,       // Green for extract
				NodeType.Insert => Color.blue,         // Blue for insert
				_ => Color.white
			};

			// Clone the material to avoid affecting the original
			if (renderer.sharedMaterial != null)
			{
				Material newMaterial = new Material(renderer.sharedMaterial);
				newMaterial.color = tint;
				renderer.sharedMaterial = newMaterial;
			}
		}

		/// <summary>
		/// Cleanup on mod shutdown
		/// </summary>
		private void OnDestroy()
		{
			// Unpatch Harmony patches
			harmony?.UnpatchSelf();

			// Shutdown network manager
			ItemConduit.Network.NetworkManager.Instance?.Shutdown();

			Jotunn.Logger.LogInfo($"{PluginName} shutdown complete");
		}
	}

	/// <summary>
	/// Enumeration of node types in the conduit system
	/// </summary>
	public enum NodeType
	{
		/// <summary>Connection node that links extract and insert nodes</summary>
		Conduit,

		/// <summary>Source node that extracts items from containers</summary>
		Extract,

		/// <summary>Destination node that inserts items into containers</summary>
		Insert
	}
}