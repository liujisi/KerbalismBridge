using System.Collections.Generic;
using HarmonyLib;
using KERBALISM;
using SystemHeat;

namespace KerbalismNative
{
	/// <summary>
	/// While Kerbalism owns SH native converter/harvester resource IO, hide input rates from stock
	/// ModuleResourceConverter input checks (ratio × fixedDeltaTime) so high timewarp does not stop modules.
	/// </summary>
	internal static class SHNativeConverterInputHarmony
	{
		internal struct InputListRateBackup
		{
			internal double[] Ratios;
			internal bool Active;

			internal bool HasBackup => Active && Ratios != null;
		}

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

		internal static InputListRateBackup ZeroInputList(List<ResourceRatio> inputList)
		{
			if (inputList == null || inputList.Count == 0)
				return default;

			var backup = new InputListRateBackup
			{
				Ratios = new double[inputList.Count],
				Active = true
			};

			for (int i = 0; i < inputList.Count; i++)
			{
				ResourceRatio entry = inputList[i];
				backup.Ratios[i] = entry.Ratio;
				entry.Ratio = 0.0;
				inputList[i] = entry;
			}

			return backup;
		}

		internal static void RestoreInputList(List<ResourceRatio> inputList, ref InputListRateBackup backup)
		{
			if (!backup.HasBackup || inputList == null)
				return;

			int count = System.Math.Min(inputList.Count, backup.Ratios.Length);
			for (int i = 0; i < count; i++)
			{
				ResourceRatio entry = inputList[i];
				entry.Ratio = backup.Ratios[i];
				inputList[i] = entry;
			}

			backup = default;
		}
	}

	[HarmonyPatch(typeof(ModuleSystemHeatConverter), "FixedUpdateFlight")]
	internal static class Patch_SystemHeatConverter_FixedUpdateFlight
	{
		private static int _diagCounter;
		private static void Prefix(ModuleSystemHeatConverter __instance, ref SHNativeConverterInputHarmony.InputListRateBackup __state)
		{
			if (!SHNativeConverterInputHarmony.ShouldZeroInputs(__instance))
				return;

			__state = SHNativeConverterInputHarmony.ZeroInputList(__instance.inputList);
		}

		private static void Postfix(ModuleSystemHeatConverter __instance, ref SHNativeConverterInputHarmony.InputListRateBackup __state)
		{
			if (!__state.HasBackup)
				return;

			SHNativeConverterInputHarmony.RestoreInputList(__instance.inputList, ref __state);

			if (_diagCounter < 5)
			{
				double metals = 0;
				if (__instance.part != null)
				{
					var pr = __instance.part.Resources["Metals"];
					if (pr != null) metals = pr.amount;
				}
				BridgeUtils.Log("[zKerbalismNative] FUF Postfix: " + __instance.ConverterName
					+ " lastTimeFactor=" + __instance.lastTimeFactor
					+ " partMetals=" + metals);
				_diagCounter++;
			}
		}
	}

	[HarmonyPatch(typeof(ModuleSystemHeatHarvester), "FixedUpdateFlight")]
	internal static class Patch_SystemHeatHarvester_FixedUpdateFlight
	{
		private static void Prefix(ModuleSystemHeatHarvester __instance, ref SHNativeConverterInputHarmony.InputListRateBackup __state)
		{
			if (!SHNativeConverterInputHarmony.ShouldZeroInputs(__instance))
				return;

			__state = SHNativeConverterInputHarmony.ZeroInputList(__instance.inputList);
		}

		private static void Postfix(ModuleSystemHeatHarvester __instance, ref SHNativeConverterInputHarmony.InputListRateBackup __state)
		{
			if (!__state.HasBackup)
				return;

			SHNativeConverterInputHarmony.RestoreInputList(__instance.inputList, ref __state);
		}
	}
}
