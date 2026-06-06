using System;
using System.Collections.Generic;
using System.Reflection;


namespace Lunaris.Config
{
	internal interface IConfigHandleInternal
	{
		string ConfigTypeName { get; }
		IReadOnlyDictionary<string, KeybindEntry> GetKeybinds();
		void SetProperty(string propertyName, object value);
		IEnumerable<MemberInfo> GetProperties();
		object GetPropertyValue(string propertyName);
		IReadOnlyList<ConfigMemberInfo> GetMemberInfos();
		void Save();
	}


	internal class ConfigMemberInfo
	{
		public MemberInfo Member;
		public string Name;
		public Type Type;
		public string Label;
		public string Section;
		public string Tooltip;
		public ConfigRangeAttribute Range;
		public bool Hidden;
		public bool IsKeybind;
	}
}
