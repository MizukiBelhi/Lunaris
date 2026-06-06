using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace Lunaris
{

	[EditorBrowsable(EditorBrowsableState.Never)]
	[HarmonyPatch(typeof(TypeText), "CheckCommands")]
	public class TypeTextHooks
	{
		static bool Prefix(TypeText __instance)
		{
			string txt = __instance.typed.text;

			if (string.IsNullOrEmpty(txt)) return true;

			return Commands.RunCommand(txt);
		}
	}
}
