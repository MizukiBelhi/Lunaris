using System;

namespace Lunaris.Config
{
	/// <summary>
	/// Display string shown in the config UI.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public sealed class ConfigLabelAttribute(string label) : Attribute
	{
		public string Label { get; } = label;
	}

	/// <summary>
	/// Description shown in the config UI.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public sealed class ConfigDescriptionAttribute(string description) : Attribute
	{
		public string Description { get; } = description;
	}

	/// <summary>
	/// Clamps a numeric property to [Min, Max].
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public sealed class ConfigRangeAttribute(float min, float max) : Attribute
	{
		public float Min { get; } = min; public float Max { get; } = max;
	}

	/// <summary>
	/// Marks a property as hidden in the config UI.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public sealed class ConfigHiddenAttribute : Attribute { }

	/// <summary>
	/// Groups properties in the config UI.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public sealed class ConfigSectionAttribute(string section) : Attribute
	{
		public string Section { get; } = section;
	}


	/// <summary>
	/// Combines other Attributes for ease of use.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
	public sealed class ConfigAttribute(string label, string section = null, string tooltip = null) : Attribute
	{
		public string Label { get; } = label;
		public string Section { get; } = section;
		public string Tooltip { get; } = tooltip;
	}
}