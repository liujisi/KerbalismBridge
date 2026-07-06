using HarmonyLib;
using SystemHeat;

namespace KerbalismNative
{
	/// <summary>
	/// Prevents JIT inlining of PartResource.amount property setter into Kerbalism's
	/// ResourceInfo.Sync loop. Without this, the JIT may cache stale PartResource.amount
	/// values in registers, causing Sync to apply phantom production delta that the
	/// broker has already blocked via ConvertInputAvailabilityScale.
	/// The empty prefix exists solely as an inlining barrier — it has no side effects.
	/// </summary>
	[HarmonyPatch(typeof(PartResource), "set_amount")]
	internal static class Patch_PartResource_set_amount_inlining_barrier
	{
		private static void Prefix(PartResource __instance, double value) { }
	}

	[HarmonyPatch(typeof(ModuleSystemHeatFissionReactor), "HandleResourceActivities")]
	internal static class Patch_FissionReactor_HandleResourceActivities
	{
		private static bool Prefix(ModuleSystemHeatFissionReactor __instance)
		{
			return __instance.part.FindModuleImplementing<SystemHeatFissionReactorKerbalismUpdater>() == null
				&& __instance.part.FindModuleImplementing<SystemHeatFissionEngineKerbalismUpdater>() == null;
		}
	}

	[HarmonyPatch(typeof(ModuleSystemHeatFissionReactor), "DoCatchup")]
	internal static class Patch_FissionReactor_DoCatchup
	{
		private static bool Prefix(ModuleSystemHeatFissionReactor __instance)
		{
			return __instance.part.FindModuleImplementing<SystemHeatFissionReactorKerbalismUpdater>() == null
				&& __instance.part.FindModuleImplementing<SystemHeatFissionEngineKerbalismUpdater>() == null;
		}
	}

	[HarmonyPatch(typeof(ModuleSystemHeatConverter), "PostProcess")]
	internal static class Patch_SystemHeatConverter_PostProcess
	{
		private static bool Prefix(ModuleSystemHeatConverter __instance, ConverterResults result, double deltaTime)
		{
			SystemHeatConverterKerbalismUpdater updater = __instance.part.FindModuleImplementing<SystemHeatConverterKerbalismUpdater>();
			if (updater == null || !updater.OwnsConverter(__instance))
				return true;

			Traverse.Create(__instance).Method("UpdateFlux", result.TimeFactor).GetValue();
			__instance.lastTimeFactor = result.TimeFactor;
			return false;
		}
	}

	[HarmonyPatch(typeof(ModuleSystemHeatHarvester), "PostProcess")]
	internal static class Patch_SystemHeatHarvester_PostProcess
	{
		private static bool Prefix(ModuleSystemHeatHarvester __instance, ConverterResults result, double deltaTime)
		{
			SystemHeatHarvesterKerbalismUpdater updater = __instance.part.FindModuleImplementing<SystemHeatHarvesterKerbalismUpdater>();
			if (updater == null || !updater.OwnsHarvester(__instance))
				return true;

			Traverse.Create(__instance).Method("UpdateFlux", result.TimeFactor).GetValue();
			__instance.lastTimeFactor = result.TimeFactor;
			return false;
		}
	}
}
