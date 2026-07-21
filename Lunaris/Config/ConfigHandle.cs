using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Lunaris.Config
{
	internal class ConfigHandle<T> : IConfigHandle<T> where T : class, new()
	{
		private readonly ConfigInstance _store;
		private readonly string _prefix;
		private readonly T _current;

		private readonly Dictionary<string, List<Delegate>> _callbacks = new(StringComparer.OrdinalIgnoreCase);
		private readonly Dictionary<string, KeybindEntry> _keybinds = new(StringComparer.OrdinalIgnoreCase);
		internal readonly List<ConfigMemberInfo> _memberInfos = [];
		private readonly Dictionary<string, Func<object, object>> _getters = [];

		public event Action<T> OnAnyChanged;

		internal ConfigHandle(ConfigInstance store, string prefix)
		{
			_store = store;
			_prefix = prefix;

			var defaults = new T();
			foreach (var m in GetMembers())
			{
				if (!typeof(IKeybind).IsAssignableFrom(MemberType(m))) continue;

				var defaultKeys = m.GetCustomAttribute<KeybindAttribute>()?.Keys ?? [];
				var entry = new KeybindEntry(defaultKeys);
				_keybinds[m.Name] = entry;
				SetValue(m, defaults, entry);
			}

			_memberInfos = [.. GetMembers().Select(m =>
			{
				var combined = m.GetCustomAttribute<ConfigAttribute>();
				return new ConfigMemberInfo
				{
					Member = m,
					Name = m.Name,
					Type = MemberType(m),
					Label = combined?.Label ?? m.GetCustomAttribute<ConfigLabelAttribute>()?.Label ?? m.Name,
					Section = combined?.Section ?? m.GetCustomAttribute<ConfigSectionAttribute>()?.Section ?? "",
					Tooltip = combined?.Tooltip ?? m.GetCustomAttribute<ConfigDescriptionAttribute>()?.Description,
					Range = m.GetCustomAttribute<ConfigRangeAttribute>(),
					Hidden = m.GetCustomAttribute<ConfigHiddenAttribute>() != null,
					IsKeybind = typeof(IKeybind).IsAssignableFrom(MemberType(m)),
				};
			})];

			foreach (var mi in _memberInfos)
			{
				var captured = mi.Member;
				if (captured is PropertyInfo prop)
					_getters[mi.Name] = (obj) => prop.GetValue(obj);
				else if (captured is FieldInfo field)
					_getters[mi.Name] = (obj) => field.GetValue(obj);
			}

			_current = Load(defaults);
		}

		internal ConfigHandle(ConfigInstance store, string prefix, bool unusued__)
		{
			_store = store;
			_prefix = prefix;
		}

		internal KeybindEntry AddKeybind(string key, KeyCode[] defaultKeys, string desc=null, string sect=null)
		{
			if (_keybinds.TryGetValue(key, out var existing)) return existing;

			var saved = _store.Read(StoreKey(key), defaultKeys.Length > 0 ? string.Join(",", defaultKeys.Select(k => ((int)k).ToString())) : "");
			var keys = !string.IsNullOrEmpty(saved) ? [.. saved.Split(',').Select(s => (KeyCode)int.Parse(s))] : defaultKeys;

			var entry = new KeybindEntry(keys);
			_keybinds[key] = entry;

			_memberInfos.Add(new ConfigMemberInfo
			{
				Member = null,
				Name = key,
				Type = typeof(IKeybind),
				Label = key,
				Section = sect,
				IsKeybind = true,
				Tooltip = desc,
			});

			return entry;
		}

		public T Get() => _current;


		public void OnChanged<TProp>(TProp selector, Action<TProp> callback, [CallerArgumentExpression(nameof(selector))] string expr = null)
		{
			var name = expr?.Split('.').Last();
			if (!_callbacks.TryGetValue(name, out var list))
				_callbacks[name] = list = [];
			list.Add(callback);
		}

		public void OnChanged<TProp>(Expression<Func<T, TProp>> selector, Action<TProp> callback)
		{
			var name = PropertyName(selector);
			if (!_callbacks.TryGetValue(name, out var list))
				_callbacks[name] = list = [];
			list.Add(callback);
		}

		internal void SetProperty(string propertyName, object value)
		{
			var m = GetMembers().FirstOrDefault(x => x.Name == propertyName);
			if (m == null && !_keybinds.ContainsKey(propertyName)) return;
			if (m is PropertyInfo p && !p.CanWrite) return;

			if (_keybinds.TryGetValue(propertyName, out var kb))
			{
				if (value is KeyCode[] keys)
					kb.SetKeys(keys);
				else if (value is KeybindEntry other)
					kb.SetKeys(other.Keys);

				_store.Write(StoreKey(propertyName), string.Join(",", kb.Keys.Select(k => ((int)k).ToString())));
				ConfigHandler.InvalidateConflicts();
				FireCallbacks(propertyName, kb);
			}
			else
			{
				SetValue(m, _current, Convert.ChangeType(value, MemberType(m)));
				_store.Write(StoreKey(propertyName), value);
				FireCallbacks(propertyName, value);
			}

			OnAnyChanged?.Invoke(_current);
		}

		internal IEnumerable<MemberInfo> GetMembers() => typeof(T).GetMembers(BindingFlags.Public | BindingFlags.Instance).Where(m => m is PropertyInfo || m is FieldInfo);
		private Type MemberType(MemberInfo m) => m is PropertyInfo p ? p.PropertyType : ((FieldInfo)m).FieldType;
		internal object GetValue(MemberInfo m, object obj) => _getters.TryGetValue(m.Name, out var getter) ? getter(obj) : null;

		private static void SetValue(MemberInfo m, object obj, object value)
		{
			if (m is PropertyInfo p) p.SetValue(obj, value);
			else ((FieldInfo)m).SetValue(obj, value);
		}
		internal IReadOnlyDictionary<string, KeybindEntry> GetKeybinds() => _keybinds;

		private T Load(T instance)
		{
			foreach (var m in GetMembers())
			{
				if (_keybinds.TryGetValue(m.Name, out var kb))
				{
					var raw = _store.Read(StoreKey(m.Name), "");
					if (!string.IsNullOrEmpty(raw))
					{
						var keys = raw.Split(',').Select(s => (KeyCode)int.Parse(s)).ToArray();
						kb.SetKeys(keys);
					}
					SetValue(m, instance, kb);
				}
				else
				{
					var defaultVal = GetValue(m, instance);
					var readMethod = typeof(ConfigInstance).GetMethod(nameof(ConfigInstance.Read)).MakeGenericMethod(MemberType(m));
					var value = readMethod.Invoke(_store, [StoreKey(m.Name), defaultVal]);
					SetValue(m, instance, value);
				}
			}
			return instance;
		}

		internal void Save()
		{
			foreach (var m in GetMembers())
			{
				if (_keybinds.TryGetValue(m.Name, out var kb))
					_store.WriteNoSave(StoreKey(m.Name), string.Join(",", kb.Keys.Select(k => ((int)k).ToString())));
				else
					_store.WriteNoSave(StoreKey(m.Name), GetValue(m, _current));
			}
		}

		private string StoreKey(string propertyName) => $"{_prefix}.{propertyName}";

		private void FireCallbacks(string propertyName, object value)
		{
			if (!_callbacks.TryGetValue(propertyName, out var list)) return;
			foreach (var d in list)
				d.DynamicInvoke(value);
		}

		private static string PropertyName<TProp>(Expression<Func<T, TProp>> expr)
		{
			if (expr.Body is MemberExpression mem)
				return mem.Member.Name;
			throw new ArgumentException("Selector must be an expression.");
		}
	}
}
