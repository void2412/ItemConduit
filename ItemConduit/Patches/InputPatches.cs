using HarmonyLib;
using ItemConduit.GUI;

namespace ItemConduit.Patches
{
	/// <summary>
	/// Simple input blocking patches for ItemConduit GUIs
	/// Leverages Valheim's existing TextInput.IsVisible() check
	/// </summary>
	public static class InputPatches
	{
		/// <summary>
		/// Make Valheim think inventory is open when our GUI is active
		/// This blocks most player input automatically
		/// </summary>
		[HarmonyPatch(typeof(InventoryGui), "IsVisible")]
		public static class InventoryGui_IsVisible_Patch
		{
			private static void Postfix(ref bool __result)
			{
				if (__result) return;

				if (GUIController.Instance != null && GUIController.Instance.HasActiveGUI())
				{
					__result = true;
				}
			}
		}

		/// <summary>
		/// CRITICAL: Make Valheim think TextInput is visible when typing in our input fields
		/// This is the key to blocking ALL game input during text entry
		/// </summary>
		[HarmonyPatch(typeof(TextInput), "IsVisible")]
		public static class TextInput_IsVisible_Patch
		{
			private static void Postfix(ref bool __result)
			{
				if (__result) return;

				// If any of our InputFields has focus, report TextInput as visible
				if (GUIController.Instance != null && GUIController.Instance.HasInputFieldFocus())
				{
					__result = true;
				}
			}
		}
	}
}