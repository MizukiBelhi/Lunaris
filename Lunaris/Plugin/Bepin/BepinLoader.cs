using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using static Lunaris.PluginLoader;

namespace Lunaris
{
	internal class BepInLoader : IPluginLoader
	{

		public bool LoadPlugin(PluginDescriptor descriptor, bool full)
		{
			if (!full)
			{
				descriptor.Source = ModSource.Legacy;
				descriptor.DeclaredPermissions = LunarisPermission.All;
				descriptor.Id = PluginAssemblyUtils.GetGuid(descriptor.OriginalFilePath);

				var hasf = GetPluginFromId(descriptor.Id);
				if (hasf != null)
				{
					Bridge.Logger.LogError($"Plugin already loaded: '{descriptor.Id}'");
					return false;
				}
			}

			return full?LoadPluginFull(descriptor):LoadDescriptor(descriptor);
		}

		public bool LoadPluginFromBytes(PluginDescriptor descriptor)
		{
			descriptor.Source = ModSource.Legacy;
			descriptor.DeclaredPermissions = LunarisPermission.All;
			return LoadDescriptor(descriptor);
		}

		public bool LoadDescriptor(PluginDescriptor descriptor)
		{
			if (descriptor.Definition == null) return false;

			foreach (var type in descriptor.Definition.MainModule.GetTypes())
			{
				if (type.BaseType != null && type.BaseType.Name == "BaseUnityPlugin")
				{
					var attr = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "BepInEx.BepInPlugin");

					if (attr != null && attr.ConstructorArguments.Count >= 3)
					{
						descriptor.Id = attr.ConstructorArguments[0].Value?.ToString();
						descriptor.SetPluginName = attr.ConstructorArguments[1].Value?.ToString();
						descriptor.Version = attr.ConstructorArguments[2].Value?.ToString();

						return true;
					}
				}

			}

			return false;
		}


		public bool LoadPluginFull(PluginDescriptor descriptor)
		{
			var pluginType = descriptor.Assembly.GetTypes().FirstOrDefault(t => t.BaseType != null && t.BaseType.Name == "BaseUnityPlugin");

			if (pluginType == null)
				return false;

			if (IsMetadataSafe(descriptor.Assembly, pluginType))
			{
				try
				{
					var go = new GameObject(descriptor.SetPluginName);
					var c = go.AddComponent(pluginType);
					UnityEngine.Object.DontDestroyOnLoad(c);

					descriptor.GameObject = go;
				}
				catch (Exception e) { Bridge.Logger.LogError(e.Message); return false; }
			}
			else
			{
				return false;
			}

			return true;
		}

		public bool IsMetadataSafe(Assembly assembly, Type ptype)
		{
			try
			{
				RuntimeHelpers.RunModuleConstructor(ptype.Module.ModuleHandle);
			}
			catch (Exception e)
			{
				Bridge.Logger.LogWarning($"Couldn't run Module constructor for {assembly.FullName}::{ptype}: {e}");
				return false;
			}
			return true;

			/*try
			{
				foreach (Type t in assembly.GetTypes())
				{
					Bridge.Logger.LogWarning($"try load {t}");
					t.GetCustomAttributes(false);
				}
				return true;
			}
			catch (Exception ex)
			{
				Bridge.Logger.LogError($"Metadata corruption detected in {assembly.FullName}: {ex.Message}");
				return false;
			}*/
		}
	}
}