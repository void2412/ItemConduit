using HarmonyLib;
using ItemConduit.GUI;

[HarmonyPatch(typeof(InventoryGui), "IsVisible")]
public static class InventoryGui_IsVisible_Patch
{
	private static void Postfix(ref bool __result)
	{
		// If inventory is already visible, don't change
		if (__result) return;

		// If ItemConduit GUI is open, pretend inventory is visible
		if (GUIController.Instance != null && GUIController.Instance.HasActiveGUI())
		{
			__result = true;  // ← Makes EVERY vanilla check think inventory is open!
		}
	}
}