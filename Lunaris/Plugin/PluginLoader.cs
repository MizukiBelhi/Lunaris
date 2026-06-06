using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Lunaris.Message;

namespace Lunaris
{

#pragma warning disable CS8632

	internal class PluginLoader
	{
		private static readonly List<PluginDescriptor> _plugins = [];

		public static string pluginPath = Path.Combine(AppContext.BaseDirectory, "plugins");
		public static string configPath = Path.Combine(pluginPath, "config");
		public static string cacheRoot = ".hotreload_cache";

		private static bool _isInit = false;

		internal static void Init()
		{
			if (_isInit) return;
			_isInit = true;

			if (!Directory.Exists(pluginPath))
				Directory.CreateDirectory(pluginPath);
			if (!Directory.Exists(cacheRoot))
				Directory.CreateDirectory(cacheRoot);

			OnInitLoadPlugins();
			LibraryLoader.LoadQueued();
			PluginUpdateChecker.CheckAll();
			PluginWatcher.Watch(pluginPath);
		}

		internal static void Update()
		{
			PluginWatcher.Update();
		}

		private static void OnInitLoadPlugins()
		{
			var files = Directory.GetFiles(pluginPath, "*.dll", SearchOption.AllDirectories).Where(f => !f.Split(Path.DirectorySeparatorChar).Contains("config")).ToArray();

			foreach (var file in files)
			{
				try {
					if (!PluginAssemblyUtils.IsManagedAssembly(file)) continue;
					ScanPlugin(file, Path.GetFileNameWithoutExtension(file), true);
				}
				catch (Exception ex)
				{
					var name = Path.GetFileNameWithoutExtension(file);
					Debug.LogError($"Failed to scan {name}: {ex}");
					Notifications.Add(new Notification(NotificationType.Error, $"Failed to load {name}, see console for info!", TimeSpan.FromSeconds(40)));
				}
			}
		}

		private static readonly List<PluginDescriptor> _loadQueue = [];

		internal static void EnqueueLoad(PluginDescriptor desc) => _loadQueue.Add(desc);

		internal static void RunLoadQueue()
		{
			var enabled = _loadQueue.Where(d => d.Manifest.IsEnabled).ToList();
			_loadQueue.Clear();

			int enabledCount = 0;
			foreach (var desc in enabled)
			{
				desc.filePath = PluginAssemblyUtils.CopyToCache(desc.OriginalFilePath);
				if(desc.filePath == null)
				{
					Notifications.Add(new Notification(NotificationType.Error, $"Could not enable {desc.SetPluginName}", Notifications.DefaultDuration));
					continue;
				}
				var res = LoadPluginFile(desc.filePath, desc.SetPluginName);
				if (!res.Item1)
					Notifications.Add(new Notification(NotificationType.Error, $"Could not enable {desc.SetPluginName}", Notifications.DefaultDuration));
				else
				{
					UI.installer.SetInstalledPluginLoaded(desc);
					enabledCount++;
				}
			}

			switch (enabledCount)
			{
				case > 1: Notifications.Add(new Notification(NotificationType.Info,$"{enabledCount} plugins enabled.", Notifications.DefaultDuration)); break;
				case 1: Notifications.Add(new Notification(NotificationType.Info,$"{enabled[0].Manifest.DisplayName} Enabled.", Notifications.DefaultDuration)); break;
			}
		}

		internal static (bool, PluginDescriptor) ScanPlugin(string file, string plName, bool register, int pluginIndex = -1)
		{
			try
			{

				var existing = _plugins.FirstOrDefault(t => t.filePath == file);
				if (existing != null) return (true, existing);

				var desc = new PluginDescriptor
				{
					OriginalFilePath = file,
					//filePath = PluginAssemblyUtils.CopyToCache(file)
				};

				if (string.IsNullOrEmpty(desc.OriginalFilePath))
				{
					Bridge.Logger.Log("ScanPlugin(): null cache path");
					return (false, null);
				}

				var readerParams = new ReaderParameters
				{
					ReadingMode = ReadingMode.Immediate,
					InMemory = true,
					AssemblyResolver = HarmonyFixes.resolver
				};

				using (var ms = new MemoryStream(File.ReadAllBytes(desc.OriginalFilePath)))
				{
					desc.Definition = AssemblyDefinition.ReadAssembly(ms, readerParams);
					desc.EffectivePermissions = PluginPermissions.GetUsedPermissions(desc.Definition);
				}

				var loader = PluginLoaderSelector.GetLoader(desc);
				if (loader == null)
				{
					LibraryLoader.Enqueue(new LibraryDescriptor
					{
						OriginalFilePath = file,
						Name = Path.GetFileNameWithoutExtension(file)
					});
					return (false, null);
				}

				var scanned = loader.LoadPlugin(desc, false);
				if (!scanned) return (false, null);

				if (register)
				{
					if (pluginIndex != -1) _plugins[pluginIndex] = desc;
					else _plugins.Add(desc);

					desc.Id = PluginAssemblyUtils.GetGuid(desc.OriginalFilePath);
					ApplyOrCreateManifest(desc);

					var deps = LibraryLoader.CollectDependencies(desc.Definition);
					if (deps != null)
					{
						desc.Manifest.Dependencies = deps;
						foreach (var dep in deps)
							LibraryLoader.RegisterDependency(desc.Id, dep);
					}

					UI.installer.AddInstalledPlugin(desc);
					Bridge.Logger.Log($"Plugin found: '{desc.Id}'");

					if (desc.Manifest.IsFromAPI && !string.IsNullOrEmpty(desc.Manifest.Id))
						PluginUpdateChecker.Enqueue(desc);
					else
						EnqueueLoad(desc);
				}

				return (true, desc);
			}
			catch (Exception ex)
			{
				Debug.LogError($"Failed to scan: {ex} \n {ex.StackTrace}");
				return (false, null);
			}
		}


		internal static (bool, PluginDescriptor) LoadPluginFile(string file, string plName)
		{
			var desc = _plugins.FirstOrDefault(t => t.filePath == file);
			if (desc == null)
			{
				Bridge.Logger.Log($"LoadPluginFile: '{plName}'");
				return (false, null);
			}

			bool loadedPlugin = false;
			try
			{
				desc.Assembly = Assembly.LoadFrom(file);
				if (desc.Assembly == null)
				{
					Bridge.Logger.Log("LoadPluginFile: assembly is null");
					return (false, null);
				}

				var loader = PluginLoaderSelector.GetLoader(desc);
				if (loader == null) return (false, null);

				if (!loader.LoadPlugin(desc, true)) // true = full load
				{
					Bridge.Logger.Log("LoadPluginFile: loader failed");
					Notifications.Add(new Notification(NotificationType.Error, $"Failed to load {plName}.", TimeSpan.FromSeconds(20)));
					return (false, desc);
				}

				loadedPlugin = true;
				desc.IsLoaded = true;
				desc.Manifest ??= new PluginManifest
				{
					Author = desc.Author,
					Id = desc.Id,
					Version = desc.Version,
					Description = desc.Description,
					DisplayName = desc.SetPluginName,
					DownloadCount = 0,
				};
				desc.Manifest.IsEnabled = true;
				PluginManifestHandler.StoreManifest(desc.Manifest);

				if (desc.EffectivePermissions.HasFlag(LunarisPermission.BepinPlugin))
					Notifications.Add(new Notification(NotificationType.Warning, $"{desc.SetPluginName} is using BepInEx, which is deprecated.", TimeSpan.FromSeconds(20)));

				return (true, desc);
			}
			catch (Exception e)
			{
				Bridge.Logger.LogError(e.Message + "\n" + e.StackTrace);
				if (loadedPlugin) UnloadPlugin(desc);
				return (false, null);
			}
		}

		internal static (bool, PluginDescriptor) LoadPluginFile(byte[] pluginBytes, string plName)
		{
			bool loadedPlugin = false;
			PluginDescriptor desc = null;
			try
			{
				desc = new PluginDescriptor();
				var readerParams = new ReaderParameters
				{
					ReadingMode = ReadingMode.Immediate,
					InMemory = true,
					AssemblyResolver = HarmonyFixes.resolver
				};

				using (var ms = new MemoryStream(pluginBytes))
				{
					desc.Definition = AssemblyDefinition.ReadAssembly(ms, readerParams);
					desc.EffectivePermissions = PluginPermissions.GetUsedPermissions(desc.Definition);
				}

				var loader = PluginLoaderSelector.GetLoader(desc);
				if (loader == null) return (false, null);

				if (!loader.LoadPluginFromBytes(desc)) return (false, desc);

				loadedPlugin = true;
				_plugins.Add(desc);

				ApplyOrCreateManifest(desc);
				return (true, desc);
			}
			catch (Exception e)
			{
				Bridge.Logger.LogError(e.Message + "\n" + e.StackTrace);
				if (loadedPlugin) UnloadPlugin(desc);
				return (false, null);
			}
		}


		internal static bool UnloadPlugin(PluginDescriptor desc)
		{
			try
			{
				if (desc?.GameObject == null) return true;

				UnityEngine.Object.DestroyImmediate(desc.GameObject);
				if (desc.Assembly != null)
				{
					try
					{
						PluginInitializer.CleanupPlugin(desc.Assembly);
						foreach (var type in desc.Assembly.GetTypes())
						{
							foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
							{
								if (!field.IsLiteral && !field.FieldType.IsValueType && !field.FieldType.IsGenericType)
									field.SetValue(null, null);
							}

							//var cctor = type.GetConstructors(BindingFlags.Static | BindingFlags.NonPublic).FirstOrDefault();
							//cctor?.Invoke(null, null);
						}
					}
					catch {  }
				}

				AssemblyHooks.RemLoc(desc.filePath);
				Resources.UnloadUnusedAssets();
				GC.Collect();

				desc.IsLoaded = false;
				desc.Manifest ??= new PluginManifest
				{
					Author = desc.Author,
					Id = desc.Id,
					Version = desc.Version,
					Description = desc.Description,
					DisplayName = desc.SetPluginName,
				};
				desc.Manifest.IsEnabled = false;
				PluginManifestHandler.StoreManifest(desc.Manifest);

				return true;
			}
			catch (Exception e)
			{
				Bridge.Logger.LogError(e.Message + "\n" + e.StackTrace);
				return false;
			}
		}

		internal static void RemovePlugin(PluginDescriptor desc)
		{
			_plugins.RemoveAll(p => p.Id == desc.Id);

			if (desc.Manifest?.Dependencies != null)
			{
				var orphans = LibraryLoader.UnregisterPlugin(desc.Id);
				foreach (var path in orphans)
				{
					try { if (File.Exists(path)) File.Delete(path); }
					catch (Exception ex) { Bridge.Logger.LogWarning($"Could not delete lib '{path}': {ex.Message}"); }
				}
			}

			if (File.Exists(desc.OriginalFilePath))
				File.Delete(desc.OriginalFilePath);

			UI.installer.RemoveInstalledPlugin(desc.Id);
			PluginManifestHandler.RemoveManifest(desc.Manifest?.Id);
		}


		internal static PluginDescriptor GetPluginFromAss(Assembly ass) => _plugins.FirstOrDefault(t => t.Assembly == ass);
		internal static PluginDescriptor GetPluginFromId(string id) => _plugins.FirstOrDefault(t => t.Id == id);
		internal static PluginDescriptor GetPluginByPath(string path) => _plugins.FirstOrDefault(t => t.filePath == path);
		internal static PluginDescriptor GetPluginByOriginalPath(string path) => _plugins.FirstOrDefault(t => t.OriginalFilePath == path);
		internal static int IndexOf(PluginDescriptor desc) => _plugins.IndexOf(desc);
		internal static PluginDescriptor GetPluginByAssemblyName(string name)
		{
			return _plugins.FirstOrDefault(t => t.Definition?.MainModule.Assembly.Name.Name == name);
		}

		private static void ApplyOrCreateManifest(PluginDescriptor desc)
		{
			var manifest = PluginManifestHandler.GetManifest(desc.Id);
			if (manifest != null)
			{
				//Bridge.Logger.Log($"found mani for {desc.Id} {manifest.IsEnabled} '{desc.Version}' '{manifest.Version}'");
				desc.Description = manifest.Description;
				// prefer embedded version
				if (!string.IsNullOrEmpty(desc.Version) && desc.Version != manifest.Version)
					manifest.Version = desc.Version;
				else
					desc.Version = manifest.Version;
				desc.Author = manifest.Author;
				desc.IsLoaded = manifest.IsEnabled;
				desc.Manifest = manifest;
			}
			else
			{
				desc.Manifest ??= new PluginManifest
				{
					Author = desc.Author,
					Id = desc.Id,
					Version = desc.Version,
					Description = desc.Description,
					DisplayName = desc.SetPluginName,
					IsEnabled = false,
					DownloadCount = 0,
				};
			}
		}

		public static void SetInstalledPluginHasUpdate(PluginDescriptor desc, string ver)
		{
			var item = UI.installer.pluginsInstalled.FirstOrDefault(t => t.desc == desc);
			if (item != null) item.hasUpdate = true;
		}
	}

#pragma warning restore CS8632
}