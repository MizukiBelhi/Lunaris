using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Lunaris
{
	internal static class PluginWatcher
	{
		private static FileSystemWatcher _watcher;

		private struct PendingFile
		{
			public string Path;
			public long Time;
			public string Id;
		}

		private static readonly Dictionary<string, PendingFile> _pending = [];

		internal static void Watch(string path)
		{
			_watcher = new FileSystemWatcher(path)
			{
				IncludeSubdirectories = true,
				NotifyFilter =
					NotifyFilters.Attributes | NotifyFilters.CreationTime |
					NotifyFilters.FileName | NotifyFilters.LastAccess |
					NotifyFilters.LastWrite | NotifyFilters.Size |
					NotifyFilters.Security | NotifyFilters.DirectoryName,
				Filter = "*.dll"
			};

			FileSystemEventHandler h = (_, e) => DispatcherBehaviour.RunOnMainThread(() => OnChanged(e.FullPath, e.ChangeType));
			RenamedEventHandler r = (_, e) => DispatcherBehaviour.RunOnMainThread(() => OnRenamed(e.FullPath, e.OldFullPath));

			_watcher.Changed += h;
			_watcher.Created += h;
			_watcher.Deleted += h;
			_watcher.Renamed += r;
			_watcher.EnableRaisingEvents = true;
			_watcher.Error += (_, e) => Debug.LogError($"[PluginWatcher] {e.GetException().Message}");
		}

		internal static void Update()
		{
			long now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
			var ready = _pending.Values.Where(f => f.Time + 1000 <= now).ToList();

			foreach (var f in ready)
			{
				var guid = PluginAssemblyUtils.GetGuid(f.Path);
				if (guid == "Unknown") return;

				_pending.Remove(f.Id);

				if (PluginLoader.GetPluginFromId(guid) != null) continue;
				if (PluginLoader.GetPluginByOriginalPath(f.Path) != null) continue;
				if (!PluginAssemblyUtils.IsManagedAssembly(f.Path)) continue;
				var result = PluginLoader.ScanPlugin(f.Path, guid, true);
				if (result.Item1)
					PluginLoader.LoadPluginFile(f.Path, result.Item2.SetPluginName);
			}
		}

		private static bool IsConfigPath(string path) => path.Split(Path.DirectorySeparatorChar).Any(p => p.Equals("config", StringComparison.OrdinalIgnoreCase));

		private static void OnChanged(string filePath, WatcherChangeTypes type)
		{
			if (IsConfigPath(filePath)) return;

			switch (type)
			{
				case WatcherChangeTypes.Changed:
					if (File.Exists(filePath))
						OnRenamed(filePath, filePath);
				break;

				case WatcherChangeTypes.Created:
					var tempId = Guid.NewGuid().ToString();
					_pending[tempId] = new PendingFile { Path = filePath, Time = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond, Id = tempId };
				break;

				case WatcherChangeTypes.Deleted:
					var plugin = PluginLoader.GetPluginByOriginalPath(filePath);
					if (plugin == null) return;

					if (_pending.TryGetValue(plugin.Id, out var pending))
					{
						_pending.Remove(plugin.Id);
						OnRenamed(pending.Path, filePath);
					}
					else if (!File.Exists(filePath) && !UI.installer.IsPluginLoaded(plugin.Id))
					{
						PluginLoader.RemovePlugin(plugin);
					}
				break;
			}

			LibraryLoader.LoadQueued();
		}

		private static void OnRenamed(string newPath, string oldPath)
		{
			if (IsConfigPath(newPath) || IsConfigPath(oldPath)) return;

			var plugin = PluginLoader.GetPluginByOriginalPath(oldPath);
			if (plugin == null) return;
			if (!PluginAssemblyUtils.IsManagedAssembly(newPath)) return;

			var readerParams = new ReaderParameters { ReadingMode = ReadingMode.Immediate, InMemory = true };
			using var ms = new MemoryStream(File.ReadAllBytes(newPath));
			var incoming = AssemblyDefinition.ReadAssembly(ms, readerParams);

			if (incoming.MainModule.Mvid != plugin.Definition.MainModule.Mvid)
			{
				var wasLoaded = plugin.IsLoaded;
				
				var result = PluginLoader.ScanPlugin(newPath, plugin.SetPluginName, true, PluginLoader.IndexOf(plugin));

				if (result.Item1 && wasLoaded)
				{
					PluginLoader.UnloadPlugin(plugin);
					PluginLoader.LoadPluginFile(newPath, plugin.SetPluginName);
				}
			}
			else
			{
				plugin.OriginalFilePath = newPath;
			}
			incoming.Dispose();
		}
	}
}