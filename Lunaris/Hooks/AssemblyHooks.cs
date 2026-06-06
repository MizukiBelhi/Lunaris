using HarmonyLib;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{

	/// <summary>
	/// Hooks Assembly.Location to remap
	/// cached plugins to their original folder
	/// </summary>
	internal class AssemblyHooks
	{
		private static readonly Dictionary<string, string> LocationRemap = [];
		private static bool isHooked = false;
		private static Hook _hook; //Required or the hook dies
		internal static string GetLoc(string loc)
		{
			//Bridge.Logger.LogInfo($"TryGetLoc: {loc.ToLowerInvariant()}");

			if (LocationRemap.TryGetValue(loc.ToLowerInvariant(), out var original))
				return original;

			else return loc;
		}

		internal static void AddLoc(string loc, string nloc)
		{
			if(!isHooked)
			{
				HookM();
				isHooked = true;
			}
			var ll = loc.ToLowerInvariant();

			//Bridge.Logger.LogInfo($"TryAddLoc: {ll} ==== {nloc}");

			if (!LocationRemap.ContainsKey(ll))
				LocationRemap.Add(ll, nloc);
		}

		internal static void RemLoc(string nloc)
		{
			var ll = nloc.ToLowerInvariant();
			if (LocationRemap.ContainsKey(ll))
				LocationRemap.Remove(ll);
		}

		internal static void HookM()
		{
			var runtimeAssemblyType = typeof(Assembly).Assembly.GetType("System.Reflection.RuntimeAssembly");
			var getLocation = runtimeAssemblyType.GetProperty("Location", BindingFlags.Public | BindingFlags.Instance).GetGetMethod();

			_hook = new Hook(getLocation, (Func<Assembly, string> orig, Assembly self) =>
			{
				var result = orig(self);
				return GetLoc(result);
			});
		}
	}

}
