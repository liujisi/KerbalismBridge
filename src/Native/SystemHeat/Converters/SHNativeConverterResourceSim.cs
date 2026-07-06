using System.Collections.Generic;
using KERBALISM;
using SystemHeat;
using KerbalismBridge;

namespace KerbalismNative
{
	/// <summary>
	/// Kerbalism resource rates for native SystemHeat converter/harvester modules (resource IO blocked via Harmony).
	/// </summary>
	internal static class SHNativeConverterResourceSim
	{
		private const string TAG = "[zKerbalismNative] ";

		internal static string AddLoadedConverterRates(
			ModuleSystemHeatConverter converter,
			string brokerTitle,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (converter == null || !converter.IsActivated || !converter.ModuleIsActive())
				return brokerTitle;

			bool diag = converter.part != null && converter.part.partInfo.name == "ELTinySmelter";

			double scale = converter.GetHeatThrottle();
			if (diag) Log("AddLoadedConverterRates " + converter.ConverterName + " moduleID=" + converter.moduleID + " heatThrottle=" + scale);

			if (scale <= double.Epsilon)
			{
				if (diag) Log("  scale=0, skip");
				return brokerTitle;
			}

			if (diag)
			{
				Log("  inputList ratios:");
				foreach (var inp in converter.inputList)
					Log("    " + inp.ResourceName + " ratio=" + inp.Ratio);
			}

			double inputScale = GetInputAvailabilityScale(converter.vessel, converter.inputList, availableResources, scale);
			double finalScale = scale * inputScale;
			if (diag) Log("  inputScale=" + inputScale + " finalScale=" + finalScale);

			scale = finalScale;
			if (scale <= double.Epsilon)
			{
				if (diag) Log("  BLOCKED: no inputs");
				return brokerTitle;
			}

			int count = 0;
			double efficiency = GetConverterEfficiency(converter);
			foreach (ResourceRatio input in converter.inputList)
			{
				double qty = -input.Ratio * scale;
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, qty));
				count++;
			}
			foreach (ResourceRatio output in converter.outputList)
			{
				double qty = efficiency * output.Ratio * scale;
				resourceChangeRequest.Add(new KeyValuePair<string, double>(output.ResourceName, qty));
				if (diag) Log("  OUTPUT " + output.ResourceName + " qty=" + qty);
				count++;
			}
			if (diag) Log("  ADDED " + count + " resource requests to list");

			return brokerTitle;
		}

		private static void Log(string msg)
		{
			BridgeUtils.Log(TAG + "BROKER " + msg);
		}

		private static double GetInputAvailabilityScale(Vessel vessel, List<ResourceRatio> inputList, Dictionary<string, double> availableResources, double scale)
		{
			if (availableResources == null || inputList == null || inputList.Count == 0)
				return 1d;

			VesselResources vesselResources = vessel != null ? KERBALISM.ResourceCache.Get(vessel) : null;
			double inputScale = 1d;
			foreach (ResourceRatio input in inputList)
			{
				if (input.Ratio <= double.Epsilon)
					continue;

				double available;
				if (vesselResources != null)
				{
					ResourceInfo resource = vesselResources.GetResource(vessel, input.ResourceName);
					available = resource.Amount + resource.Deferred;
				}
				else if (!availableResources.TryGetValue(input.ResourceName, out available))
					return 0d;

				double limit = available / (input.Ratio * scale);
				inputScale = System.Math.Min(inputScale, limit);
				if (inputScale <= double.Epsilon)
					return 0d;
			}

			return System.Math.Min(1d, inputScale);
		}

		internal static string AddLoadedHarvesterRates(
			ModuleSystemHeatHarvester harvester,
			string brokerTitle,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (harvester == null || !harvester.IsActivated || !harvester.ModuleIsActive())
				return brokerTitle;

			double scale = harvester.GetHeatThrottle();
			scale *= GetInputAvailabilityScale(harvester.vessel, harvester.inputList, availableResources, scale);
			if (scale <= double.Epsilon)
				return brokerTitle;

			foreach (ResourceRatio input in harvester.inputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio * scale));

			double abundance = BridgeUtils.SampleResourceAbundance(harvester.vessel, harvester);
			if (abundance > harvester.HarvestThreshold)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(harvester.ResourceName, abundance * harvester.Efficiency * scale));

			return brokerTitle;
		}

		internal static void BackgroundUpdateConverter(
			Vessel v,
			ProtoPartModuleSnapshot converterSnapshot,
			ModuleSystemHeatConverter converterPrefab,
			string brokerName,
			string brokerTitle,
			double elapsed_s)
		{
			if (converterSnapshot == null || converterPrefab == null || !Lib.Proto.GetBool(converterSnapshot, "IsActivated"))
				return;

			VesselResources resources = KERBALISM.ResourceCache.Get(v);
			ResourceRecipe recipe = new ResourceRecipe(KERBALISM.ResourceBroker.GetOrCreate(
				brokerName,
				KERBALISM.ResourceBroker.BrokerCategory.Converter,
				brokerTitle));

			foreach (ResourceRatio input in converterPrefab.inputList)
				recipe.AddInput(input.ResourceName, input.Ratio * elapsed_s);

			foreach (ResourceRatio output in converterPrefab.outputList)
				recipe.AddOutput(output.ResourceName, GetConverterEfficiency(converterPrefab) * output.Ratio * elapsed_s, output.DumpExcess);

			resources.AddRecipe(recipe);
			Lib.Proto.Set(converterSnapshot, "lastUpdateTime", Planetarium.GetUniversalTime());
		}

		internal static void BackgroundUpdateHarvester(
			Vessel v,
			ProtoPartModuleSnapshot harvesterSnapshot,
			ModuleSystemHeatHarvester harvesterPrefab,
			string brokerName,
			string brokerTitle,
			double elapsed_s)
		{
			if (harvesterSnapshot == null || harvesterPrefab == null || !Lib.Proto.GetBool(harvesterSnapshot, "IsActivated"))
				return;

			double abundance = BridgeUtils.SampleResourceAbundance(v, harvesterPrefab);
			if (abundance <= harvesterPrefab.HarvestThreshold)
				return;

			VesselResources resources = KERBALISM.ResourceCache.Get(v);
			ResourceRecipe recipe = new ResourceRecipe(KERBALISM.ResourceBroker.GetOrCreate(
				brokerName,
				KERBALISM.ResourceBroker.BrokerCategory.Harvester,
				brokerTitle));

			foreach (ResourceRatio input in harvesterPrefab.inputList)
				recipe.AddInput(input.ResourceName, input.Ratio * elapsed_s);

			recipe.AddOutput(harvesterPrefab.ResourceName, abundance * harvesterPrefab.Efficiency * elapsed_s, true);
			resources.AddRecipe(recipe);
			Lib.Proto.Set(harvesterSnapshot, "lastUpdateTime", Planetarium.GetUniversalTime());
		}

		internal static string AddPlannerConverterRates(
			ModuleSystemHeatConverter converter,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			string brokerTitle)
		{
			if (converter == null)
				return brokerTitle;

			foreach (ResourceRatio input in converter.inputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio));

			foreach (ResourceRatio output in converter.outputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(output.ResourceName, GetConverterEfficiency(converter) * output.Ratio));

			return brokerTitle;
		}

		internal static string AddPlannerHarvesterRates(
			ModuleSystemHeatHarvester harvester,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			string brokerTitle)
		{
			if (harvester == null)
				return brokerTitle;

			foreach (ResourceRatio input in harvester.inputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio));

			resourceChangeRequest.Add(new KeyValuePair<string, double>(harvester.ResourceName, harvester.Efficiency * 0.1));
			return brokerTitle;
		}

		private static float GetConverterEfficiency(ModuleSystemHeatConverter converter)
		{
			return BridgeModuleFields.GetFloat(converter, "Efficiency", 1f);
		}
	}
}
