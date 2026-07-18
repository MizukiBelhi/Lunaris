using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	internal static class PluginPermissions
	{
		private const LunarisPermission PluginTypeFlags = LunarisPermission.BepinPlugin | LunarisPermission.LunarisPlugin;

		//We just do a very basic check across the assembly
		internal static LunarisPermission GetUsedPermissions(AssemblyDefinition def)
		{
			LunarisPermission used = LunarisPermission.None;

			var referenced = def.MainModule.AssemblyReferences;
			if (referenced.Any(a => a.Name == "0Harmony" || a.Name == "HarmonyLib" || a.Name == "HarmonyX"))
				used |= LunarisPermission.Harmony;
			if (referenced.Any(a => a.Name.Contains("BepIn")))
				used |= LunarisPermission.BepinPlugin;
			if (referenced.Any(a => a.Name.Contains("Lunaris")))
				used |= LunarisPermission.LunarisPlugin;
			if (referenced.Any(a => a.Name.Contains("Mono.Cecil")))
				used |= LunarisPermission.Harmony;

			foreach (var type in def.MainModule.Types)
			{
				foreach (var method in type.Methods)
				{
					if (!method.HasBody) continue;

					foreach (var instruction in method.Body.Instructions)
					{
						var op = instruction.OpCode;

						if (op == Mono.Cecil.Cil.OpCodes.Call || op == Mono.Cecil.Cil.OpCodes.Callvirt || op == Mono.Cecil.Cil.OpCodes.Newobj)
						{
							if (instruction.Operand is MethodReference target)
							{
								var dt = target.DeclaringType;
								if (dt == null) continue;

								var ns = dt.Namespace ?? "";

								if (ns.StartsWith("System.IO"))
									used |= LunarisPermission.FileAccess;
								else if (ns.StartsWith("System.Net"))
									used |= LunarisPermission.Network;
								else if (ns.StartsWith("System.Reflection.Emit"))
									used |= LunarisPermission.Unsafe;
								else if (ns.StartsWith("System.Reflection"))
									used |= LunarisPermission.Reflection;
								else if (dt.Name.Contains("Harmony") || dt.Name.Contains("Cecil"))
									used |= LunarisPermission.Harmony;
							}
						}
					}
				}
			}

			return used;
		}

		internal static bool ArePermissionsDeclared(LunarisPermission declared, LunarisPermission used)
		{
			var usedPermissions = used & ~PluginTypeFlags;
			return declared.HasFlag(usedPermissions);
		}
	}
}
