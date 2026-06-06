using System.Collections.Generic;
using System.Reflection;

namespace Lunaris.Config
{
	internal class ConfigHandleWrapper<T>(ConfigHandle<T> handle) : IConfigHandleInternal where T : class, new()
	{
		internal readonly ConfigHandle<T> _handle = handle;
		public string ConfigTypeName => typeof(T).Name;
		public IReadOnlyDictionary<string, KeybindEntry> GetKeybinds() => _handle.GetKeybinds();
		public void SetProperty(string propertyName, object value) => _handle.SetProperty(propertyName, value);
		public IEnumerable<MemberInfo> GetProperties() => _handle.GetMembers();

		public object GetPropertyValue(string name)
		{
			var t = typeof(T);
			MemberInfo m = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance) ?? (MemberInfo)t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
			return m == null ? null : _handle.GetValue(m, _handle.Get());
		}

		public IReadOnlyList<ConfigMemberInfo> GetMemberInfos() => _handle._memberInfos;
		public void Save() => _handle.Save();
	}
}
