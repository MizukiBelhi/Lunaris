using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Lunaris
{
	public class Lunaris
	{
		[DllImport("winhttp.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void DebugLog(string msg);

		[DllImport("winhttp.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void SetPatchedPath(string path);

		[DllImport("winhttp.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void OnStartCB();

		//public static bool IsErenshorStarted = false;

		public static Timer _timer;

		public static Assembly cecilAsm;
		public static byte[] harmAssBytes;

		private static Dictionary<string, Assembly> loadedAssemblies = [];


		/// <summary>
		/// Entrypoint.
		/// </summary>
		public static void Init()
		{
			Console.SetOut(new DualWriter(Console.Out, s => { try { DebugLog(s); } catch { } }));
			/*try
			{
				var fallback = new StreamWriter("LunarisManaged.log", true, Encoding.UTF8) { AutoFlush = true };
				Console.SetOut(fallback);
				Console.SetError(fallback);
			}
			catch { }*/

			string exePath = Process.GetCurrentProcess().MainModule.FileName;
			string exeDir = Path.GetDirectoryName(exePath);
			string dataDir = Path.Combine(exeDir, Path.GetFileNameWithoutExtension(exePath) + "_Data");
			string managedDir = Path.Combine(dataDir, "Managed");
			string unityAssemblyPath = Path.Combine(managedDir, "UnityEngine.CoreModule.dll");
			if (!File.Exists(unityAssemblyPath))
			{
				Console.WriteLine("Could not find UnityEngine.CoreModule.dll at: " + unityAssemblyPath);
			}

			string[] import =
			{
				"Mono.Cecil",
				"Mono.Cecil.Mdb",
				"Mono.Cecil.Pdb",
				"Mono.Cecil.Rocks",
			};

			foreach (string file in import)
			{
				var resourceName = "Lunaris." + file + ".dll";
				try
				{
					var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

					byte[] bytes = new byte[stream.Length];
					int offset = 0;
					while (offset < bytes.Length)
						offset += stream.Read(bytes, offset, bytes.Length - offset);
					var ass = Assembly.Load(bytes);
					loadedAssemblies.Add(file, ass);
					if (file == "Mono.Cecil")
						cecilAsm = ass;

					stream.Dispose();
				}
				catch
				{
					DebugLog($"Could not find Resource: {resourceName} ");
				}
			}


			AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
			{
				var name = new AssemblyName(args.Name).Name;
				if (loadedAssemblies.ContainsKey(name))
				{
					return loadedAssemblies[name];
				}
				return Unity.AssemblyResolve(sender, args);
			};

			Unity.Patch(unityAssemblyPath);

			Console.WriteLine("Lunaris Managed Initialized.");
		}

		internal static void RegisterAssemblies()
		{
			//Force load important dlls
			string[] import =
			{
				"MonoMod.Utils",
				"MonoMod.RuntimeDetour",
				"0Harmony",
				"MonoMod.Backports",
				"MonoMod.ILHelpers",
				"System.ValueTuple",
				"MonoMod.Iced",
				"System.Numerics.Vectors",
				"System.Runtime.CompilerServices.Unsafe",
				"ImGui.NET",
				"MonoMod.Core",
				"Newtonsoft.Json",
				"BepInEx"
			};

			foreach (string file in import)
			{
				var resourceName = "Lunaris." + file + ".dll";
				try
				{
					var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);

					byte[] bytes = new byte[stream.Length];
					int offset = 0;
					while (offset < bytes.Length)
						offset += stream.Read(bytes, offset, bytes.Length - offset);
					var ass = Assembly.Load(bytes);
					loadedAssemblies.Add(file, ass);
					if (file == "0Harmony")
						harmAssBytes = bytes;

					stream.Dispose();
				}
				catch
				{
					DebugLog($"Could not find Resource: {resourceName} ");
				}
			}
		}

		private static void OnStarted()
		{
			DispatcherBehaviour.RunOnMainThread(() =>
			{
				DispatcherBehaviour.CreateGameObject("Lunaris", go =>
				{
					DispatcherBehaviour.AddComponent(go, "Lunaris.Bridge");

					//Start timer for check
					_timer = new Timer(TimerCheck, null, 10, 1000);
				});
			});
		}

		private static Assembly bridgeAssembly = null;
		private static void TimerCheck(object state)
		{
			try
			{
				if (bridgeAssembly == null)
				{
					foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
					{
						if (assembly.GetName().Name == "LunarisBridge")
						{
							bridgeAssembly = assembly;
							break;
						}
					}

					LunarisSentry.Snapshot(bridgeAssembly);

				}
				else
				{
					LunarisSentry.CheckIntegrity();
				}
			} catch{ }
		}

		public static void AttachGO()
		{
			//IsErenshorStarted = true;
			OnStartCB();
		}


		private class DualWriter : TextWriter
		{
			private readonly TextWriter _base;
			private readonly Action<string> _native;
			public DualWriter(TextWriter baseWriter, Action<string> nativeWriter) { _base = baseWriter; _native = nativeWriter; }
			public override Encoding Encoding => Encoding.UTF8;
			public override void WriteLine(string value) { _base.WriteLine(value); _native?.Invoke(value); }
			public override void Write(char value) { _base.Write(value); }
		}

	}
}
