using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	[Flags]
	public enum LunarisPermission
	{
		None = 0,
		FileAccess = 1 << 0,
		Network = 1 << 1,
		Reflection = 1 << 2,
		Harmony = 1 << 3,
		BepinPlugin = 1 << 4,
		LunarisPlugin = 1 << 5,
		Unsafe = 1 << 30,
		All = ~0
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class LunarisPermissionAttribute(LunarisPermission permissions) : Attribute
	{
		public LunarisPermission Permissions { get; } = permissions;
	}
}
