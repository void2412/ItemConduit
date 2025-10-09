using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ItemConduit.Interfaces;
using UnityEngine;

namespace ItemConduit.Extensions
{
	public class BaseExtension : MonoBehaviour
	{
		protected ZNetView zNetView;

		protected virtual void Awake()
		{
			zNetView = GetComponentInParent<ZNetView>();
		}
	}
}
