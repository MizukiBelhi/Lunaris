using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using UnityEngine;

namespace Lunaris
{
	internal class Hooks
	{
		private static Harmony harmony;
		public static void Init()
		{
			try
			{

				harmony = new Harmony("Lunaris.Patches");
				harmony.PatchAll(Assembly.GetExecutingAssembly());
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}
		}

		public static void Dispose()
		{
			if (harmony == null) return;
			harmony.UnpatchSelf();
		}
	}

}

