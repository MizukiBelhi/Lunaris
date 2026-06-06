using Lunaris.Message;
using System.Reflection;
using Lunaris.Config;
using System.Collections.Generic;
using System;

namespace Lunaris
{
	/// <summary>
	/// Initializes LunarisPlugins.
	/// </summary>
	internal class PluginInitializer
	{
		static readonly Dictionary<Assembly, WeakReference<LunarisPlugin>> _instances = new();

		internal static void InstantiateFields(LunarisPlugin plugin, Assembly assembly)
		{
			var desc = PluginLoader.GetPluginFromAss(assembly);
			if (desc == null) return; //Something weird happened

			var sanitizedName = desc.SetPluginName.Replace(" ", "").ToLower();

			plugin.Config = new ConfigInstance(sanitizedName);
			plugin.Logging = Bridge.Logger;
			plugin.Notification = Notifications.Get();
			_instances[assembly] = new WeakReference<LunarisPlugin>(plugin);

			ImGuiWrap.OnRender += plugin.OnImGuiDraw;

			/*InstantiateStatics(plugin, config);
			InstantiateStatics(plugin, Bridge.Logger);
			InstantiateStatics(plugin, notification);*/


			Commands.ParsePluginCommands(assembly, sanitizedName);
		}


		internal static void InstantiateStatics(LunarisPlugin plugin, object value)
		{
			var type = value.GetType();
			foreach (var field in plugin.GetType().GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
			{
				if (field.FieldType.IsAssignableFrom(type))
					field.SetValue(null, value);
			}
		}

		internal static void CleanupPlugin(Assembly assembly)
		{
			if (_instances.TryGetValue(assembly, out var weak))
			{
				if (weak.TryGetTarget(out var plugin))
					ImGuiWrap.OnRender -= plugin.OnImGuiDraw;
				_instances.Remove(assembly);
			}
		}
	}
}
