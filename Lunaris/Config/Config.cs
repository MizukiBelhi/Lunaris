using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Lunaris.Config
{
	internal class ConfigInstance : IConfig
	{
		public bool SaveAsJson { get; set; } = false;

		internal readonly string PluginName;
		private readonly string _filePath;
		private Dictionary<string, SettingEntry> _settings = new(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, string> _descs = new(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, (float, float)> _ranges = new(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, string> _sects = new(StringComparer.OrdinalIgnoreCase);
		private Dictionary<string, SettingEntry> _defaults = [];
		private readonly Dictionary<string, List<Action<object>>> _Callbacks = [];

		internal class SettingEntry
		{
			[JsonIgnore] public Type Type;
			public object Value;
			public SettingEntry() { }
			public SettingEntry(object val, Type type)
			{
				Value = val;
				Type = type;
			}
		}


		public Action<string, object> OnAnyChanged { get; set; } = null;

		public IReadOnlyDictionary<string, object> GetSettings() => _settings.ToDictionary(x => x.Key, x => x.Value.Value);
		internal IReadOnlyDictionary<string, string> GetDescs() => _descs;
		internal IReadOnlyDictionary<string, (float, float)> GetRanges() => _ranges;
		internal IReadOnlyDictionary<string, string> GetSections() => _sects;

		private enum EntryType : byte { String = 0, Int = 1, Float = 2, Bool = 3, Long = 4, Vector2, Vector3, Color, Enum = 10, BsonBlob = 255 }

		private readonly HashSet<string> _hiddenKeys = new(StringComparer.OrdinalIgnoreCase);
		private const string HIDDEN_META_KEY = "__Lunaris.HiddenKeys";

		public ConfigInstance(string pluginName)
		{
			_filePath = Path.Combine(PluginLoader.configPath, $"{pluginName}.lpcfg");
			PluginName = pluginName;
			ConfigHandler.Add(pluginName, this);
			Load();
		}

		~ConfigInstance()
		{
			if (string.IsNullOrEmpty(PluginName)) return;
			ConfigHandler.Remove(PluginName);
		}

		public IConfigHandle<T> Register<T>() where T : class, new() => ConfigHandler.Register<T>(PluginName);
		public IConfigHandle<T> Register<T>(ref T instance) where T : class, new() => ConfigHandler.Register<T>(PluginName, ref instance);

		internal string Sanitize(string str)
		{
			if (string.IsNullOrEmpty(str)) return str;
			return str.Replace("%", "%%").ToLower();
		}

		internal KeybindEntry RegisterKeybind(string key, KeyCode[] keys, string desc = null, string sect = null)
		{
			var handle = ConfigHandler.GetOrCreateHandle(PluginName);
			return handle.AddKeybind(key, keys, Sanitize(desc), Sanitize(sect));
		}

		public void OnChanged(string key, Action<object> callback)
		{
			if (!_Callbacks.ContainsKey(key)) _Callbacks[key] = [];
			_Callbacks[key].Add(callback);
		}

		public void Reset()
		{
			foreach (var key in _settings.Keys.ToArray())
			{
				if (!_defaults.TryGetValue(key, out var def)) continue;

				if (_Callbacks.TryGetValue(key, out var cbs))
					foreach (var cb in cbs) cb(def.Value);

				OnAnyChanged?.Invoke(key, def.Value);
				_settings[key] = def;
			}
			Save();
		}

		public T Read<T>(string key, T defaultValue = default)
		{
			var result = Read(key, typeof(T), defaultValue);
			if (result is T typed) return typed;
			try { return (T)Convert.ChangeType(result, typeof(T)); }
			catch(Exception e) {
				Bridge.Logger.LogError($"Failed to read config key '{key}' ({result.GetType()}) as type {typeof(T)}. Exception: {e}");
				return defaultValue;
			}
		}

		internal object Read(string key, Type type, object defaultValue = null, int depth = 0)
		{
			if (!_defaults.ContainsKey(key))
				_defaults[key] = new(defaultValue, type);

			if (!_descs.ContainsKey(key))
				_descs[key] = null;

			if (!_settings.TryGetValue(key, out var value))
			{
				_settings[key] = new(defaultValue, type);
				return defaultValue;
			}

			try
			{
				if (value.Value is byte[] bytes && type != typeof(byte[]))
				{
					//DebugDumpBson(bytes);
					using var ms = new MemoryStream(bytes);
					using var reader = new BsonReader(ms);
					reader.ReadRootValueAsArray = false;
					var serializer = MakeBsonSerializer();

					var token = JToken.ReadFrom(reader);

					if (depth == 32)
					{
						Bridge.Logger.LogError($"Max config read depth reached for key '{key}', returning default value. Last token: {token}");
						return defaultValue;
					}

					//BSON sometimes gives us another BSON object
					if (token["$type"]?.ToString().Contains("System.Byte[]") == true)
					{
						var innerBytes = token["$value"].ToObject<byte[]>();
						value.Value = innerBytes;
						return Read(key, type, defaultValue, depth + 1);
					}

					var res = token.ToObject(type, serializer);
					value.Value = res;
					return res;
				}
				if (type.IsInstanceOfType(value.Value))
					return value.Value;

				return Convert.ChangeType(value.Value, value.Type);
			}
			catch (Exception e)
			{
				Bridge.Logger.LogError($"Failed to read config key '{key}' with value '{value}' ({value.Value?.GetType()}) as type {type}. Exception: {e}");
				return defaultValue;
			}
		}

		public void Write<T>(string key, T value)
		{
			if (!_settings.ContainsKey(key))
				_settings[key] = new(value, typeof(T));
			else
				_settings[key].Value = value;

			if (!_defaults.ContainsKey(key))
				_defaults[key] = new(value, typeof(T));

			if (!_descs.ContainsKey(key))
				_descs[key] = null;
			Save();

			if (_Callbacks.TryGetValue(key, out var actions))
				foreach (var action in actions) action(value);

			OnAnyChanged?.Invoke(key, value);
		}

		internal void WriteNoSave<T>(string key, T value)
		{
			if (!_settings.ContainsKey(key))
				_settings[key] = new(value, typeof(T));
			else
				_settings[key].Value = value;

			if (!_defaults.ContainsKey(key))
				_defaults[key] = new(value, typeof(T));

			if (!_descs.ContainsKey(key))
				_descs[key] = null;
		}

		internal void WriteBep<T>(string key, T value)
		{
			if (!_settings.ContainsKey(key))
				_settings[key] = new(value, typeof(T));

			if (!_defaults.ContainsKey(key))
				_defaults[key] = new(value, typeof(T));

			if (!_descs.ContainsKey(key))
				_descs[key] = null;
			Save();

			if (_Callbacks.TryGetValue(key, out var actions))
				foreach (var action in actions) action(value);

			OnAnyChanged?.Invoke(key, value);
		}

		public void SetDesc(string key, string desc)
		{
			_descs[key] = Sanitize(desc);
		}

		public void SetRange(string key, float min, float max)
		{
			_ranges[key] = (min, max);
		}

		public void SetSection(string key, string section)
		{
			_sects[key] = Sanitize(section);
		}

		//Removes a setting entirely.
		public void Remove(string key)
		{
			if (_settings.ContainsKey(key))
				_settings.Remove(key);
			if (_descs.ContainsKey(key))
				_descs.Remove(key);
			if (_ranges.ContainsKey(key))
				_ranges.Remove(key);
			if (_defaults.ContainsKey(key))
				_defaults.Remove(key);
			if (_Callbacks.ContainsKey(key))
				_Callbacks.Remove(key);
			Save();
		}

		internal IReadOnlyCollection<string> GetHiddenKeys()
		{
			if (_hiddenKeys.Count == 0)
			{
				var arr = Read(HIDDEN_META_KEY, typeof(string[]), Array.Empty<string>()) as string[];
				if (arr != null && arr.Length > 0)
				{
					foreach (var k in arr)
						if (!string.IsNullOrEmpty(k)) _hiddenKeys.Add(k);
				}
			}
			return _hiddenKeys;
		}

		internal void HideKeyNoSave(string key)
		{
			if (string.IsNullOrEmpty(key)) return;
			GetHiddenKeys();
			if (_hiddenKeys.Add(key))
				WriteObjectNoSave(HIDDEN_META_KEY, _hiddenKeys.ToArray(), typeof(string[]));
		}

		internal void HideKey(string key)
		{
			HideKeyNoSave(key);
			Save();
		}

		internal void WriteObjectNoSave(string key, object value, Type type)
		{
			if (!_settings.ContainsKey(key))
				_settings[key] = new SettingEntry(value, type);
			else
				_settings[key].Value = value;

			if (!_defaults.ContainsKey(key))
				_defaults[key] = new SettingEntry(value, type);

			if (!_descs.ContainsKey(key))
				_descs[key] = null;
		}

		internal void PrunePrefix(string prefix, IEnumerable<string> validMemberNames)
		{
			if (string.IsNullOrEmpty(prefix)) return;
			var valid = new HashSet<string>(validMemberNames, StringComparer.OrdinalIgnoreCase);
			var prefixDot = prefix + ".";
			var toRemove = _settings.Keys.Where(k => k.StartsWith(prefixDot, StringComparison.OrdinalIgnoreCase)).ToList();
			foreach (var fullKey in toRemove)
			{
				var remainder = fullKey.Length > prefixDot.Length ? fullKey.Substring(prefixDot.Length) : "";
				if (string.IsNullOrEmpty(remainder)) continue;
				if (valid.Contains(remainder)) continue;
				_settings.Remove(fullKey);
				if (_descs.ContainsKey(fullKey)) _descs.Remove(fullKey);
				if (_ranges.ContainsKey(fullKey)) _ranges.Remove(fullKey);
				if (_sects.ContainsKey(fullKey)) _sects.Remove(fullKey);
				if (_defaults.ContainsKey(fullKey)) _defaults.Remove(fullKey);
				if (_Callbacks.ContainsKey(fullKey)) _Callbacks.Remove(fullKey);
			}
			if (toRemove.Count > 0)
				Save();
		}

		public void Save()
		{
			foreach (var handle in ConfigHandler.GetHandles(PluginName))
				handle.Save();

			Directory.CreateDirectory(Path.GetDirectoryName(_filePath));

			if (SaveAsJson)
			{
				File.WriteAllText(_filePath, JsonConvert.SerializeObject(_settings, Formatting.Indented, MakeJsonSettings()));
				return;
			}

			using var writer = new BinaryWriter(File.Open(_filePath, FileMode.Create));
			writer.Write(0x4C504346); // LPCF
			writer.Write(_settings.Count);
			foreach (var kvp in _settings)
			{
				writer.Write(kvp.Key);
				WriteValue(writer, kvp.Value.Value);
			}


		}
		void DebugDumpBson(byte[] bytes)
		{
			try
			{
				using var ms = new MemoryStream(bytes);
				ms.Position = 0;
				using var reader = new BsonReader(ms);
				// Read into a JToken so we can inspect structure

				var token = JToken.ReadFrom(reader);
				Debug.Log("BSON -> JToken:\n" + token.ToString(Formatting.Indented));
			}
			catch (Exception ex)
			{
				Debug.LogError("DebugDumpBson failed: " + ex);
			}
		}

		private void WriteValue(BinaryWriter w, object value)
		{
			switch (value)
			{
				case string s: w.Write((byte)EntryType.String); w.Write(s); break;
				case int i: w.Write((byte)EntryType.Int); w.Write(i); break;
				case float f: w.Write((byte)EntryType.Float); w.Write(f); break;
				case bool b: w.Write((byte)EntryType.Bool); w.Write(b); break;
				case long l: w.Write((byte)EntryType.Long); w.Write(l); break;
				case Enum e:
				{
					w.Write((byte)EntryType.Enum);
					w.Write(value.GetType().AssemblyQualifiedName);
					w.Write(value.ToString());
					break;
				}
				default:
				w.Write((byte)EntryType.BsonBlob);
				using (var ms = new MemoryStream())
				{
					using (var bsonWriter = new BsonWriter(ms))
						MakeBsonSerializer().Serialize(bsonWriter, value);
					var bson = ms.ToArray();
					w.Write(bson.Length);
					w.Write(bson);
				}
				break;
			}
		}

		private void Load()
		{
			if (!File.Exists(_filePath)) return;
			var fileData = File.ReadAllBytes(_filePath);
			if (fileData.Length == 0) return;

			if (fileData[0] == '{')
			{
				_settings = JsonConvert.DeserializeObject<Dictionary<string, SettingEntry>>(File.ReadAllText(_filePath), MakeJsonSettings());
				return;
			}

			using var ms = new MemoryStream(fileData);
			using var reader = new BinaryReader(ms);
			if (reader.ReadInt32() != 0x4C504346) return;

			int count = reader.ReadInt32();
			for (int i = 0; i < count; i++)
			{
				string key = reader.ReadString();
				var type = (EntryType)reader.ReadByte();

				var (x, y) = ReadValue(reader, type);
				if (x == null)
				{
					Bridge.Logger.LogError($"Failed to read config key '{key}', stopping load of {_filePath}");
					return;
				}

				_settings[key] = new(x, y);
			}
		}

		private (object, Type) ReadValue(BinaryReader r, EntryType type)
		{
			//var type = (EntryType)r.ReadByte();
			Type eType = null;
			if (type == EntryType.Enum)
				eType = Type.GetType(r.ReadString());

			return type switch
			{
				EntryType.String => (r.ReadString(), typeof(string)),
				EntryType.Int => (r.ReadInt32(), typeof(int)),
				EntryType.Float => (r.ReadSingle(), typeof(float)),
				EntryType.Bool => (r.ReadBoolean(), typeof(bool)),
				EntryType.Long => (r.ReadInt64(), typeof(long)),
				EntryType.Enum => (Enum.Parse(eType, r.ReadString()), eType),
				EntryType.BsonBlob => (r.ReadBytes(r.ReadInt32()), typeof(byte[])),
				_ => (null, null)
			};
		}
		private static JsonSerializer MakeBsonSerializer() => new JsonSerializer
		{
			TypeNameHandling = TypeNameHandling.All,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			ContractResolver = new FieldsOnlyContractResolver(),
			FloatParseHandling = FloatParseHandling.Decimal,
		};

		private static JsonSerializerSettings MakeJsonSettings() => new JsonSerializerSettings
		{
			TypeNameHandling = TypeNameHandling.All,
			ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
			ContractResolver = new FieldsOnlyContractResolver(),
			FloatParseHandling = FloatParseHandling.Decimal,
			Converters = [new SettingEntryConverter()]
		};

		public class FieldsOnlyContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
		{
			protected override List<System.Reflection.MemberInfo> GetSerializableMembers(Type objectType)
			{
				if (objectType.IsPrimitive || objectType == typeof(string) || typeof(System.Collections.IEnumerable).IsAssignableFrom(objectType))
					return base.GetSerializableMembers(objectType);

				return [.. objectType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Cast<System.Reflection.MemberInfo>()];
			}
		}
		internal class SettingEntryConverter : JsonConverter<SettingEntry>
		{
			public override void WriteJson(JsonWriter writer, SettingEntry value, JsonSerializer serializer)
			{
				writer.WriteStartObject();

				writer.WritePropertyName("Type");
				writer.WriteValue(value.Type?.AssemblyQualifiedName);

				writer.WritePropertyName("Value");

				var tmp = JsonSerializer.Create(new JsonSerializerSettings
				{
					TypeNameHandling = TypeNameHandling.All,
					ReferenceLoopHandling = serializer.ReferenceLoopHandling,
					ContractResolver = serializer.ContractResolver,
					FloatParseHandling = FloatParseHandling.Decimal
				});

				tmp.Serialize(writer, value.Value);

				writer.WriteEndObject();
			}

			public override SettingEntry ReadJson(JsonReader reader, Type objectType, SettingEntry existingValue, bool hasExistingValue, JsonSerializer serializer)
			{
				var jo = JObject.Load(reader);

				var typeName = jo["Type"]?.ToString();
				var valueToken = jo["Value"];

				var entry = new SettingEntry();

				if (!string.IsNullOrEmpty(typeName))
				{
					var t = Type.GetType(typeName, throwOnError: false);
					entry.Type = t;
				}

				if (valueToken == null || valueToken.Type == JTokenType.Null)
				{
					entry.Value = null;
					return entry;
				}

				if (entry.Type != null)
				{
					if (entry.Type.IsEnum)
					{
						if (valueToken.Type == JTokenType.String)
						{
							var s = valueToken.ToObject<string>();
							try { entry.Value = Enum.Parse(entry.Type, s, true); }
							catch { entry.Value = Enum.ToObject(entry.Type, Convert.ChangeType(s, Enum.GetUnderlyingType(entry.Type))); }
						}
						else
						{
							var underlying = Enum.GetUnderlyingType(entry.Type);
							var val = valueToken.ToObject(underlying);
							entry.Value = Enum.ToObject(entry.Type, val);
						}
						return entry;
					}

					if (entry.Type.IsPrimitive || entry.Type == typeof(string) || typeof(IConvertible).IsAssignableFrom(entry.Type))
					{
						try
						{
							var obj = valueToken.ToObject<object>();
							entry.Value = Convert.ChangeType(obj, entry.Type);
							return entry;
						}
						catch { }
					}
					var tmp = JsonSerializer.Create(new JsonSerializerSettings
					{
						TypeNameHandling = TypeNameHandling.All,
						ReferenceLoopHandling = serializer.ReferenceLoopHandling,
						ContractResolver = serializer.ContractResolver,
						FloatParseHandling = FloatParseHandling.Decimal
					});

					try
					{
						entry.Value = valueToken.ToObject(entry.Type, tmp);
						return entry;
					}
					catch { }
				}
				if (valueToken.Type == JTokenType.Object || valueToken.Type == JTokenType.Array)
					entry.Value = valueToken;
				else
					entry.Value = valueToken.ToObject<object>();

				return entry;
			}
		}
	}
}