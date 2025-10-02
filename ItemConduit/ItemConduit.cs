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
using ItemConduit.Utils;
using Logger = Jotunn.Logger;
using ItemConduit.Config;
namespace ItemConduit.Core
{
	/// <summary>
	/// Main plugin class for ItemConduit mod
	/// Handles initialization, configuration, and piece registration
	/// </summary>
	[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
	[BepInDependency(Jotunn.Main.ModGuid)]
	[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
	public class ItemConduit : BaseUnityPlugin
	{
		// Plugin metadata
		public const string PluginGUID = "void.itemconduit";
		public const string PluginName = "ItemConduit";
		public const string PluginVersion = "1.0.0";

		// Singleton instance for global access
		private static ItemConduit _instance;
		public static ItemConduit Instance => _instance;

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
			PrefabManager.OnVanillaPrefabsAvailable += NodeRegistration.RegisterPieces;

			// Initialize the network management system
			global::ItemConduit.Network.NetworkManager.Instance.Initialize();

			Jotunn.Logger.LogInfo($"{PluginName} v{PluginVersion} initialized successfully!");
		}

		/// <summary>
		/// Load and bind configuration values from BepInEx config file
		/// </summary>
		private void LoadConfiguration()
		{
			Logger.LogInfo($"Loading {PluginName} config");
			ConfigManager.Initialize(Config);
			Logger.LogInfo("Config Load Successfully");

		}

		/// <summary>
		/// Cleanup on mod shutdown
		/// </summary>
		private void OnDestroy()
		{
			// Unpatch Harmony patches
			harmony?.UnpatchSelf();

			// Shutdown network manager
			global::ItemConduit.Network.NetworkManager.Instance?.Shutdown();

			Jotunn.Logger.LogInfo($"{PluginName} shutdown complete");
		}
	}

}