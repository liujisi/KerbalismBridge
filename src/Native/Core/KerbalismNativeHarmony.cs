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

			// Register individual patch classes so one failure doesn't abort others.
			// 'false' arguments below disable selected patches for troubleshooting.
			bool enableConverterPostProcess  = false;
			bool enableHarvesterPostProcess  = true;
			bool enableFixedUpdateFlight     = true;
			bool enableFissionReactor        = true;
			bool enableFissionDoCatchup      = true;

			TryPatch(harmony, typeof(Patch_FissionReactor_HandleResourceActivities), enableFissionReactor);
			TryPatch(harmony, typeof(Patch_FissionReactor_DoCatchup), enableFissionDoCatchup);
			TryPatch(harmony, typeof(Patch_SystemHeatConverter_PostProcess), enableConverterPostProcess);
			TryPatch(harmony, typeof(Patch_SystemHeatHarvester_PostProcess), enableHarvesterPostProcess);
			TryPatch(harmony, typeof(Patch_SystemHeatConverter_FixedUpdateFlight), enableFixedUpdateFlight);
			TryPatch(harmony, typeof(Patch_SystemHeatHarvester_FixedUpdateFlight), enableFixedUpdateFlight);

			BridgeUtils.Log("Native core Layer B Harmony patches applied (converter=" + enableConverterPostProcess
				+ " harvester=" + enableHarvesterPostProcess
				+ " fixedUpdate=" + enableFixedUpdateFlight + ")");
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
