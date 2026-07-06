using System.Collections.Generic;
using KSP.Localization;
using KERBALISM;
using SystemHeat;
using KerbalismBridge;

namespace KerbalismNative
{
	/// <summary>
	/// Routes ModuleSystemHeatHarvester resource IO through Kerbalism while keeping native SystemHeat behaviour.
	/// </summary>
	public class SystemHeatHarvesterKerbalismUpdater : PartModule, IKerbalismModule
	{
		public static string brokerName = "SHNativeHarvester";
		public static string brokerTitle = Localizer.Format("#LOC_KerbalismBridge_Brokers_Harvester");

		[KSPField(isPersistant = true)]
		public string harvesterModuleID = "harvester";

		protected ModuleSystemHeatHarvester harvesterModule;

		internal bool OwnsHarvester(ModuleSystemHeatHarvester harvester)
		{
			return harvester != null
				&& harvesterModuleID == harvester.moduleID
				&& harvester.part == part;
		}

		internal ModuleSystemHeatHarvester FindHarvesterModule()
		{
			if (harvesterModule != null)
				return harvesterModule;

			harvesterModule = FindHarvesterPrefab(part, harvesterModuleID);
			return harvesterModule;
		}

		public string ResourceUpdate(Dictionary<string, double> availableResources, List<KeyValuePair<string, double>> resourceChangeRequest)
		{
			return SHNativeConverterResourceSim.AddLoadedHarvesterRates(
				FindHarvesterModule(),
				brokerTitle,
				availableResources,
				resourceChangeRequest);
		}

		public string PlannerUpdate(List<KeyValuePair<string, double>> resourceChangeRequest, CelestialBody body, Dictionary<string, double> environment)
		{
			ModuleSystemHeatHarvester harvester = FindHarvesterModule();
			if (harvester != null && harvester.IsActivated)
				return SHNativeConverterResourceSim.AddPlannerHarvesterRates(harvester, resourceChangeRequest, brokerTitle);
			return brokerTitle;
		}

		public static string BackgroundUpdate(
			Vessel v,
			ProtoPartSnapshot part_snapshot,
			ProtoPartModuleSnapshot module_snapshot,
			PartModule proto_part_module,
			Part proto_part,
			Dictionary<string, double> availableResources,
			List<KeyValuePair<string, double>> resourceChangeRequest,
			double elapsed_s)
		{
			string moduleId = Lib.Proto.GetString(module_snapshot, "harvesterModuleID");
			ProtoPartModuleSnapshot harvesterSnapshot = FindHarvesterSnapshot(part_snapshot, moduleId);
			ModuleSystemHeatHarvester harvesterPrefab = FindHarvesterPrefab(proto_part, moduleId);

			if (harvesterSnapshot != null && harvesterPrefab != null)
			{
				SHNativeConverterResourceSim.BackgroundUpdateHarvester(
					v,
					harvesterSnapshot,
					harvesterPrefab,
					brokerName,
					brokerTitle,
					elapsed_s);
			}

			SystemHeatBackgroundThermal.TryRun(v, elapsed_s);
			return brokerTitle;
		}

		private static ModuleSystemHeatHarvester FindHarvesterPrefab(Part part, string moduleId)
		{
			ModuleSystemHeatHarvester firstHarvester = null;
			for (int i = 0; i < part.Modules.Count; i++)
			{
				ModuleSystemHeatHarvester harvester = part.Modules[i] as ModuleSystemHeatHarvester;
				if (harvester == null)
					continue;

				if (firstHarvester == null)
					firstHarvester = harvester;

				if (harvester.moduleID == moduleId)
					return harvester;
			}

			return firstHarvester;
		}

		private static ProtoPartModuleSnapshot FindHarvesterSnapshot(ProtoPartSnapshot partSnapshot, string moduleId)
		{
			ProtoPartModuleSnapshot firstHarvester = null;
			for (int i = 0; i < partSnapshot.modules.Count; i++)
			{
				ProtoPartModuleSnapshot module = partSnapshot.modules[i];
				if (module.moduleName != "ModuleSystemHeatHarvester")
					continue;

				if (firstHarvester == null)
					firstHarvester = module;

				if (Lib.Proto.GetString(module, "moduleID") == moduleId)
					return module;
			}

			return firstHarvester;
		}
	}
}
