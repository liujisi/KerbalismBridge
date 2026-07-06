using System.Collections.Generic;
using HarmonyLib;
using KERBALISM;
using SystemHeat;
using KerbalismBridge;

namespace KerbalismNative
{
	/// <summary>
	/// Tracks whether we are inside a Kerbalism-owned SystemHeat converter FixedUpdateFlight.
	/// Used by PartRequestResource interceptor to detect stock converter resource IO.
	/// </summary>
	internal static class KerbalismSHConverterContext
	{
		internal static bool Active;
		internal static string ConverterName;
		internal static string ModuleID;
		internal static string PartName;
		internal static int DiagCounter;

		internal static void Enter(ModuleSystemHeatConverter c)
		{
			Active = true;
			ConverterName = c.ConverterName;
			ModuleID = c.moduleID;
			PartName = c.part?.partInfo?.name ?? "?";
			if (DiagCounter < 10)
			{
				DiagCounter++;
				BridgeUtils.Log("[zKerbalismNative] CTX Enter conv=" + ConverterName + " moduleID=" + ModuleID + " part=" + PartName);
			}
		}

		internal static void Leave()
		{
			if (DiagCounter < 10)
			{
				BridgeUtils.Log("[zKerbalismNative] CTX Leave conv=" + ConverterName + " moduleID=" + ModuleID);
			}
			Active = false;
			ConverterName = null;
			ModuleID = null;
			PartName = null;
		}
	}

	[HarmonyPatch(typeof(Part), nameof(Part.RequestResource), typeof(string), typeof(double))]
	internal static class Patch_Part_RequestResource_String_Double
	{
		[HarmonyPrefix]
		private static bool Prefix(Part __instance, string resourceName, double amount, ref double __result)
		{
			if (!KerbalismSHConverterContext.Active) return true;
			KerbalismSHConverterContext.DiagCounter++;
			if (KerbalismSHConverterContext.DiagCounter <= 30)
				BridgeUtils.Log("[zKerbalismNative] RR-SD " + resourceName + " amt=" + amount + " conv=" + KerbalismSHConverterContext.ConverterName + " part=" + KerbalismSHConverterContext.PartName);
			return true;
		}
	}

	[HarmonyPatch(typeof(Part), nameof(Part.RequestResource), typeof(int), typeof(double))]
	internal static class Patch_Part_RequestResource_Int_Double
	{
		[HarmonyPrefix]
		private static bool Prefix(Part __instance, int resourceID, double amount, ref double __result)
		{
			if (!KerbalismSHConverterContext.Active) return true;
			KerbalismSHConverterContext.DiagCounter++;
			if (KerbalismSHConverterContext.DiagCounter <= 30)
			{
				var def = PartResourceLibrary.Instance.GetDefinition(resourceID);
				BridgeUtils.Log("[zKerbalismNative] RR-ID " + (def?.name ?? resourceID.ToString()) + " amt=" + amount + " conv=" + KerbalismSHConverterContext.ConverterName + " part=" + KerbalismSHConverterContext.PartName);
			}
			return true;
		}
	}

	[HarmonyPatch(typeof(Part), nameof(Part.RequestResource), typeof(string), typeof(double), typeof(ResourceFlowMode), typeof(bool))]
	internal static class Patch_Part_RequestResource_String_Double_Flow_Bool
	{
		[HarmonyPrefix]
		private static bool Prefix(Part __instance, string resourceName, double amount, ResourceFlowMode flowMode, bool ignoreFlow, ref double __result)
		{
			if (!KerbalismSHConverterContext.Active) return true;
			KerbalismSHConverterContext.DiagCounter++;
			if (KerbalismSHConverterContext.DiagCounter <= 30)
				BridgeUtils.Log("[zKerbalismNative] RR-S4 " + resourceName + " amt=" + amount + " flow=" + flowMode + " ignoreFlow=" + ignoreFlow + " conv=" + KerbalismSHConverterContext.ConverterName + " part=" + KerbalismSHConverterContext.PartName);
			return true;
		}
	}

	[HarmonyPatch(typeof(Part), nameof(Part.RequestResource), typeof(int), typeof(double), typeof(ResourceFlowMode), typeof(bool))]
	internal static class Patch_Part_RequestResource_Int_Double_Flow_Bool
	{
		[HarmonyPrefix]
		private static bool Prefix(Part __instance, int resourceID, double amount, ResourceFlowMode flowMode, bool ignoreFlow, ref double __result)
		{
			if (!KerbalismSHConverterContext.Active) return true;
			KerbalismSHConverterContext.DiagCounter++;
			if (KerbalismSHConverterContext.DiagCounter <= 30)
			{
				var def = PartResourceLibrary.Instance.GetDefinition(resourceID);
				BridgeUtils.Log("[zKerbalismNative] RR-I4 " + (def?.name ?? resourceID.ToString()) + " amt=" + amount + " flow=" + flowMode + " ignoreFlow=" + ignoreFlow + " conv=" + KerbalismSHConverterContext.ConverterName + " part=" + KerbalismSHConverterContext.PartName);
			}
			return true;
		}
	}

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
		private static void Prefix(ModuleSystemHeatConverter __instance, ref SHNativeConverterInputHarmony.InputListRateBackup __state)
		{
			if (!SHNativeConverterInputHarmony.ShouldZeroInputs(__instance))
				return;

			KerbalismSHConverterContext.Enter(__instance);
			__state = SHNativeConverterInputHarmony.ZeroInputList(__instance.inputList);
		}

		private static void Postfix(ModuleSystemHeatConverter __instance, ref SHNativeConverterInputHarmony.InputListRateBackup __state)
		{
			if (!__state.HasBackup)
				return;

			SHNativeConverterInputHarmony.RestoreInputList(__instance.inputList, ref __state);
			KerbalismSHConverterContext.Leave();
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
