using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Lunaris.Config
{
	internal static class ConfigHandler
	{
		private static readonly ConcurrentDictionary<string, IConfig> _configs = [];
		private static readonly Dictionary<string, List<IConfigHandleInternal>> _handles = [];
		private static Dictionary<string, List<(string Plugin, string Config, string Property)>> _conflictCache;
		private static bool _conflictsDirty = true;
		private static readonly HashSet<int> _heldVKs = [];
		private static readonly Dictionary<int, int> _downFrames = [];
		private static readonly Dictionary<int, int> _upFrames = [];

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

		internal static void DeleteConfig(string name)
		{
			Remove(name);

			var path = Path.Combine(PluginLoader.configPath, $"{name}.lpcfg");
			try { if (File.Exists(path)) File.Delete(path); }
			catch (Exception ex) { Bridge.Logger.LogWarning($"Could not delete config '{path}': {ex.Message}"); }
		}

		public static IConfig Get(string name) => _configs.TryGetValue(name, out var cfg) ? cfg : null;
		public static bool Has(string name) => _configs.ContainsKey(name);

		internal static IConfigHandle<T> Register<T>(string pluginName) where T : class, new()
		{
			if (!_configs.TryGetValue(pluginName, out var rawCfg))
				throw new InvalidOperationException($"No ConfigInstance for plugin '{pluginName}'. This should not happen!");

			var store = (ConfigInstance)rawCfg;
			var prefix = typeof(T).Name;

			var members = typeof(T).GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(m => m is PropertyInfo || m is FieldInfo).ToList();
			var settings = store.GetSettings();
			bool didChange = false;

			foreach (var m in members)
			{
				var aliases = m.GetCustomAttributes(typeof(ConfigAliasAttribute), inherit: true).OfType<ConfigAliasAttribute>().ToArray();
				if (aliases == null || aliases.Length == 0) continue;
				var memberType = m is PropertyInfo p ? p.PropertyType : ((FieldInfo)m).FieldType;

				foreach (var a in aliases)
				{
					var alias = a.Alias;
					if (string.IsNullOrEmpty(alias)) continue;

					var candidates = new List<string>();
					if (alias.Contains('.')) candidates.Add(alias);
					else { candidates.Add(prefix + "." + alias); candidates.Add(alias); }

					foreach (var cand in candidates)
					{
						if (string.IsNullOrEmpty(cand)) continue;
						if (!settings.ContainsKey(cand)) continue;

						var newKey = prefix + "." + m.Name;
						if (settings.ContainsKey(newKey)) break; // already have a value for new key

						var val = store.Read(cand, memberType, memberType.IsValueType ? Activator.CreateInstance(memberType) : null);
						store.WriteObjectNoSave(newKey, val, memberType);
						store.HideKeyNoSave(cand);
						Bridge.Logger.Log($"Config: Migrated '{cand}' => '{newKey}' for plugin '{pluginName}'");
						didChange = true;
						// refresh settings snapshot
						settings = store.GetSettings();
						break;
					}
				}
			}

			if (didChange)
				store.Save();

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

		internal static bool NotifyVKey(int vk, bool down)
		{
			if (vk == 0) return false;

			var frame = Time.frameCount;
			if (down)
			{
				if (!_heldVKs.Contains(vk))
					_downFrames[vk] = frame;
				_heldVKs.Add(vk);
			}
			else
			{
				if (_heldVKs.Contains(vk))
					_upFrames[vk] = frame;
				_heldVKs.Remove(vk);
			}

			bool handled = false;
			foreach (var handle in GetAllHandles())
				foreach (var kb in handle.GetKeybinds().Values)
				{
					kb.NotifyVKey(vk, down);
					if (kb.IsPressed || kb.IsReleased)
						handled = true;
				}

			return handled;
		}

		internal static bool IsShortcutDown(KeyCode mainKey, IEnumerable<KeyCode> modifiers) => IsShortcutState(mainKey, modifiers, ShortcutState.Down);
		internal static bool IsShortcutPressed(KeyCode mainKey, IEnumerable<KeyCode> modifiers) => IsShortcutState(mainKey, modifiers, ShortcutState.Pressed);
		internal static bool IsShortcutUp(KeyCode mainKey, IEnumerable<KeyCode> modifiers) => IsShortcutState(mainKey, modifiers, ShortcutState.Up);

		private enum ShortcutState { Down, Pressed, Up }

		private static bool IsShortcutState(KeyCode mainKey, IEnumerable<KeyCode> modifiers, ShortcutState state)
		{
			var mainVK = KeybindEntry.KeyToVK(mainKey);
			if (mainVK == 0) return false;

			var modifierVKs = (modifiers ?? []).Select(KeybindEntry.KeyToVK).Where(vk => vk != 0).ToHashSet();
			var required = modifierVKs.Append(mainVK).ToHashSet();
			var frame = Time.frameCount;

			return state switch
			{
				ShortcutState.Down => _downFrames.TryGetValue(mainVK, out var downFrame) && downFrame == frame && _heldVKs.SetEquals(required),
				ShortcutState.Pressed => _heldVKs.SetEquals(required),
				ShortcutState.Up => _upFrames.TryGetValue(mainVK, out var upFrame) && upFrame == frame && _heldVKs.SetEquals(modifierVKs),
				_ => false
			};
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
