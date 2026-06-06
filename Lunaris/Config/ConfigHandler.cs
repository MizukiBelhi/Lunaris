using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lunaris.Config
{
	internal static class ConfigHandler
	{
		private static readonly ConcurrentDictionary<string, IConfig> _configs = [];
		private static readonly Dictionary<string, List<IConfigHandleInternal>> _handles = [];
		private static Dictionary<string, List<(string Plugin, string Config, string Property)>> _conflictCache;
		private static bool _conflictsDirty = true;

		internal static void InvalidateConflicts() => _conflictsDirty = true;

		public static void Add(string name, IConfig cfg)
		{
			if (!_configs.ContainsKey(name))
				_configs.TryAdd(name, cfg);
		}

		public static void Remove(string name)
		{
			_configs.TryRemove(name, out _);
			RemoveHandles(name);
		}

		public static IConfig Get(string name) => _configs.TryGetValue(name, out var cfg) ? cfg : null;
		public static bool Has(string name) => _configs.ContainsKey(name);

		internal static IConfigHandle<T> Register<T>(string pluginName) where T : class, new()
		{
			if (!_configs.TryGetValue(pluginName, out var rawCfg))
				throw new InvalidOperationException($"No ConfigInstance for plugin '{pluginName}'. This should not happen!");

			var store = (ConfigInstance)rawCfg;
			var prefix = typeof(T).Name;
			var handle = new ConfigHandle<T>(store, prefix);

			if (!_handles.ContainsKey(pluginName))
				_handles[pluginName] = [];

			_handles[pluginName].Add(new ConfigHandleWrapper<T>(handle));
			return handle;
		}

		internal static IConfigHandle<T> Register<T>(string pluginName, ref T instance) where T : class, new()
		{
			var handle = Register<T>(pluginName);
			instance = handle.Get();
			return handle;
		}

		private static Dictionary<string, List<(string, string, string)>> BuildConflicts()
		{
			var map = new Dictionary<string, List<(string, string, string)>>();
			foreach (var hndl in _handles)
			{
				var name = hndl.Key;
				var handles = hndl.Value;
				foreach (var handle in handles)
				{
					foreach (var kbp in handle.GetKeybinds())
					{
						var kb = kbp.Value;
						var prpName = kbp.Key;
						if (kb.Keys.Length == 0) continue;
						var key = string.Join("+", kb.Keys.OrderBy(k => (int)k));
						if (!map.ContainsKey(key))
							map[key] = [];
						map[key].Add((name, handle.ConfigTypeName, prpName));
					}
				}
			}
			return map.Where(kvp => kvp.Value.Count > 1).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
		}

		internal static Dictionary<string, List<(string Plugin, string Config, string Property)>> GetKeybindConflicts()
		{
			if (!_conflictsDirty) return _conflictCache;
			_conflictCache = BuildConflicts();
			_conflictsDirty = false;
			return _conflictCache;
		}

		internal static IReadOnlyList<IConfigHandleInternal> GetHandles(string pluginName) => _handles.TryGetValue(pluginName, out var list) ? list : Array.Empty<IConfigHandleInternal>();
		internal static IEnumerable<IConfigHandleInternal> GetAllHandles() => _handles.Values.SelectMany(x => x);
		private static void RemoveHandles(string pluginName) => _handles.Remove(pluginName);

		internal static void NotifyVKey(int vk, bool down)
		{
			foreach (var handle in GetAllHandles())
				foreach (var kb in handle.GetKeybinds().Values)
					kb.NotifyVKey(vk, down);
		}

		internal static ConfigHandle<BepInKeybinds> GetOrCreateHandle(string pluginName)
		{
			if (_handles.TryGetValue(pluginName, out var list))
			{
				var existing = list.OfType<ConfigHandleWrapper<BepInKeybinds>>().FirstOrDefault();
				if (existing != null) return existing._handle;
			}

			if (!_configs.TryGetValue(pluginName, out var rawCfg)) return null;
			var store = (ConfigInstance)rawCfg;
			var handle = new ConfigHandle<BepInKeybinds>(store, "KB", true);

			if (!_handles.ContainsKey(pluginName))
				_handles[pluginName] = [];
			_handles[pluginName].Add(new ConfigHandleWrapper<BepInKeybinds>(handle));
			return handle;
		}

		internal class BepInKeybinds { }
	}

}