using KerbalismBridge;

namespace KerbalismNative
{
	public static class KerbalismNativeCoreInit
	{
		public static void Initialize()
		{
			BridgeUtils.Log("[zKerbalismNative] v1.0.8-smelter-debug-2 loaded");
			KerbalismNativeHarmony.ApplyPatches();
		}
	}
}
