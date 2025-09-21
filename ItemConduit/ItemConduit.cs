using BepInEx;
using BepInEx.Configuration;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
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
		/// Load and bind configuration values from BepInEx config file
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

			MaxNetworkSize = Config.Bind(
				"General",
				"MaxNetworkSize",
				1000,
				new ConfigDescription(
					"Maximum number of nodes allowed in a single network",
					new AcceptableValueRange<int>(10, 5000)
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
		/// Register extract node variants (1m, 2m, 4m)
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
		/// Register insert node variants (1m, 2m, 4m)
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
		/// <param name="prefabName">Internal name of the prefab</param>
		/// <param name="displayName">Display name shown to player</param>
		/// <param name="description">Description shown in build menu</param>
		/// <param name="length">Length of the node in meters</param>
		/// <param name="nodeType">Type of node (Conduit/Extract/Insert)</param>
		/// <param name="woodCost">Amount of wood required to build</param>
		/// <param name="bronzeCost">Amount of bronze required to build</param>
		private void RegisterNode(string prefabClone, string prefabName, string displayName, string description,
			float length, NodeType nodeType, int woodCost, int bronzeCost)
		{
			// Clone wood_beam as base prefab for our nodes
			GameObject beamPrefab = PrefabManager.Instance.GetPrefab(prefabClone);
			if (beamPrefab == null)
			{
				Jotunn.Logger.LogError("Could not find wood_beam prefab for cloning");
				return;
			}

			// Create cloned prefab with our name
			GameObject nodePrefab = PrefabManager.Instance.CreateClonedPrefab(prefabName, beamPrefab);
			if (nodePrefab == null)
			{
				Jotunn.Logger.LogError($"Failed to create {prefabName} prefab");
				return;
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

			// Configure node component properties
			if (nodeComponent != null)
			{
				nodeComponent.NodeLength = length;
				nodeComponent.NodeType = nodeType;
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

			if (ShowDebugInfo.Value)
			{
				Jotunn.Logger.LogInfo($"Registered {displayName} [{prefabName}]");
			}
		}

		/// <summary>
		/// Add snap points to node prefab for connections
		/// </summary>
		private void AddSnapPoints(GameObject prefab, float length)
		{
			// Create snap point at start of node
			GameObject snapPoint1 = new GameObject("snappoint_start");
			snapPoint1.transform.SetParent(prefab.transform);
			snapPoint1.transform.localPosition = new Vector3(-length / 2f, 0, 0);
			snapPoint1.tag = "snappoint";

			// Add small sphere for debug visualization
			if (ShowDebugInfo.Value)
			{
				GameObject sphere1 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				sphere1.transform.SetParent(snapPoint1.transform);
				sphere1.transform.localScale = Vector3.one * 0.1f;
				Destroy(sphere1.GetComponent<Collider>());
			}

			// Create snap point at end of node
			GameObject snapPoint2 = new GameObject("snappoint_end");
			snapPoint2.transform.SetParent(prefab.transform);
			snapPoint2.transform.localPosition = new Vector3(length / 2f, 0, 0);
			snapPoint2.tag = "snappoint";

			// Add small sphere for debug visualization
			if (ShowDebugInfo.Value)
			{
				GameObject sphere2 = GameObject.CreatePrimitive(PrimitiveType.Sphere);
				sphere2.transform.SetParent(snapPoint2.transform);
				sphere2.transform.localScale = Vector3.one * 0.1f;
				Destroy(sphere2.GetComponent<Collider>());
			}
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

			// Apply tint to material
			if (renderer.material != null)
			{
				renderer.material.color = tint;
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