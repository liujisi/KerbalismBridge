using System.Collections.Generic;
using KERBALISM;
using SystemHeat;
using KerbalismBridge;

namespace KerbalismNative
{
	internal static class SHNativeConverterResourceSim
	{
		internal static string AddLoadedConverterRates(
			ModuleSystemHeatConverter converter,
			string brokerTitle,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (converter == null)
				return brokerTitle;

			converter.lastTimeFactor = 0.0;

			if (!converter.IsActivated || !converter.ModuleIsActive())
				return brokerTitle;

			double scale = converter.GetHeatThrottle();
			if (scale <= double.Epsilon)
				return brokerTitle;

			double inputScale = GetInputAvailabilityScale(converter.vessel, converter.inputList, availableResources, scale);
			if (inputScale <= double.Epsilon)
				return brokerTitle;

			double efficiency = GetConverterEfficiency(converter);
			double outputScale = GetOutputAvailabilityScale(converter.vessel, converter.outputList, efficiency, scale);
			if (outputScale <= double.Epsilon)
				return brokerTitle;

			double finalScale = scale * System.Math.Min(inputScale, outputScale);
			if (finalScale <= double.Epsilon)
				return brokerTitle;

			converter.lastTimeFactor = 1.0;

			foreach (ResourceRatio input in converter.inputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio * finalScale));

			foreach (ResourceRatio output in converter.outputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(output.ResourceName, efficiency * output.Ratio * finalScale));

			return brokerTitle;
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

		private static double GetOutputAvailabilityScale(Vessel vessel, List<ResourceRatio> outputList, double efficiency, double scale)
		{
			if (outputList == null || outputList.Count == 0)
				return 1d;

			VesselResources vesselResources = vessel != null ? KERBALISM.ResourceCache.Get(vessel) : null;
			if (vesselResources == null)
				return 1d;

			double outputScale = 1d;
			foreach (ResourceRatio output in outputList)
			{
				if (output.DumpExcess)
					continue;

				ResourceInfo resource = vesselResources.GetResource(vessel, output.ResourceName);
				if (resource == null)
					continue;

				double availableRoom = resource.Capacity - (resource.Amount + resource.Deferred);
				double limit = availableRoom / (efficiency * output.Ratio * scale);
				outputScale = System.Math.Min(outputScale, limit);
				if (outputScale <= double.Epsilon)
					return 0d;
			}

			return System.Math.Min(1d, outputScale);
		}

		internal static string AddLoadedHarvesterRates(
			ModuleSystemHeatHarvester harvester,
			string brokerTitle,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			if (harvester == null)
				return brokerTitle;

			harvester.lastTimeFactor = 0.0;

			if (!harvester.IsActivated || !harvester.ModuleIsActive())
				return brokerTitle;

			double scale = harvester.GetHeatThrottle();
			if (scale <= double.Epsilon)
				return brokerTitle;

			double inputScale = GetInputAvailabilityScale(harvester.vessel, harvester.inputList, availableResources, scale);
			if (inputScale <= double.Epsilon)
				return brokerTitle;

			double abundance = BridgeUtils.SampleResourceAbundance(harvester.vessel, harvester);
			if (abundance <= harvester.HarvestThreshold)
				return brokerTitle;

			harvester.lastTimeFactor = 1.0;

			foreach (ResourceRatio input in harvester.inputList)
				resourceChangeRequest.Add(new KeyValuePair<string, double>(input.ResourceName, -input.Ratio * scale));

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
