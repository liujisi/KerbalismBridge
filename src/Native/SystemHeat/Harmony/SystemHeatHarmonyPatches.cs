using HarmonyLib;
using SystemHeat;
using KerbalismBridge;

namespace KerbalismNative
{
	/// <summary>Traces all Part.RequestResource calls for Metals to find out-of-band production.</summary>
	internal static class PartRequestResourceTracer
	{
		private static int metalsLogLimit = 60;
		private static void LogMetals(string methodSig, Part part, string resourceName, int resourceID, double demand, double result)
		{
			if (metalsLogLimit <= 0) return;
			bool isMetals = (resourceID == 0) ? (resourceName == "Metals") : (PartResourceLibrary.Instance.GetDefinition(resourceID)?.name == "Metals");
			if (!isMetals || !(result > 0.0)) return;

			var st = System.Environment.StackTrace;
			if (st.Contains("Sync") || st.Contains("Deferred"))
			{
				if (metalsLogLimit > 40) { metalsLogLimit--; }
				return;
			}
			metalsLogLimit--;
			BridgeUtils.Log("[zKerbalismNative] RequestResource Metals sig=" + methodSig
				+ " part=" + (part != null ? part.partInfo.name : "null")
				+ " demand=" + demand + " result=" + result
				+ "\n" + st);
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

	/// <summary>Catches ANY direct write to PartResource.amount, including from Kerbalism Sync wrappers.</summary>
	[HarmonyPatch(typeof(PartResource), "set_amount")]
	internal static class Patch_PartResource_set_amount
	{
		private static int metalsLogLimit = 60;
		private static void Prefix(PartResource __instance, double value)
		{
			if (__instance.resourceName != "Metals" || metalsLogLimit <= 0) return;
			if (!(value - __instance.amount > 1e-6)) return;
			metalsLogLimit--;
			BridgeUtils.Log("[zKerbalismNative] PartResource.set_amount Metals old=" + __instance.amount
				+ " new=" + value + " delta=" + (value - __instance.amount)
				+ " part=" + (__instance.part != null ? __instance.part.partInfo.name : "?")
				+ "\n" + System.Environment.StackTrace);
		}
	}

	[HarmonyPatch(typeof(KERBALISM.ResourceInfo), "Produce")]
	internal static class Patch_ResourceInfo_Produce
	{
		private static int metalsLogLimit = 20;
		private static void Prefix(KERBALISM.ResourceInfo __instance, double quantity, KERBALISM.ResourceBroker broker)
		{
			if (__instance.ResourceName != "Metals" || metalsLogLimit <= 0) return;
			if (quantity <= 0.0) return;
			metalsLogLimit--;
			BridgeUtils.Log("[zKerbalismNative] ResourceInfo.Produce Metals qty=" + quantity
				+ " broker=" + (broker != null ? broker.Title : "null")
				+ "\n" + System.Environment.StackTrace);
		}
	}

	[HarmonyPatch(typeof(KERBALISM.VesselResources), "Produce")]
	internal static class Patch_VesselResources_Produce
	{
		private static int metalsLogLimit = 20;
		private static void Prefix(KERBALISM.VesselResources __instance, Vessel v, string resource_name, double quantity, KERBALISM.ResourceBroker broker)
		{
			if (resource_name != "Metals" || metalsLogLimit <= 0) return;
			if (quantity <= 0.0) return;
			metalsLogLimit--;
			BridgeUtils.Log("[zKerbalismNative] VesselResources.Produce Metals qty=" + quantity
				+ " broker=" + (broker != null ? broker.Title : "null")
				+ "\n" + System.Environment.StackTrace);
		}
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
