namespace KerbalismNative
{
	/// <summary>
	/// Called by zKerbalismPluginHost after zKerbalismNative.dll is loaded (requires Bridge + Kerbalism).
	/// </summary>
	public static class KerbalismNativeCoreInit
	{
		public static void Initialize()
		{
			KerbalismNativeHarmony.ApplyPatches();
		}
	}
}
