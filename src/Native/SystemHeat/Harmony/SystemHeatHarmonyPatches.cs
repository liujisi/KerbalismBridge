using HarmonyLib;
using SystemHeat;
using KerbalismBridge;

namespace KerbalismNative
{
	/// <summary>Traces all Part.RequestResource calls for Metals to find out-of-band production.</summary>
	internal static class PartRequestResourceTracer
	{
		private static int logLimit = 30;
		private static void LogMetals(string methodSig, Part part, string resourceName, int resourceID, double demand, double result)
		{
			if (logLimit <= 0) return;
			bool isMetals = (resourceID == 0) ? (resourceName == "Metals") : (PartResourceLibrary.Instance.GetDefinition(resourceID)?.name == "Metals");
			if (!isMetals || !(result > 0.0)) return;

			logLimit--;
			BridgeUtils.Log("[zKerbalismNative] RequestResource " + methodSig
				+ " part=" + (part != null ? part.partInfo.name : "null")
				+ " demand=" + demand + " result=" + result
				+ "\n" + System.Environment.StackTrace);
		}

		[HarmonyPatch(typeof(Part), nameof(Part.RequestResource), typeof(string), typeof(double))]
		[HarmonyPostfix]
		private static void Postfix_String_Double(Part __instance, string resourceName, double demand, double __result) =>
			LogMetals("(string,double)", __instance, resourceName, 0, demand, __result);

		[HarmonyPatch(typeof(Part), nameof(Part.RequestResource), typeof(int), typeof(double))]
		[HarmonyPostfix]
		private static void Postfix_Int_Double(Part __instance, int resourceID, double demand, double __result) =>
			LogMetals("(int,double)", __instance, null, resourceID, demand, __result);

		[HarmonyPatch(typeof(Part), nameof(Part.RequestResource), typeof(int), typeof(double), typeof(ResourceFlowMode), typeof(bool))]
		[HarmonyPostfix]
		private static void Postfix_Int_Double_Flow_Bool(Part __instance, int resourceID, double demand, ResourceFlowMode flowMode, bool useStackPriority, double __result) =>
			LogMetals("(int,double,flow,bool)", __instance, null, resourceID, demand, __result);

		[HarmonyPatch(typeof(Part), nameof(Part.RequestResource), typeof(string), typeof(double), typeof(ResourceFlowMode), typeof(bool))]
		[HarmonyPostfix]
		private static void Postfix_String_Double_Flow_Bool(Part __instance, string resourceName, double demand, ResourceFlowMode flowMode, bool useStackPriority, double __result) =>
			LogMetals("(string,double,flow,bool)", __instance, resourceName, 0, demand, __result);
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
		private static int logCounter;
		private static bool Prefix(ModuleSystemHeatConverter __instance, ConverterResults result, double deltaTime)
		{
			SystemHeatConverterKerbalismUpdater updater = __instance.part.FindModuleImplementing<SystemHeatConverterKerbalismUpdater>();
			if (updater == null || !updater.OwnsConverter(__instance))
				return true;

			if (logCounter < 5)
			{
				BridgeUtils.Log("[zKerbalismNative] PostProcess INTERCEPTED: " + __instance.ConverterName
					+ " moduleID=" + __instance.moduleID + " TimeFactor=" + result.TimeFactor);
				logCounter++;
			}
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
