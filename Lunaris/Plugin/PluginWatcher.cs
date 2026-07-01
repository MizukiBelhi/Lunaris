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
		private const int PendingDelayMs = 1000;
		private const int MaxAttempts = 5;

		private struct PendingFile
		{
			public string Path;
			public string OldPath;
			public long Time;
			public string Id;
			public int Attempts;
			public bool Deleted;
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
			RenamedEventHandler r = (_, e) => DispatcherBehaviour.RunOnMainThread(() => QueuePending(e.FullPath, e.OldFullPath));

			_watcher.Changed += h;
			_watcher.Created += h;
			_watcher.Deleted += h;
			_watcher.Renamed += r;
			_watcher.EnableRaisingEvents = true;
			_watcher.Error += (_, e) => Debug.LogError($"[PluginWatcher] {e.GetException().Message}");
		}

		internal static void Update()
		{
			long now = NowMs();
			var ready = _pending.Values.Where(f => f.Time + PendingDelayMs <= now).ToList();

			foreach (var f in ready)
			{
				if (!_pending.ContainsKey(f.Id)) continue;

				try
				{
					if (f.Deleted)
					{
						ProcessDeleted(f);
						continue;
					}

					if (!File.Exists(f.Path))
					{
						Reschedule(f);
						continue;
					}

					if (!PluginAssemblyUtils.IsManagedAssembly(f.Path))
					{
						_pending.Remove(f.Id);
						continue;
					}

					if (!string.IsNullOrEmpty(f.OldPath))
					{
						_pending.Remove(f.Id);
						OnRenamed(f.Path, f.OldPath);
						continue;
					}

					ProcessCreated(f);
				}
				catch (IOException)
				{
					Reschedule(f);
				}
				catch (UnauthorizedAccessException)
				{
					Reschedule(f);
				}
				catch (Exception ex)
				{
					if (f.Attempts < MaxAttempts)
					{
						Reschedule(f);
						continue;
					}

					_pending.Remove(f.Id);
					Bridge.Logger.LogError($"PluginWatcher: failed to process '{f.Path}': {ex.Message}\n{ex.StackTrace}");
				}
			}

			if (ready.Count > 0)
				LibraryLoader.LoadQueued();
		}

		private static bool IsConfigPath(string path) => path.Split(Path.DirectorySeparatorChar).Any(p => p.Equals("config", StringComparison.OrdinalIgnoreCase));

		private static long NowMs() => DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

		private static string PendingId(string path) => Path.GetFullPath(path).ToLowerInvariant();

		private static void QueuePending(string path, string oldPath = null, bool deleted = false)
		{
			if (IsConfigPath(path) || (oldPath != null && IsConfigPath(oldPath))) return;

			var id = PendingId(oldPath ?? path);
			var attempts = _pending.TryGetValue(id, out var pending) ? pending.Attempts : 0;
			_pending[id] = new PendingFile { Path = path, OldPath = oldPath, Time = NowMs(), Id = id, Attempts = attempts, Deleted = deleted };
		}

		private static void Reschedule(PendingFile file)
		{
			if (file.Attempts >= MaxAttempts)
			{
				_pending.Remove(file.Id);
				Bridge.Logger.LogWarning($"PluginWatcher: '{file.Path}' did not settle.");
				return;
			}

			file.Time = NowMs();
			file.Attempts++;
			_pending[file.Id] = file;
		}

		private static void ProcessCreated(PendingFile file)
		{
			var plugin = PluginLoader.GetPluginByOriginalPath(file.Path);
			if (plugin != null)
			{
				_pending.Remove(file.Id);
				OnRenamed(file.Path, file.Path);
				return;
			}

			var guid = PluginAssemblyUtils.GetGuid(file.Path);
			_pending.Remove(file.Id);

			if (guid == "unk") return;
			if (PluginLoader.GetPluginFromId(guid) != null) return;

			var result = PluginLoader.ScanPlugin(file.Path, guid, true);
			if (result.Item1)
				PluginLoader.LoadPluginFile(file.Path, result.Item2.SetPluginName);
		}

		private static void ProcessDeleted(PendingFile file)
		{
			if (File.Exists(file.Path))
			{
				QueuePending(file.Path);
				return;
			}

			_pending.Remove(file.Id);

			var plugin = PluginLoader.GetPluginByOriginalPath(file.Path);
			if (plugin == null) return;

			if (!UI.installer.IsPluginLoaded(plugin.Id))
				PluginLoader.RemovePlugin(plugin);
		}

		private static void OnChanged(string filePath, WatcherChangeTypes type)
		{
			if (IsConfigPath(filePath)) return;

			switch (type)
			{
				case WatcherChangeTypes.Changed:
					if (File.Exists(filePath))
						QueuePending(filePath, filePath);
				break;

				case WatcherChangeTypes.Created:
					QueuePending(filePath);
				break;

				case WatcherChangeTypes.Deleted:
					QueuePending(filePath, deleted: true);
				break;
			}
		}

		private static void OnRenamed(string newPath, string oldPath)
		{
			if (IsConfigPath(newPath) || IsConfigPath(oldPath)) return;

			var plugin = PluginLoader.GetPluginByOriginalPath(oldPath);
			if (plugin == null) return;
			if (!PluginAssemblyUtils.IsManagedAssembly(newPath)) return;

			var readerParams = new ReaderParameters { ReadingMode = ReadingMode.Immediate, InMemory = true };
			using var ms = new MemoryStream(File.ReadAllBytes(newPath));
			using var incoming = AssemblyDefinition.ReadAssembly(ms, readerParams);

			if (incoming.MainModule.Mvid != plugin.Definition.MainModule.Mvid)
			{
				var wasLoaded = plugin.IsLoaded;

				var result = PluginLoader.ScanPlugin(newPath, plugin.SetPluginName, true, PluginLoader.IndexOf(plugin));

				if (result.Item1 && wasLoaded)
				{
					PluginLoader.UnloadPlugin(plugin);
					PluginLoader.LoadPluginFile(result.Item2.filePath, result.Item2.SetPluginName);
				}
			}
			else
			{
				plugin.OriginalFilePath = newPath;
			}
		}
	}
}
