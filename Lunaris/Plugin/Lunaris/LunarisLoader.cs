using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Lunaris.PluginLoader;

namespace Lunaris
{
	internal class LunarisLoader : IPluginLoader
	{

		public bool LoadPlugin(PluginDescriptor descriptor, bool full)
		{
			if (!full)
			{
				descriptor.Id = PluginAssemblyUtils.GetGuid(descriptor.OriginalFilePath);

				var hasf = GetPluginFromId(descriptor.Id);
				if (hasf != null)
				{
					Bridge.Logger.LogError($"Plugin already loaded: '{descriptor.Id}'");
					return false;
				}
			}
			descriptor.Source = ModSource.Lunaris;

			return full ? LoadPluginFull(descriptor) : LoadDescriptor(descriptor);
		}

		public bool LoadPluginFromBytes(PluginDescriptor descriptor)
		{
			descriptor.Source = ModSource.Lunaris;

			return LoadDescriptor(descriptor);
		}


		public bool LoadDescriptor(PluginDescriptor descriptor)
		{
			if (descriptor.Definition == null) return false;

			foreach (var type in descriptor.Definition.MainModule.GetTypes())
			{
				if (type.BaseType == null || type.BaseType.Name != "LunarisPlugin")
					continue;

				var attr = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "LunarisPluginAttribute");
				if (attr != null)
				{
					descriptor.SetPluginName = attr.ConstructorArguments[0].Value?.ToString();
					descriptor.Version = attr.ConstructorArguments[1].Value?.ToString();
					descriptor.Author = attr.ConstructorArguments.Count > 2 ? attr.ConstructorArguments[2].Value?.ToString() : "Unknown";
					descriptor.Description = attr.ConstructorArguments.Count > 3 ? attr.ConstructorArguments[3].Value?.ToString() : "";
					descriptor.Id = type.FullName;

					var permAttr = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "LunarisPermissionAttribute");
					if (permAttr != null && permAttr.ConstructorArguments.Count > 0)
						descriptor.DeclaredPermissions = (LunarisPermission)permAttr.ConstructorArguments[0].Value;
					else
						descriptor.DeclaredPermissions = LunarisPermission.None;

					return true;
				}
			}

			return false;
		}

		public bool LoadPluginFull(PluginDescriptor descriptor)
		{
			var pluginType = descriptor.Assembly.GetTypes().FirstOrDefault(t => t.BaseType != null && t.BaseType.Name == "LunarisPlugin");

			if (pluginType == null)
				return false;

			try
			{
				var go = new GameObject(descriptor.SetPluginName);
				var c = go.AddComponent(pluginType);
				UnityEngine.Object.DontDestroyOnLoad(c);

				descriptor.GameObject = go;
			}
			catch (Exception e) { Bridge.Logger.LogError(e.Message); return false; }

			return true;
		}

		
	}
}
