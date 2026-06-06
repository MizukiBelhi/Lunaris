using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class LunarisPluginAttribute(string name, string version, string author = null, string description = null) : Attribute
	{
		public string Name { get; } = name;
		public string Version { get; } = version;
		public string Author { get; } = author;
		public string Description { get; } = description;
	}
}
