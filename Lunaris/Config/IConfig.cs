using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris.Config
{
	public interface IConfig
	{
		/// <summary> If true save config as JSON. If false saves as binary. </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > JSON is not fully readable and only ment for debugging.
		///  </remarks>
		/// <example><code> Config.SaveAsJson = true; </code></example>
		bool SaveAsJson { get; set; }

		/// <summary>
		/// Register a typed config class and get back a handle.
		/// Lunaris will build the settings UI automatically.
		/// </summary>
		/// <example><code>
		/// var config = Config.Register&lt;MySettings&gt;();
		/// </code></example>
		IConfigHandle<T> Register<T>() where T : class, new();

		/// <summary>
		/// Register a typed config class and get back a handle.
		/// Also assigns the loaded config to <paramref name="instance"/>.
		/// Lunaris will build the settings UI automatically.
		/// </summary>
		/// <example><code>
		/// var config = Config.Register(ref instance);
		/// var config = Config.Register&lt;MySettings&gt;(ref instance);
		/// </code></example>
		IConfigHandle<T> Register<T>(ref T instance) where T : class, new();

		/// <summary> Read value from config. </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > Using the low-level API is not recommended. Use <see cref="Register{T}"/> or <see cref="Register{T}(ref T)"/> instead.
		///  </remarks>
		/// <example><code>
		/// var val = Config.Read&lt;int&gt;("Key");
		/// var val = Config.Read("Key", 1.0f);
		/// </code></example>
		T Read<T>(string key, T defaultValue = default);

		/// <summary> Write value to config. </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > Using the low-level API is not recommended. Use <see cref="Register{T}"/> or <see cref="Register{T}(ref T)"/> instead.
		///  </remarks>
		/// <example><code> Config.Write("Key", 2.0f); </code></example>
		void Write<T>(string key, T value);

		/// <summary> Set description for key. </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > Using the low-level API is not recommended. Use <see cref="Register{T}"/> or <see cref="Register{T}(ref T)"/> instead.
		///  </remarks>
		/// <example><code> Config.SetDesc("Key", "This value changes how..."); </code></example>
		void SetDesc(string key, string desc);

		/// <summary>
		/// Set range for key.
		/// </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > Using the low-level API is not recommended. Use <see cref="Register{T}"/> or <see cref="Register{T}(ref T)"/> instead.
		///  </remarks>
		/// <example><code> Config.SetRange("Key", 0f, 10f); </code></example>
		void SetRange(string key, float min, float max);

		/// <summary>
		/// Set section for key.
		/// </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > Using the low-level API is not recommended. Use <see cref="Register{T}"/> or <see cref="Register{T}(ref T)"/> instead.
		///  </remarks>
		/// <example><code> Config.SetSection("Key", "SectionName"); </code></example>
		void SetSection(string key, string section);

		/// <summary> Save config. Notice: Writing automatically saves. </summary>
		/// <example><code> Config.Save(); </code></example>
		void Save();

		/// <summary> Resets config to default values. </summary>
		/// <example><code> Config.Reset(); </code></example>
		void Reset();

		/// <summary> Subscribe to see if a value changed. </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > Using the low-level API is not recommended. Use <see cref="Register{T}"/> or <see cref="Register{T}(ref T)"/> instead.
		///  </remarks>
		/// <example><code> Config.OnChanged("Key", (val) => { }); </code></example>
		void OnChanged(string key, Action<object> callback);

		/// <summary> Subscribe to ALL changes. </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > Using the low-level API is not recommended. Use <see cref="Register{T}"/> or <see cref="Register{T}(ref T)"/> instead.
		///  </remarks>
		/// <example><code> Config.OnAnyChanged += (key, val) => { }; </code></example>
		Action<string, object> OnAnyChanged { get; set; }

		/// <summary> Returns all settings, for config UI. </summary>
		/// <example><code> var settings = Config.GetSettings(); </code></example>
		IReadOnlyDictionary<string, object> GetSettings();
	}
}
