using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Win32;
using Mono.Cecil;
using Mono.Cecil.Cil;


namespace Lunaris
{

	internal class LunarisSentry
	{
		private static readonly Dictionary<string, byte[]> _ilCache = [];
		private static bool IsTampered { get; set; }
		private static Assembly ass;

		public static void Snapshot(Assembly assembly)
		{
			ass = assembly;

			foreach (Type type in assembly.GetTypes())
			{
				if (type.Namespace != null && type.Namespace.StartsWith("Lunaris"))
				{
					foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
					{
						if (method.IsAbstract || method.IsGenericMethodDefinition) continue;

						if (method.Name.Contains("MapKey") || type.FullName == "Lunaris.Bridge" || type.FullName.Contains("FontAwesomeIcon") || type.FullName.Contains("Easing") || type.FullName.Contains("Notifi") || type.FullName.Contains("IPC") || type.FullName.Contains("Lunaris.Math") || type.FullName.Contains("Lunaris.UI")) continue;
						if (method.DeclaringType != type) continue;
						if (method.IsVirtual || method.IsSpecialName) continue;

						try
						{
							var body = method.GetMethodBody();
							if (body == null) continue;

							_ilCache[$"{type.FullName}.{method.Name}"] = body.GetILAsByteArray();
						}
						catch { }
					}
				}
			}
		}

		public static void CheckIntegrity()
		{
			if (IsTampered) return;
			if (ass == null) return;

			if (!RegGetValue("TamperWarning", true)) return;

			foreach (Type type in ass.GetTypes())
			{
				if (type.Namespace != null && type.Namespace.StartsWith("Lunaris"))
				{
					foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
					{
						string key = $"{type.FullName}.{method.Name}";
						if (!_ilCache.ContainsKey(key)) continue;

						var body = method.GetMethodBody();
						if (body == null) continue;

						byte[] currentIL = body.GetILAsByteArray();
						byte[] originalIL = _ilCache[key];

						if (currentIL.Length != originalIL.Length || !CompareBytes(currentIL, originalIL))
						{
							IsTampered = true;
							DispatcherBehaviour.NotifyWarn("Lunaris has been tampered with!", TimeSpan.FromSeconds(60));
							Lunaris.DebugLog($"Lunaris has been tampered with! [{key}]");
							return;
						}
					}
				}
			}
		}

		private static bool CompareBytes(byte[] a, byte[] b)
		{
			for (int i = 0; i < a.Length; i++)
			{
				if (a[i] != b[i]) return false;
			}
			return true;
		}

		public static void RegSetValue(string name, object value, RegistryValueKind kind)
		{
			using var key = Registry.CurrentUser.CreateSubKey(@"Software\Lunaris");
			key?.SetValue(name, value, kind);
		}

		public static T RegGetValue<T>(string name, T defaultValue = default!)
		{
			using var key = Registry.CurrentUser.OpenSubKey(@"Software\Lunaris");
			var value = key?.GetValue(name);
			if (value == null)
				return defaultValue;

			if (typeof(T) == typeof(bool) && value is int i)
				return (T)(object)(i != 0);

			return (T)value;
		}
	}

	
	public class Unity
	{
		//private static object updateMonoBehaviour;
		private static string patchedPath = null;
		public static string tmpDir = "";

		public static void Patch(string unityAssemblyPath)
		{
			Console.WriteLine("Patching UnityEngine.CoreModule.dll...");
			var cecilAssembly = Lunaris.cecilAsm;

			//AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;

			tmpDir = Path.Combine(Path.GetTempPath(), "LunarisDeps");
			Directory.CreateDirectory(tmpDir);

			//load assemblies
			/*var asm = Assembly.GetExecutingAssembly();
			string[] resourceNames = asm.GetManifestResourceNames();
			foreach (var res in resourceNames)
			{
				if (res.Contains("Mono.Cecil.dll")) continue;

				using (var s = asm.GetManifestResourceStream(res))
				{
					if (s == null) continue;
					var bytes = new byte[s.Length];
					s.Read(bytes, 0, bytes.Length);

					var fileName = res.Replace("Lunaris.", "");

					var asm2 = Assembly.Load(bytes);
					Loaded[fileName] = asm2;
				}
			}*/

			try
			{
				if (cecilAssembly == null)
				{
					Lunaris.DebugLog("Cecil assembly not found, cannot patch Unity.");
					return;
				}

				string patchedAssemblyPath = Path.Combine(Path.GetTempPath(), "UnityEngine.CoreModule.Patched.dll");
				ModuleDefinition mainModule = ModuleDefinition.ReadModule(unityAssemblyPath);

				TypeDefinition entryType = null;
				foreach (var t in mainModule.Types)
					if (t.Name == "Application") { entryType = t; break; }
				if (entryType == null)
				{
					Lunaris.DebugLog("UnityEngine.Application not found");
					return;
				}
				MethodDefinition cctor = null;
				foreach (var m in entryType.Methods)
				{
					if (m.IsConstructor && m.IsStatic)
					{
						cctor = m;
						break;
					}
				}
				if (cctor == null)
				{
					TypeSystem ts = mainModule.TypeSystem;
					cctor = new MethodDefinition(".cctor", Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName | Mono.Cecil.MethodAttributes.Private, ts.Void);
					entryType.Methods.Add(cctor);
					ILProcessor cctorIL = cctor.Body.GetILProcessor();
					cctor.Body.InitLocals = true;
					cctorIL.Append(cctorIL.Create(OpCodes.Ret));
				}

				var lunarisModule = ModuleDefinition.ReadModule(typeof(Unity).Assembly.Location);
				var unityType = lunarisModule.Types.First(t => t.FullName == "Lunaris.Unity");
				var initMethod = unityType.Methods.First(m => m.Name == "Init");
				var initRef = mainModule.ImportReference(initMethod);

				ILProcessor cctorILProcessor = cctor.Body.GetILProcessor();
				cctorILProcessor.InsertBefore(cctor.Body.Instructions[0], cctorILProcessor.Create(OpCodes.Call, initRef));
				cctorILProcessor.InsertBefore(cctor.Body.Instructions[0], cctorILProcessor.Create(OpCodes.Nop));

				mainModule.Write(patchedAssemblyPath);

				Lunaris.SetPatchedPath(patchedAssemblyPath);
				patchedPath = patchedAssemblyPath;
			}
			catch (Exception ex)
			{
				Lunaris.DebugLog("Exception during patching: " + ex);
			}
		}
		private static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.OrdinalIgnoreCase);

		public static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
		{
			try
			{
				var requestedName = new AssemblyName(args.Name).Name;
				Lunaris.DebugLog($"EmbeddedAssemblyLoader trying to resolve {requestedName} \n {args.Name}");
				var already = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a =>
				{
					try { return string.Equals(a.GetName().Name, requestedName, StringComparison.OrdinalIgnoreCase); }
					catch { return false; }
				});
				if (already != null)
				{
					//Lunaris.DebugLog($"ALREADY LOADED {requestedName}");
					return already;
				}

				//lock (locker)
				{
					if (Loaded.TryGetValue(requestedName, out var cached)) return cached;

					var exec = Assembly.GetExecutingAssembly();
					var resourceName = exec.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith(requestedName + ".dll", StringComparison.OrdinalIgnoreCase));

					if (resourceName == null)
					{
						//try embed
						resourceName = exec.GetManifestResourceNames().FirstOrDefault(r => r.EndsWith("Lunaris."+requestedName + ".dll", StringComparison.OrdinalIgnoreCase));
						if (resourceName == null)
						{
							Lunaris.DebugLog($"EmbeddedAssemblyLoader failed to resolve {requestedName} \n {args.Name}");
							return null;
						}
					}

					Lunaris.DebugLog($"EmbeddedAssemblyLoader failed to resolve {requestedName} \n {args.Name}");
					return null;
					/*using (var s = exec.GetManifestResourceStream(resourceName))
					{
						if (s == null) return null;
						var bytes = new byte[s.Length];
						s.Read(bytes, 0, bytes.Length);

						var asm = Assembly.Load(bytes);
						Lunaris.DebugLog($"Loaded {requestedName} @ bytes");
						Loaded[requestedName] = asm;
						return asm;
					}*/
				}
			}
			catch (Exception ex)
			{
				try { Lunaris.DebugLog($"EmbeddedAssemblyLoader failed to resolve {args.Name}: {ex}"); } catch { }
				return null;
			}

			
		}

		private static bool _started = false;
		public static void OnStartedDelayed()
		{
			if(_started) return;
			_started = true;

			Lunaris.RegisterAssemblies();
			/*if (updateMonoBehaviour != null)
			{
				Lunaris.DebugLog("Init called, but updateMonoBehaviour already exists. Skipping.");
				return;
			}*/

			Lunaris.DebugLog("Initializing Lunaris dispatcher...");

			Assembly unityAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "UnityEngine.CoreModule" || a.GetName().Name.StartsWith("UnityEngine"));

			if (unityAssembly == null)
			{
				try { unityAssembly = Assembly.LoadFrom(patchedPath); }
				catch (Exception ex) { Lunaris.DebugLog($"Failed to load UnityEngine: {ex}"); return; }
			}

			Type goType = unityAssembly.GetType("UnityEngine.GameObject");
			Type unityObjectType = unityAssembly.GetType("UnityEngine.Object");

			if (goType == null || unityObjectType == null)
			{
				Lunaris.DebugLog("Failed to find core Unity types from loaded assembly.");
				return;
			}

			Type unityMonoType = unityAssembly.GetType("UnityEngine.MonoBehaviour");
			if (unityMonoType == null)
			{
				Lunaris.DebugLog("Failed to get UnityEngine.MonoBehaviour from unityAssembly.");
				return;
			}

			try
			{
				var asmName = new System.Reflection.AssemblyName("Lunaris.DynamicProxy");
				var asmBuilder = System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(asmName, System.Reflection.Emit.AssemblyBuilderAccess.Run);
				var modBuilder = asmBuilder.DefineDynamicModule("Lunaris.DynamicProxy.Module");
				var tb = modBuilder.DefineType("Lunaris.Unity.DispatcherProxy", System.Reflection.TypeAttributes.Public | System.Reflection.TypeAttributes.Class,
					unityMonoType);

				// public void Update()
				var meth = tb.DefineMethod("Update", System.Reflection.MethodAttributes.Public | System.Reflection.MethodAttributes.HideBySig, typeof(void), Type.EmptyTypes);

				// call Lunaris.DispatcherBridge.Tick();
				var il = meth.GetILGenerator();
				var tickMethod = typeof(DispatcherBehaviour).GetMethod("Tick", BindingFlags.Public | BindingFlags.Static);
				if (tickMethod == null)
				{
					Lunaris.DebugLog("Failed to find DispatcherBridge.Tick method.");
					return;
				}
				il.Emit(System.Reflection.Emit.OpCodes.Call, tickMethod);
				il.Emit(System.Reflection.Emit.OpCodes.Ret);

				Type proxyType = tb.CreateType();
				// add go
				object go = Activator.CreateInstance(goType, ["Lunaris_Internal"]);
				var hideFlagsProp = goType.GetProperty("hideFlags");
				hideFlagsProp.SetValue(go, 61, null);

				var dontDestroy = goType.Assembly.GetType("UnityEngine.Object").GetMethod("DontDestroyOnLoad", [goType.Assembly.GetType("UnityEngine.Object")]);
				dontDestroy.Invoke(null, [go]);
				MethodInfo addComp = goType.GetMethod("AddComponent", [typeof(Type)]);
				object comp = addComp.Invoke(go, [proxyType]);


				if (comp != null)
				{
					var compFlags = comp.GetType().GetProperty("hideFlags");
					compFlags.SetValue(comp, 61, null);

					Lunaris.AttachGO();
					DispatcherBehaviour.BringHarm(Lunaris.harmAssBytes);
				}
				else
					Lunaris.DebugLog("AddComponent returned null for Dispatcher.");
			}
			catch (Exception ex)
			{
				Lunaris.DebugLog($"Exception while attaching Dispatcher: {ex}");
			}
		}


		public static void Init()
		{
			Assembly unityAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "UnityEngine.CoreModule" || a.GetName().Name.StartsWith("UnityEngine"));

			if (unityAssembly == null)
			{
				Lunaris.DebugLog($"Failed to find unityAssembly, loading it.");
				try { unityAssembly = Assembly.LoadFrom(patchedPath); }
				catch (Exception ex) { Lunaris.DebugLog($"Failed to load UnityEngine: {ex}"); return; }
			}

			Type goType = unityAssembly.GetType("UnityEngine.GameObject");
			Type unityObjectType = unityAssembly.GetType("UnityEngine.Object");

			if (goType == null || unityObjectType == null)
			{
				Lunaris.DebugLog("Failed to find core Unity types from loaded assembly.");
				return;
			}

			try
			{
				OnStartedDelayed();
			}
			catch (Exception ex)
			{
				Lunaris.DebugLog("Reflection Hook Error: " + ex);
			}
		}
	}
}
