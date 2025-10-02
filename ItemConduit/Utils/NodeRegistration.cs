using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ItemConduit.Config;
using ItemConduit.Core;
using ItemConduit.GUI;
using ItemConduit.Network;
using ItemConduit.Nodes;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Utils
{
	public enum NodeType
	{
		/// <summary>Connection node that links extract and insert nodes</summary>
		Conduit,

		/// <summary>Source node that extracts items from containers</summary>
		Extract,

		/// <summary>Destination node that inserts items into containers</summary>
		Insert
	}
	public static class NodeRegistration
	{
		public static string node_horizontal_1m { get; set; } = "wood_beam_1";
		public static string node_horizontal_2m { get; set; } = "wood_beam";
		public static string node_horizontal_4m {  get; set; }
		public static string node_vertical_1m { get; set; } = "wood_pole";
		public static string node_vertical_2m { get; set; } = "wood_pole2";
		public static string node_vertical_4m { get; set; }

		private static void RegisterNode(string prefabClone, string prefabName, string displayName, string description, float length, NodeType nodeType, int woodCost, int bronzeCost, string category)
		{


			// Clone wood_beam as base prefab for our nodes
			GameObject beamPrefab = PrefabManager.Instance.GetPrefab(prefabClone);
			if (beamPrefab == null)
			{
				Logger.LogError($"Could not find {prefabClone} prefab for cloning");
				return;
			}

			// Create cloned prefab with our name
			GameObject nodePrefab = PrefabManager.Instance.CreateClonedPrefab(prefabName, beamPrefab);
			if (nodePrefab == null)
			{
				Logger.LogError($"Failed to create {prefabName} prefab");
				return;
			}

			// Remove any existing node components (in case of re-registration)
			foreach (var existingNode in nodePrefab.GetComponents<BaseNode>())
			{
				UnityEngine.Object.DestroyImmediate(existingNode);
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

				Logger.LogInfo($"[ItemConduit] Set node {prefabName} length to {length}m (verification: {nodeComponent.NodeLength}m)");

				// Log existing snappoints from the cloned prefab
				int snapCount = 0;
				foreach (Transform child in nodePrefab.transform)
				{
					if (child.tag == "snappoint")
					{
						snapCount++;
						Logger.LogInfo($"[ItemConduit] Found existing snappoint: {child.name} at local pos {child.localPosition}");
					}
				}
				Logger.LogInfo($"[ItemConduit] {prefabName} has {snapCount} existing snappoints from {prefabClone}");

			}

			// Configure piece component for building system
			Piece piece = nodePrefab.GetComponent<Piece>();
			if (piece != null)
			{
				piece.m_name = displayName;
				piece.m_description = description;
				piece.m_category = Piece.PieceCategory.Misc;

				// DON'T add custom snappoints - use the existing ones from wood_beam

				// Add visual customization based on node type
				CustomizeNodeVisuals(nodePrefab, nodeType);
			}

			// Create piece configuration for registration
			PieceConfig pieceConfig = new PieceConfig
			{
				Name = displayName,
				Description = description,
				PieceTable = "Hammer",
				Category = category,
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
						Logger.LogInfo($"[ItemConduit] Post-registration: Set {prefabName} length to {length}m, type to {nodeType}");
					}
				}
			};

			if (DebugConfig.showDebug.Value)
			{
				Jotunn.Logger.LogInfo($"Registered {displayName} [{prefabName}] with length {length}m");
			}
		}

		private static void CustomizeNodeVisuals(GameObject prefab, NodeType nodeType)
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
		public static void RegisterPieces()
		{
			try
			{
				if (PrefabManager.Instance == null)
				{
					Jotunn.Logger.LogError("PrefabManager.Instance is null!");
					return;
				}

				if (PieceManager.Instance == null)
				{
					Jotunn.Logger.LogError("PieceManager.Instance is null!");
					return;
				}

				// Register each type of node
				RegisterConduitNodes();
				RegisterExtractNodes();
				RegisterInsertNodes();

				// Unsubscribe after successful registration
				PrefabManager.OnVanillaPrefabsAvailable -= RegisterPieces;

				Jotunn.Logger.LogInfo($"Successfully registered all {ItemConduit.Core.ItemConduit.PluginName} pieces");
			}
			catch (System.Exception ex)
			{
				Jotunn.Logger.LogError($"Failed to register pieces: {ex.Message}");
				Jotunn.Logger.LogError($"Stack trace: {ex.StackTrace}");
			}
		}
		private static void RegisterConduitNodes()
		{

			// 1 meter conduit
			RegisterNode(
				node_horizontal_1m,
				"node_conduit_1m",
				"Conduit Node (1m)",
				"Connects extract and insert nodes\nLength: 1 meter",
				1f,
				NodeType.Conduit,
				2, 1, // Wood and Bronze requirements
				"Conduit"
			);

			// 2 meter conduit
			RegisterNode(
				node_horizontal_2m,
				"node_conduit_2m",
				"Conduit Node (2m)",
				"Connects extract and insert nodes\nLength: 2 meters",
				2f,
				NodeType.Conduit,
				3, 1,
				"Conduit"
			);

			RegisterNode(
				node_vertical_1m,
				"node_conduit_vertical_1m",
				"Vertical Conduit Node (1m)",
				"Connects extract and insert nodes\nLength: 1 meter",
				1f,
				NodeType.Conduit,
				2, 1,
				"Conduit"
			);

			RegisterNode(
				node_vertical_2m,
				"node_conduit_vertical_2m",
				"Vertical Conduit Node (2m)",
				"Connects extract and insert nodes\nLength: 2 meter",
				2f,
				NodeType.Conduit,
				2, 1,
				"Conduit"
			);
		}
		private static void RegisterExtractNodes()
		{
			// 1 meter extract node
			RegisterNode(
				node_horizontal_1m,
				"node_extract_1m",
				"Extract Node (1m)",
				"Pulls items from containers\n[E] to configure\nLength: 1 meter",
				1f,
				NodeType.Extract,
				3, 2,
				"Extract"
			);

			// 2 meter extract node
			RegisterNode(
				node_horizontal_2m,
				"node_extract_2m",
				"Extract Node (2m)",
				"Pulls items from containers\n[E] to configure\nLength: 2 meters",
				2f,
				NodeType.Extract,
				4, 2,
				"Extract"
			);

			RegisterNode(
				node_vertical_1m,
				"node_extract_vertical_1m",
				"Vertical Extract Node (1m)",
				"Pulls items from containers\n[E] to configure\nLength: 1 meter",
				1f,
				NodeType.Extract,
				2, 1,
				"Extract"
			);

			RegisterNode(
				node_vertical_2m,
				"node_extract_vertical_2m",
				"Vertical Extract Node (2m)",
				"Pulls items from containers\n[E] to configure\nLength: 2 meter",
				2f,
				NodeType.Extract,
				2, 1,
				"Extract"
			);
		}
		private static void RegisterInsertNodes()
		{
			// 1 meter insert node
			RegisterNode(
				node_horizontal_1m,
				"node_insert_1m",
				"Insert Node (1m)",
				"Pushes items into containers\n[E] to configure\nLength: 1 meter",
				1f,
				NodeType.Insert,
				3, 2,
				"Insert"
			);

			// 2 meter insert node
			RegisterNode(
				node_horizontal_2m,
				"node_insert_2m",
				"Insert Node (2m)",
				"Pushes items into containers\n[E] to configure\nLength: 2 meters",
				2f,
				NodeType.Insert,
				4, 2,
				"Insert"
			);

			RegisterNode(
				node_vertical_1m,
				"node_insert_vertical_1m",
				"Vertical Insert Node (1m)",
				"Pushes items into containers\n[E] to configure\nLength: 1 meter",
				1f,
				NodeType.Insert,
				2, 1,
				"Insert"
			);

			RegisterNode(
				node_vertical_2m,
				"node_insert_vertical_2m",
				"Vertical Insert Node (2m)",
				"Pushes items into containers\n[E] to configure\nLength: 2 meter",
				2f,
				NodeType.Insert,
				2, 1,
				"Insert"
			);
		}
	}
}
