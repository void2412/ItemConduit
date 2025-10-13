using ItemConduit.Config;
using ItemConduit.Interfaces;
using ItemConduit.Nodes;
using Jotunn.Configs;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace ItemConduit.Extensions
{
	public class BeehiveExtension : BaseExtension, IContainerInterface
	{
		private Beehive beehive;
		public Container m_container;

		protected override void Awake()
		{
			base.Awake();
			beehive = GetComponentInParent<Beehive>();
			if (beehive == null)
			{
				beehive = GetComponent<Beehive>();
				if (beehive == null)
				{
					beehive = GetComponentInChildren<Beehive>();
				}
				else
				{
					Logger.LogError($"[ItemConduit] SmelteryExtension could not find Smelter component!");
					return;
				}
			}

			ZNetView znetView = beehive.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid())
			{
				if (DebugConfig.showDebug.Value)
				{
					Logger.LogInfo($"[ItemConduit] Skipping container creation - invalid ZNetView");
				}
				return;
			}

			SetupContainer();
			LoadInventoryFromZDO();
		}

		private void SetupContainer()
		{
			m_container = beehive.gameObject.AddComponent<Container>();
			m_container.m_width = 1;
			m_container.m_height = 1;
			m_container.m_inventory = new Inventory("Smelter Output", null, 1, 1);
			m_container.name = "Smelter Output";
		}

		private void SaveInventoryToZDO()
		{
			if (beehive == null || m_container == null) return;

			ZNetView znetView = beehive.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			// Save inventory as a ZPackage
			ZPackage pkg = new ZPackage();
			m_container.m_inventory.Save(pkg);
			zdo.Set("ItemConduit_SmelterInventory", pkg.GetBase64());
		}

		private void LoadInventoryFromZDO()
		{
			if (beehive == null || m_container == null) return;

			ZNetView znetView = beehive.GetComponent<ZNetView>();
			if (znetView == null || !znetView.IsValid()) return;

			ZDO zdo = znetView.GetZDO();
			if (zdo == null) return;

			string data = zdo.GetString("ItemConduit_SmelterInventory", "");
			if (!string.IsNullOrEmpty(data))
			{
				ZPackage pkg = new ZPackage(data);
				m_container.m_inventory.Load(pkg);
			}
		}

	}
}
