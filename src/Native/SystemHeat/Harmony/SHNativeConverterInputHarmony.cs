using System;
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
			if (!SHNativeConverterInputHarmony.ShouldBlockStockIO(__instance))
				return true;

			SHConverterReflection.SafeCall(__instance, "UpdateConverterStatus");

			var heatModule = Traverse.Create(__instance).Field("heatModule").GetValue();
			if (heatModule == null)
			{
				__instance.enabled = false;
				return false;
			}

			SHConverterReflection.SafeCall(__instance, "CheckOverheat");

			if (__instance.AlwaysActive && !__instance.IsActivated)
			{
				__instance.IsActivated = true;
				SHConverterReflection.SafeCall(__instance, "UpdateConverterStatus");
			}

			if (!__instance.IsActivated && !__instance.AlwaysActive)
			{
				__instance.enabled = false;
				return false;
			}

			SHConverterReflection.SafeCall(__instance, "UpdateFlux", __instance.lastTimeFactor);

			return false;
		}
	}

	[HarmonyPatch(typeof(ModuleSystemHeatHarvester), "FixedUpdateFlight")]
	internal static class Patch_SystemHeatHarvester_FixedUpdateFlight
	{
		private static bool Prefix(ModuleSystemHeatHarvester __instance)
		{
			if (!SHNativeConverterInputHarmony.ShouldBlockStockIO(__instance))
				return true;

			SHConverterReflection.SafeCall(__instance, "UpdateConverterStatus");

			var heatModule = Traverse.Create(__instance).Field("heatModule").GetValue();
			if (heatModule == null)
			{
				__instance.enabled = false;
				return false;
			}

			SHConverterReflection.SafeCall(__instance, "CheckOverheat");

			if (__instance.AlwaysActive && !__instance.IsActivated)
			{
				__instance.IsActivated = true;
				SHConverterReflection.SafeCall(__instance, "UpdateConverterStatus");
			}

			if (!__instance.IsActivated && !__instance.AlwaysActive)
			{
				__instance.enabled = false;
				return false;
			}

			SHConverterReflection.SafeCall(__instance, "UpdateFlux", __instance.lastTimeFactor);

			return false;
		}
	}

	/// <summary>Safe reflection calls with error logging for SystemHeat internal methods.</summary>
	internal static class SHConverterReflection
	{
		private static void LogError(PartModule module, string methodName, Exception ex)
		{
			string partName = "?";
			try { partName = module?.part?.partInfo?.name ?? "?"; } catch { }
			string convId = "?";
			try
			{
				if (module is ModuleSystemHeatConverter c) convId = c.moduleID;
				else if (module is ModuleSystemHeatHarvester h) convId = h.moduleID;
			}
			catch { }
			UnityEngine.Debug.LogError("[KerbalismBridge] SystemHeat " + methodName + " failed: " +
				"type=" + (module?.GetType().Name ?? "?") +
				" part=" + partName +
				" moduleID=" + convId +
				" ex=" + ex.Message);
		}

		public static void SafeCall(PartModule module, string methodName, params object[] args)
		{
			try
			{
				Traverse.Create(module).Method(methodName, args).GetValue();
			}
			catch (Exception ex)
			{
				LogError(module, methodName, ex);
			}
		}
	}

	/// <summary>
	/// Detects if this converter/harvester is owned by Kerbalism and should have
	/// stock resource IO blocked.
	/// </summary>
	internal static class SHNativeConverterInputHarmony
	{
		internal static bool ShouldBlockStockIO(PartModule module)
		{
			if (module == null || module.part == null || !Lib.IsFlight())
				return false;

			if (module is ModuleSystemHeatConverter shConverter)
			{
				var updaters = module.part.FindModulesImplementing<SystemHeatConverterKerbalismUpdater>();
				for (int i = 0; i < updaters.Count; i++)
				{
					if (updaters[i].OwnsConverter(shConverter))
						return true;
				}
			}

			if (module is ModuleSystemHeatHarvester shHarvester)
			{
				var updaters = module.part.FindModulesImplementing<SystemHeatHarvesterKerbalismUpdater>();
				for (int i = 0; i < updaters.Count; i++)
				{
					if (updaters[i].OwnsHarvester(shHarvester))
						return true;
				}
			}

			return false;
		}
	}
}
