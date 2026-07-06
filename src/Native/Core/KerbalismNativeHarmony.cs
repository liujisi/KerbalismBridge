using System;
using HarmonyLib;
using KerbalismBridge;

namespace KerbalismNative
{
	public static class KerbalismNativeHarmony
	{
		private static bool patchesApplied;

		public static void ApplyPatches()
		{
			if (patchesApplied)
				return;
			patchesApplied = true;
			var harmony = new Harmony("KerbalismNative");

			TryPatch(harmony, typeof(Patch_FissionReactor_HandleResourceActivities), true);
			TryPatch(harmony, typeof(Patch_FissionReactor_DoCatchup), true);
			TryPatch(harmony, typeof(Patch_SystemHeatConverter_PostProcess), true);
			TryPatch(harmony, typeof(Patch_SystemHeatHarvester_PostProcess), true);
			TryPatch(harmony, typeof(Patch_SystemHeatConverter_FixedUpdateFlight), true);
			TryPatch(harmony, typeof(Patch_SystemHeatHarvester_FixedUpdateFlight), true);

			BridgeUtils.Log("Native core Layer B Harmony patches applied.");
		}

		private static void TryPatch(Harmony harmony, Type patchType, bool enabled)
		{
			if (!enabled)
			{
				BridgeUtils.Log("  Native Harmony: " + patchType.Name + " DISABLED");
				return;
			}
			try
			{
				harmony.CreateClassProcessor(patchType).Patch();
				BridgeUtils.Log("  Native Harmony: " + patchType.Name + " ok");
			}
			catch (Exception e)
			{
				BridgeUtils.Log("  Native Harmony: " + patchType.Name + " FAILED: " + e.Message);
			}
		}
	}
}
