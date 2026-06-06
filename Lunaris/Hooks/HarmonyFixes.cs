using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	/// <summary>
	/// This fixes compatibility issues when a plugin uses an older harmony version.
	/// Yes it's silly to hook harmony with harmony, but we're doing it anyway.
	/// </summary>
	internal static class HarmonyFixes
	{
		public class CecilSymbolResolver : DefaultAssemblyResolver
		{
			public readonly AssemblyDefinition _harmonyDef;

			public CecilSymbolResolver(AssemblyDefinition harmonyDef)
			{
				_harmonyDef = harmonyDef;
			}

			public override AssemblyDefinition Resolve(AssemblyNameReference name)
			{
				if (name.Name == "0Harmony")
					return _harmonyDef;

				return base.Resolve(name);
			}
		}
		public static CecilSymbolResolver resolver;

		public static void LoadResolver(byte[] bytes)
		{
			using MemoryStream ms = new(bytes);
			resolver = new(AssemblyDefinition.ReadAssembly(ms));
		}

	}

	[EditorBrowsable(EditorBrowsableState.Never)]
	[HarmonyPatch(typeof(HarmonyMethodExtensions))]
	[HarmonyPatch("GetFromType")]
	[HarmonyPatch([typeof(Type)])]
	class GetFromTypePatch
	{
		static bool Prefix(Type type, ref List<HarmonyMethod> __result)
		{
			var methods = new List<HarmonyMethod>();
			foreach (var tt in type.Assembly.GetTypes())
			{
				if (tt.FullName != type.FullName) continue;
				var attrs = tt.GetCustomAttributes<HarmonyAttribute>(inherit: true);
				foreach (var attr in attrs)
				{
					if (attr != null)
					{
						var f_info = attr.GetType().GetField(nameof(HarmonyAttribute.info), AccessTools.all);
						if (f_info is null) continue;
						var info = f_info.GetValue(attr);
						methods.Add(AccessTools.MakeDeepCopy<HarmonyMethod>(info));
					}
				}
			}
			__result = methods;
			return false;
		}
	}
}
