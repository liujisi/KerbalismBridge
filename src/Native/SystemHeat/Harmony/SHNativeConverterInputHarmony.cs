using System.Collections.Generic;
using HarmonyLib;
using KERBALISM;
using SystemHeat;
using KerbalismBridge;

namespace KerbalismNative
{
	/// <summary>
	/// While Kerbalism owns SH native converter/harvester resource IO, intercept FixedUpdateFlight
	/// for Kerbalism-owned converters to skip stock ModuleResourceConverter resource processing,
	/// preventing phantom production while still handling heat, overheat, and inactive-disable.
	/// </summary>
	[HarmonyPatch(typeof(ModuleSystemHeatConverter), "FixedUpdateFlight")]
	internal static class Patch_SystemHeatConverter_FixedUpdateFlight
	{
		private static bool Prefix(ModuleSystemHeatConverter __instance)
		{
			if (!SHNativeConverterInputHarmony.ShouldZeroInputs(__instance))
				return true; // not owned by Kerbalism, let original run

			// -- Replicate FixedUpdateFlight without the stock resource IO --
			// 1. heatModule check (disable if missing)
			var heatModule = Traverse.Create(__instance).Field("heatModule").GetValue();
			if (heatModule == null)
			{
				__instance.enabled = false;
				return false;
			}

			// 2. Overheat check (keeps safety shutdown)
			Traverse.Create(__instance).Method("CheckOverheat").GetValue();

			// 3. Disable if inactive
			if (!__instance.IsActivated && !__instance.AlwaysActive)
			{
				__instance.enabled = false;
				return false;
			}

			// 4. Push heat via UpdateFlux using lastTimeFactor (preserves heat management)
			Traverse.Create(__instance).Method("UpdateFlux", __instance.lastTimeFactor).GetValue();

			// Skip base.FixedUpdate() — no stock resource IO, no phantom production
			return false;
		}
	}

	[HarmonyPatch(typeof(ModuleSystemHeatHarvester), "FixedUpdateFlight")]
	internal static class Patch_SystemHeatHarvester_FixedUpdateFlight
	{
		private static bool Prefix(ModuleSystemHeatHarvester __instance)
		{
			if (!SHNativeConverterInputHarmony.ShouldZeroInputs(__instance))
				return true;

			var heatModule = Traverse.Create(__instance).Field("heatModule").GetValue();
			if (heatModule == null)
			{
				__instance.enabled = false;
				return false;
			}

			Traverse.Create(__instance).Method("CheckOverheat").GetValue();

			if (!__instance.IsActivated && !__instance.AlwaysActive)
			{
				__instance.enabled = false;
				return false;
			}

			Traverse.Create(__instance).Method("UpdateFlux", __instance.lastTimeFactor).GetValue();

			return false;
		}
	}

	/// <summary>
	/// Helper — detects if this converter/harvester is owned by Kerbalism.
	/// </summary>
	internal static class SHNativeConverterInputHarmony
	{
		internal static bool ShouldZeroInputs(PartModule module)
		{
			if (module == null || module.part == null || !Lib.IsFlight())
				return false;

			SystemHeatConverterKerbalismUpdater converterUpdater =
				module.part.FindModuleImplementing<SystemHeatConverterKerbalismUpdater>();
			if (converterUpdater != null
				&& module is ModuleSystemHeatConverter shConverter
				&& converterUpdater.OwnsConverter(shConverter))
			{
				return true;
			}

			SystemHeatHarvesterKerbalismUpdater harvesterUpdater =
				module.part.FindModuleImplementing<SystemHeatHarvesterKerbalismUpdater>();
			if (harvesterUpdater != null
				&& module is ModuleSystemHeatHarvester shHarvester
				&& harvesterUpdater.OwnsHarvester(shHarvester))
			{
				return true;
			}

			return false;
		}
	}
}
