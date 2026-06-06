using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	internal static class PluginLoaderSelector
	{
		private static readonly Dictionary<LunarisPermission, IPluginLoader> pluginLoaders = new() { { LunarisPermission.BepinPlugin, new BepInLoader() }, { LunarisPermission.LunarisPlugin, new LunarisLoader() } };


		public static IPluginLoader GetLoader(PluginDescriptor descriptor)
		{
			foreach(var loader in pluginLoaders)
			{
				if(descriptor.EffectivePermissions.HasFlag(loader.Key))
				{
					return loader.Value;
				}
			}
			return null;
		}
	}
}
