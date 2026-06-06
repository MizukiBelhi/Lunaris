using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine;
using BepInEx;
using System.ComponentModel;
using Lunaris.Config;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace Lunaris
{
	/// <summary>
	/// DO NOT USE.
	/// Forwards all BepInEx calls to Lunaris.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class BepinWrapper
	{
		private static ConcurrentDictionary<string, IConfig> _configs = [];
		private static ConcurrentDictionary<string, BepInEx.Configuration.KeyboardShortcut> _bpks = [];
		private static readonly Dictionary<object, string> _assemblyNameCache = [];

		public static object Wrapper(Type classType, object instance, string methodName, object[] args, Type returnType)
		{

			if(classType.Name == "ConfigFile")
			{
				if(methodName == "Bind" && args.Length == 4)
				{
					Type tType = returnType.GetGenericArguments()[0];
					var method = typeof(BepinWrapper).GetMethod("BepinConfigBind", BindingFlags.Public | BindingFlags.Static);
					var genericMethod = method.MakeGenericMethod(tType);
					return genericMethod.Invoke(null, [instance, args]);
				}
				if(methodName == "ContainsKey" && args.Length == 1)
				{
					var pln = GetPluginAssemblyName(instance);
					if (_configs.TryGetValue(pln, out var cfg))
					{
						if (args[0] is BepInEx.Configuration.ConfigDefinition)
						{
							var def = args[0] as BepInEx.Configuration.ConfigDefinition;

							if(cfg.GetSettings().ContainsKey(def.Key))
							{
								return true;
							}
							return false;
						}
					}
					return false;
				}
				if (methodName == "Remove")
				{
					var pln = GetPluginAssemblyName(instance);
					if (_configs.TryGetValue(pln, out var cfg))
					{
						if (args[0] is BepInEx.Configuration.ConfigDefinition)
						{
							var def = args[0] as BepInEx.Configuration.ConfigDefinition;
							((ConfigInstance)cfg).Remove(def.Key);
							return true;
						}
					}
					return false;
				}
				if (methodName == "Save")
				{
					var pln = GetPluginAssemblyName(instance);
					if (_configs.TryGetValue(pln, out var cfg))
					{
						cfg.Save();
					}
					return null;
				}
			}
			if(classType.Name == "ConfigDefinition")
			{
				if(methodName == "ToString")
				{
					return ((BepInEx.Configuration.ConfigDefinition)instance).Section + "." + ((BepInEx.Configuration.ConfigDefinition)instance).Key;
				}
			}
			if (classType.Name == "ConfigEntry`1")
			{
				var pln = GetPluginAssemblyName(instance);
				if (_configs.TryGetValue(pln, out var cfg))
				{

					var ins = ((BepInEx.Configuration.ConfigEntryBase)instance);
					var key = ins?.Definition?.Key;

					if (key == null)
					{
						Bridge.Logger.LogWarning($"Key not found for CFG '{key}' '{ins}' '{ins?.Definition}' '{ins?.Definition?.Key}' '{pln}'");
						return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
					}

					var tType = instance.GetType().GetGenericArguments()[0];

					if (methodName == "get_Value")
					{
						//makes some mods happy, overengineered to keep gc low, just in case
						if(returnType == typeof(BepInEx.Configuration.KeyboardShortcut))
						{
							var kb = ((ConfigInstance)cfg).RegisterKeybind(key, [], "");

							var mainKey = kb.Keys[0];
							var mods = kb.Keys.Where(k => k != mainKey).ToArray();

							if (_bpks.TryGetValue(pln + key, out BepInEx.Configuration.KeyboardShortcut sk))
							{
								if (sk.MainKey != mainKey || sk.Modifiers != mods)
								{
									sk = new BepInEx.Configuration.KeyboardShortcut(mainKey, mods);
									_bpks[pln + key] = sk;
								}
							}
							else
							{
								sk = new BepInEx.Configuration.KeyboardShortcut(mainKey, mods);
								_bpks[pln + key] = sk;
							}

							return sk;
						}
						return ((ConfigInstance)cfg).Read(key, returnType, returnType.IsValueType ? Activator.CreateInstance(returnType) : null);
					}
					else if (methodName == "set_Value")
					{
						cfg.Write(key, args[0]);
						return null;
					}
				}
			}
			if (classType.Name == "ManualLogSource")
			{
				if(methodName == "LogInfo" && args.Length == 1)
				{
					Bridge.Logger.LogInfo(args[0].ToString());
					return null;
				}
				if (methodName == "LogMessage" && args.Length == 1)
				{
					Bridge.Logger.Log(args[0].ToString());
					return null;
				}
				if (methodName == "LogDebug" && args.Length == 1)
				{
					Bridge.Logger.LogDebug(args[0].ToString());
					return null;
				}
				if (methodName == "LogWarning" && args.Length == 1)
				{
					Bridge.Logger.LogWarning(args[0].ToString());
					return null;
				}
				if (methodName == "LogError" && args.Length == 1)
				{
					Bridge.Logger.LogError(args[0].ToString());
					return null;
				}
			}
			if(classType.Name == "KeyboardShortcut")
			{
				var keys = (KeyCode[])args[0];
				if (methodName == "SanitizeKeys")
				{
					if (keys == null || keys.Length == 0)
						return new KeyCode[1];
					return new KeyCode[]{ keys[0] }.Concat(from x in keys.Skip(1).Distinct() where x != keys[0] orderby x select x).ToArray();
				}
			}

			if(classType.Name == "Logger")
			{
				if(methodName == "CreateLogSource" && args.Length == 1)
					return new BepInEx.Logging.ManualLogSource("fake");
			}

			Bridge.Logger.LogError($"BepinWrapper Unhandled: type: '{classType.Name}' method: '{methodName}' argC: '{args.Length}' ret: '{returnType}'");

			if (returnType == typeof(void)) return null;
			return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
		}

		public static object WrapperStr(Type classType, TypedReference inst, string methodName, object[] args, Type returnType)
		{
			var type = TypedReference.GetTargetType(inst);
			var instance = TypedReference.ToObject(inst);

			if (classType.Name == "KeyboardShortcut")
			{
				if (methodName == "IsDown")
				{
					return false;
				}
				if(methodName == "ToString")
				{
					return "NULL";
				}
			}

			Bridge.Logger.LogError($"BepinStrWrapper Unhandled: type: '{classType.Name}' method: '{methodName}' argC: '{args.Length}' ret: '{returnType}'");

			if (returnType == typeof(void)) return null;
			return returnType.IsValueType ? Activator.CreateInstance(returnType) : null;
		}

		public static void CtorWrapper(object instance, Type classType, object[] args)
		{
			

			if (classType.Name == "BaseUnityPlugin")
			{
				var ass = instance.GetType().Assembly;
				BaseUnityPluginCtor(instance, ass);
				return;
			}
			if(classType.Name == "BepInPlugin")
			{
				//var bpInst = (BepInPlugin)instance;
				SetField(instance, "GUID", (string)args[0]);
				SetField(instance, "Name", (string)args[1]);
				Version ver = null;
				try
				{
					ver = new Version((string)args[2]);
				}
				catch {}
				SetField(instance, "Version", ver);
				return;
			}
			if(classType.Name == "ConfigDefinition")
			{
				SetBackingFieldDirect(instance, "Key", (string)args[1]);
				SetBackingFieldDirect(instance, "Section", (string)args[0]);
				return;
			}
			if(classType.Name == "ConfigEntry`1")
			{
				//var configFile = args[0];
				var definition = args[1];
				//var defaultValue = args[2];
				var description = args[3];

				//var settingType = classType.GetGenericArguments()[0];

				//SetField(instance, "ConfigFile", configFile);
				SetBackingField(instance, "Definition", definition);
				//SetField(instance, "SettingType", settingType);
				SetBackingField(instance, "Description", description);
				//SetField(instance, "DefaultValue", defaultValue);
				//SetField(instance, "BoxedValue", defaultValue);

				return;
			}
			if(classType.Name == "ConfigDescription")
			{
				SetBackingFieldDirect(instance, "Description", (string)args[0]);
				SetBackingFieldDirect(instance, "AcceptableValues", args[1]);
				SetBackingFieldDirect(instance, "Tags", args[2]);
				return;
			}
			if (classType.Name == "AcceptableValueRange`1")
			{
				SetBackingFieldDirect(instance, "MinValue", args[0]);
				SetBackingFieldDirect(instance, "MaxValue", args[1]);
				return;
			}
			if (classType.Name == "AcceptableValueList`1")
			{
				SetBackingFieldDirect(instance, "AcceptableValues", args[0]);
				return;
			}
			if (classType.Name == "AcceptableValueBase")
			{
				SetBackingField(instance, "ValueType", args[0]);
				return;
			}

			//Bridge.Logger.LogError($"BepinWrapper .cctor: '{classType.Name}' argC: '{args.Length}'");
		}



		//For structs
		public static void CtorWrapperStr(TypedReference instance, Type type, object[] args)
		{

		}

		public static void OnProxyException(Exception ex)
		{
			if (ex.InnerException == null)
				Bridge.Logger.LogError($"BepinWrapper Error: {ex}");
			else
				Bridge.Logger.LogError($"BepinWrapper Error {ex} {ex.InnerException}");
		}


		//
		// ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
		// ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, ConfigDescription configDescription = null)
		//
		public static BepInEx.Configuration.ConfigEntry<T> BepinConfigBind<T>(object instance, object[] args)
		{
			var cf = (BepInEx.Configuration.ConfigFile)instance;

			//DebugArgs(args, instance);

			var configType = typeof(BepInEx.Configuration.ConfigEntry<T>);
			var ctor = configType.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).FirstOrDefault(c => c.GetParameters().Length == 4);

			var cd = new BepInEx.Configuration.ConfigDefinition((string)args[0], (string)args[1]);

			BepInEx.Configuration.ConfigDescription cdesc = null;

			//check if this already is a desc
			if (args[3] != null)
			{
				if (args[3] is string s)
					cdesc = new BepInEx.Configuration.ConfigDescription(s);
				else
					cdesc = (BepInEx.Configuration.ConfigDescription)args[3];
			}


			//Grab cfg of ass
			var ass = GetPluginAssembly();
			var spln = ass.FullName.Replace(" ", "").ToLower();
			if (!_configs.TryGetValue(spln, out var cfg))
			{
				var attr = ass.GetTypes().SelectMany(t => t.GetCustomAttributes(false)).FirstOrDefault(a => a.GetType().FullName == "BepInEx.BepInPlugin");
				if (attr == null)
				{
					Bridge.Logger.LogWarning($"No BepInPlugin attribute found on {ass.FullName}.");
				}
				else
				{
					var setName = ((BepInPlugin)attr).Name;
					var sanitizedName = setName.Replace(" ", "").ToLower();
					cfg = new ConfigInstance(sanitizedName);
					_configs.TryAdd(spln, cfg);
				}
			}

			if (typeof(T) == typeof(BepInEx.Configuration.KeyboardShortcut))
			{
				var shortcut = args[2] != null ? (BepInEx.Configuration.KeyboardShortcut)args[2] : BepInEx.Configuration.KeyboardShortcut.Empty;

				var shortcutObj = args[2];
				var mainKey = (KeyCode)(int)shortcutObj.GetType().GetProperty("MainKey").GetValue(shortcutObj);
				var modifiers = ((System.Collections.IEnumerable)shortcutObj.GetType().GetProperty("Modifiers").GetValue(shortcutObj)).Cast<object>().Select(m => (KeyCode)(int)m);

				var keys = mainKey == KeyCode.None ? [] : new[] { mainKey }.Concat(modifiers).ToArray();

				//cfg.Write((string)args[1], string.Join(",", keys.Select(k => ((int)k).ToString())));
				((ConfigInstance)cfg).RegisterKeybind((string)args[1], keys, cdesc?.Description, cd?.Section);
			}
			else
			{
				//Get range
				var range = cdesc?.AcceptableValues;
				if (range != null && range.GetType().Name == "AcceptableValueRange`1")
				{
					var rangeType = range.GetType();
					var min = (float)Convert.ChangeType(rangeType.GetProperty("MinValue").GetValue(range), typeof(float));
					var max = (float)Convert.ChangeType(rangeType.GetProperty("MaxValue").GetValue(range), typeof(float));
					cfg.SetRange((string)args[1], min, max);
				}

				((ConfigInstance)cfg).WriteBep((string)args[1], (T)args[2]);
				cfg.SetDesc((string)args[1], cdesc?.Description);
				cfg.SetSection((string)args[1], cd?.Section);
			}
			

			var configEntry = (BepInEx.Configuration.ConfigEntry<T>)ctor.Invoke([cf, cd, args[2], cdesc]);

			return configEntry;
		}

		public static void DebugArgs(object[] args, object instance)
		{
			Bridge.Logger.Log($"=== DEBUGGING BEPIN ARGS ===");
			Bridge.Logger.Log($"Instance Type: {instance?.GetType().FullName ?? "NULL"}");
			Bridge.Logger.Log($"Args Length: {args.Length}");

			for (int i = 0; i < args.Length; i++)
			{
				object val = args[i];
				string typeName = val?.GetType().FullName ?? "NULL";
				string valueStr = val?.ToString() ?? "null";

				Bridge.Logger.Log($"Arg[{i}] | Type: {typeName} | Value: {valueStr}");
			}
			Bridge.Logger.Log($"===========================");
		}


		public static void BaseUnityPluginCtor(object instance, Assembly pluginAss)
		{
			if (string.IsNullOrEmpty(Paths.PluginPath) || Paths.PluginPath != PluginLoader.pluginPath)
			{
				var prop = typeof(Paths).GetProperty("PluginPath", BindingFlags.Public | BindingFlags.Static);

				if (prop != null)
				{
					var currentValue = (string)prop.GetValue(null);
					if (string.IsNullOrEmpty(currentValue))
					{
						var setMethod = prop.GetSetMethod(nonPublic: true);
						if (setMethod != null)
							setMethod.Invoke(null, [PluginLoader.pluginPath]);
						else
							UnityEngine.Debug.LogWarning("Setter for PluginPath not found!");
					}
				}
				else
					UnityEngine.Debug.LogWarning("Property 'PluginPath' not found!");

				prop = typeof(Paths).GetProperty("ConfigPath", BindingFlags.Public | BindingFlags.Static);

				if (prop != null)
				{
					var currentValue = (string)prop.GetValue(null);
					if (string.IsNullOrEmpty(currentValue))
					{
						var setMethod = prop.GetSetMethod(nonPublic: true);
						if (setMethod != null)
							setMethod.Invoke(null, [PluginLoader.configPath]);
						else
							UnityEngine.Debug.LogWarning("Setter for ConfigPath not found!");
					}
				}
			}

			var cfg = new BepInEx.Configuration.ConfigFile("fake.cfg", false);
			var log = new BepInEx.Logging.ManualLogSource("fake.cfg");


			//var ass = GetPluginAssembly();

			/*if (!_configs.TryGetValue(ass.FullName.Replace(" ", "").ToLower(), out _))
			{

				var attr = ass.GetTypes().SelectMany(t => t.GetCustomAttributes(false)).FirstOrDefault(a => a.GetType().FullName == "BepInEx.BepInPlugin");
				if (attr == null)
				{
					Bridge.Logger.LogWarning($"No BepInPlugin attribute found on {ass.FullName}.");
					return;
				}
				var setName = ((BepInPlugin)attr).Name;
				var sanitizedName = setName.Replace(" ", "").ToLower();
				var lunaCfg = new ConfigInstance(sanitizedName);
				_configs.TryAdd(ass.FullName.Replace(" ", "").ToLower(), lunaCfg);

			}*/

			var plInfo = new BepInEx.PluginInfo();

			PopulatePluginInfo(plInfo, null, [], [], [], "LunarisWrapper", new Version(99, 99), PluginLoader.pluginPath);

			SetBackingField(instance, "Config", cfg);
			SetBackingField(instance, "Logger", log);
			SetBackingField(instance, "Info", plInfo);
		}

		public static void PopulatePluginInfo(object plInfo, BepInPlugin metadata, IEnumerable<BepInProcess> processes, IEnumerable<BepInDependency> dependencies, IEnumerable<BepInIncompatibility> incompatibilities, string typeName, Version bepInExVersion, string location)
		{
			var type = plInfo.GetType();
			var values = new Dictionary<string, object>
			{
				{ "Metadata", metadata },
				{ "Processes", processes },
				{ "Dependencies", dependencies },
				{ "Incompatibilities", incompatibilities },
				{ "TypeName", typeName },
				{ "TargettedBepInExVersion", bepInExVersion },
				{ "Location", location }
			};

			foreach (var kvp in values)
			{
				var prop = type.GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (prop != null && prop.CanWrite)
				{
					prop.SetValue(plInfo, kvp.Value);
				}
				else
				{
					var field = type.GetField($"<{kvp.Key}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
					field?.SetValue(plInfo, kvp.Value);
				}
			}
		}

		private static void SetBackingField(object instance, string propertyName, object value)
		{
			if (instance == null) return;

			var field = instance.GetType().BaseType.GetField($"<{propertyName}>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

			if (field != null)
				field.SetValue(instance, value);
			else
				Bridge.Logger.Log($"Could not find backing field for {propertyName} @ {instance.GetType()}");
		}

		private static void SetBackingFieldDirect(object instance, string propertyName, object value)
		{
			if (instance == null) return;

			var field = instance.GetType().GetField($"<{propertyName}>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

			if (field != null)
				field.SetValue(instance, value);
			else
				Bridge.Logger.Log($"Could not find direct backing field for {propertyName} @ {instance.GetType()}");
		}

		private static void SetField(object instance, string propertyName, object value)
		{
			if (instance == null) return;

			var prop = instance.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (prop != null && prop.CanWrite)
			{
				prop.SetValue(instance, value);
				return;
			}

			var fld = instance.GetType().GetField(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (fld != null)
			{
				fld.SetValue(instance, value);
				return;
			}

			Bridge.Logger.Log($"Could not find field or prop for {propertyName} on {instance}");
		}

		private static Assembly GetPluginAssembly()
		{
			var frames = new StackTrace(3, false).GetFrames();
			if (frames == null) return null;

			foreach (var frame in frames)
			{
				var method = frame.GetMethod();
				var assembly = method?.DeclaringType?.Assembly;
				if (assembly == null) continue;

				var name = assembly.FullName;
				if (name.StartsWith("System") ||
					name.StartsWith("mscorlib") ||
					name.StartsWith("UnityEngine") ||
					name.StartsWith("BepInEx") ||
					name.StartsWith("Lunaris") ||
					name.StartsWith("Mono."))
					continue;

				return assembly;
			}
			return null;
		}

		private static string GetPluginAssemblyName(object instance)
		{
			if (!_assemblyNameCache.TryGetValue(instance, out var name))
			{
				var ass = GetPluginAssembly();
				if (ass == null) return "unknown";
				var n = ass.FullName.Replace(" ", "").ToLower();
				_assemblyNameCache.Add(instance, n);
				return n;
			}
			else
				return name;
		}

		/// <summary>
		/// Overwrites most methods of originalPath to call Wrapper, saves modified file to outputPath
		/// </summary>
		/// <param name="originalPath"></param>
		/// <param name="outputPath"></param>
		public static void ProxyBepin(string originalPath, string outputPath)
		{
			var assembly = AssemblyDefinition.ReadAssembly(originalPath);
			var module = assembly.MainModule;

			var wrapperMethod = module.ImportReference(typeof(BepinWrapper).GetMethod("Wrapper"));
			var ctorWrapperMethod = module.ImportReference(typeof(BepinWrapper).GetMethod("CtorWrapper"));
			var exceptionMethod = module.ImportReference(typeof(BepinWrapper).GetMethod("OnProxyException"));
			var ctorWrapperMethStr = module.ImportReference(typeof(BepinWrapper).GetMethod("CtorWrapperStr"));
			var wrapperMethStr = module.ImportReference(typeof(BepinWrapper).GetMethod("WrapperStr"));

			foreach (var type in module.GetTypes())
			{
				if (type.IsInterface || type.Name == "<Module>") continue;
				if (type.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute")) continue;

				foreach (var method in type.Methods)
				{
					if (!method.HasBody) continue;// || method.IsConstructor || method.IsAbstract) continue;
					if (method.IsConstructor)
					{
						
						if (method.IsStatic) continue;
						if (method.DeclaringType.BaseType.IsGenericInstance) continue;
						TransformConstructor(method, ctorWrapperMethod, ctorWrapperMethStr);
					}
					else
					{
						if (method.IsSpecialName)
						{
							if (type.Name.StartsWith("ConfigEntry") && (method.Name == "get_Value" || method.Name == "set_Value"))
								TransformGenericPropertyToProxy(method, wrapperMethod, exceptionMethod);
							continue;
						}
						TransformToProxy(method, wrapperMethod, wrapperMethStr, exceptionMethod);
					}
				}
			}

			assembly.Write(outputPath);
		}

		private static void TransformConstructor(MethodDefinition method, MethodReference bridgeCtorWrapper, MethodReference bridgeCtorWrapperStr)
		{
			if (method.DeclaringType.FullName.Contains("KeyboardShortcut")) return;

			//Bridge.Logger.LogInfo($"Transforming constructor: {method.DeclaringType.FullName}.{method.Name}");

			var il = method.Body.GetILProcessor();
			method.Body.Instructions.Clear();
			method.Body.Variables.Clear();
			method.Body.ExceptionHandlers.Clear();
			method.Body.InitLocals = true;

			var baseType = method.DeclaringType.BaseType;
			MethodDefinition baseCtorMethod = null;
			var resolvedBase = baseType?.Resolve();

			if (resolvedBase != null)
			{
				baseCtorMethod = resolvedBase.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic && !m.HasParameters);
			}
			MethodReference baseCtor;
			if (baseCtorMethod != null)
			{
				baseCtor = method.Module.ImportReference(baseCtorMethod);
			}
			else
			{
				var objectType = method.Module.ImportReference(typeof(object));
				baseCtor = method.Module.ImportReference(typeof(object).GetConstructor(Type.EmptyTypes));
			}

			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Call, baseCtor);

			var argsVar = new VariableDefinition(method.Module.ImportReference(typeof(object[])));
			method.Body.Variables.Add(argsVar);


			il.Emit(OpCodes.Ldc_I4, method.Parameters.Count);
			il.Emit(OpCodes.Newarr, method.Module.ImportReference(typeof(object)));
			il.Emit(OpCodes.Stloc, argsVar);

			for (int i = 0; i < method.Parameters.Count; i++)
			{
				il.Emit(OpCodes.Ldloc, argsVar);
				il.Emit(OpCodes.Ldc_I4, i);
				il.Emit(OpCodes.Ldarg, i + 1);
				//if (method.Parameters[i].ParameterType.IsValueType || method.Parameters[i].ParameterType.IsGenericParameter)
					il.Emit(OpCodes.Box, method.Parameters[i].ParameterType);
				il.Emit(OpCodes.Stelem_Ref);
			}

			if (method.DeclaringType.IsValueType)
			{
				

				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Mkrefany, method.DeclaringType);
				il.Emit(OpCodes.Ldtoken, method.DeclaringType);
				il.Emit(OpCodes.Call, method.Module.ImportReference(typeof(Type).GetMethod("GetTypeFromHandle")));
				il.Emit(OpCodes.Ldloc, argsVar);
				il.Emit(OpCodes.Call, bridgeCtorWrapperStr);
			}
			else
			{
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Ldtoken, method.DeclaringType);
				il.Emit(OpCodes.Call, method.Module.ImportReference(typeof(Type).GetMethod("GetTypeFromHandle")));
				il.Emit(OpCodes.Ldloc, argsVar);
				il.Emit(OpCodes.Call, bridgeCtorWrapper);
			}

			il.Emit(OpCodes.Ret);
		}

		private static void TransformGenericPropertyToProxy(MethodDefinition method, MethodReference bridgeWrapper, MethodReference exceptionHandler)
		{
			var body = method.Body;
			body.Instructions.Clear();
			body.Variables.Clear();
			body.ExceptionHandlers.Clear();
			body.InitLocals = true;
			var il = body.GetILProcessor();
			var objectType = method.Module.ImportReference(typeof(object));
			var objectArrayType = method.Module.ImportReference(typeof(object[]));
			var exceptionType = method.Module.ImportReference(typeof(Exception));
			var typeGetHandle = method.Module.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle)));

			var argsVar = new VariableDefinition(objectArrayType);
			var resultVar = new VariableDefinition(objectType);
			var exVar = new VariableDefinition(exceptionType);
			body.Variables.Add(argsVar);
			body.Variables.Add(resultVar);
			body.Variables.Add(exVar);

			var tryStart = il.Create(OpCodes.Nop);
			var tryEnd = il.Create(OpCodes.Nop);
			var handlerStart = il.Create(OpCodes.Stloc, exVar);
			var handlerEnd = il.Create(OpCodes.Nop);
			var retStart = il.Create(OpCodes.Nop);

			il.Append(tryStart);

			il.Emit(OpCodes.Ldc_I4, method.Parameters.Count);
			il.Emit(OpCodes.Newarr, objectType);
			il.Emit(OpCodes.Stloc, argsVar);
			for (int i = 0; i < method.Parameters.Count; i++)
			{
				il.Emit(OpCodes.Ldloc, argsVar);
				il.Emit(OpCodes.Ldc_I4, i);
				il.Emit(OpCodes.Ldarg, i + 1);
				il.Emit(OpCodes.Box, method.Parameters[i].ParameterType);
				il.Emit(OpCodes.Stelem_Ref);
			}

			il.Emit(OpCodes.Ldtoken, method.DeclaringType);
			il.Emit(OpCodes.Call, typeGetHandle);
			il.Emit(OpCodes.Ldarg_0);
			il.Emit(OpCodes.Ldstr, method.Name);
			il.Emit(OpCodes.Ldloc, argsVar);
			il.Emit(OpCodes.Ldtoken, method.ReturnType);
			il.Emit(OpCodes.Call, typeGetHandle);

			il.Emit(OpCodes.Call, bridgeWrapper);
			il.Emit(OpCodes.Stloc, resultVar);
			il.Emit(OpCodes.Leave, retStart);

			il.Append(tryEnd);
			il.Append(handlerStart);
			il.Emit(OpCodes.Ldloc, exVar);
			il.Emit(OpCodes.Call, exceptionHandler);
			if (method.ReturnType.MetadataType != MetadataType.Void)
			{
				il.Emit(OpCodes.Ldnull);
				il.Emit(OpCodes.Stloc, resultVar);
			}
			il.Emit(OpCodes.Leave, retStart);

			il.Append(handlerEnd);
			il.Append(retStart);

			if (method.ReturnType.MetadataType != MetadataType.Void)
			{
				il.Emit(OpCodes.Ldloc, resultVar);
				il.Emit(OpCodes.Unbox_Any, method.ReturnType);
			}

			il.Emit(OpCodes.Ret);

			body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
			{
				CatchType = exceptionType,
				TryStart = tryStart,
				TryEnd = tryEnd,
				HandlerStart = handlerStart,
				HandlerEnd = handlerEnd
			});
		}

		private static void TransformToProxy(MethodDefinition method, MethodReference bridgeWrapper, MethodReference bridgeWrapperStr, MethodReference exceptionHandler)
		{
			var body = method.Body;
			body.Instructions.Clear();
			body.Variables.Clear();
			body.ExceptionHandlers.Clear();
			body.InitLocals = true;

			var il = body.GetILProcessor();

			var objectType = method.Module.ImportReference(typeof(object));
			var objectArrayType = method.Module.ImportReference(typeof(object[]));
			var exceptionType = method.Module.ImportReference(typeof(Exception));
			var typeGetHandle = method.Module.ImportReference(
				typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))
			);

			var argsVar = new VariableDefinition(objectArrayType);
			var resultVar = new VariableDefinition(objectType);
			var exVar = new VariableDefinition(exceptionType);

			body.Variables.Add(argsVar);
			body.Variables.Add(resultVar);
			body.Variables.Add(exVar);

			var tryStart = il.Create(OpCodes.Nop);
			var tryEnd = il.Create(OpCodes.Nop);
			var handlerStart = il.Create(OpCodes.Stloc, exVar);
			var handlerEnd = il.Create(OpCodes.Nop);
			var retStart = il.Create(OpCodes.Nop);

			il.Append(tryStart);

			il.Emit(OpCodes.Ldc_I4, method.Parameters.Count);
			il.Emit(OpCodes.Newarr, objectType);
			il.Emit(OpCodes.Stloc, argsVar);

			for (int i = 0; i < method.Parameters.Count; i++)
			{
				il.Emit(OpCodes.Ldloc, argsVar);
				il.Emit(OpCodes.Ldc_I4, i);
				il.Emit(OpCodes.Ldarg, method.IsStatic ? i : i + 1);

				//if (method.Parameters[i].ParameterType.IsValueType)
					il.Emit(OpCodes.Box, method.Parameters[i].ParameterType);

				il.Emit(OpCodes.Stelem_Ref);
			}


			{
				il.Emit(OpCodes.Ldtoken, method.DeclaringType);
				il.Emit(OpCodes.Call, typeGetHandle);
			}

			if (method.IsStatic)
				il.Emit(OpCodes.Ldnull);
			else if (method.DeclaringType.IsValueType)
			{
				il.Emit(OpCodes.Ldarg_0);
				il.Emit(OpCodes.Mkrefany, method.DeclaringType);
			}
			else
				il.Emit(OpCodes.Ldarg_0);

			il.Emit(OpCodes.Ldstr, method.Name);
			il.Emit(OpCodes.Ldloc, argsVar);
			il.Emit(OpCodes.Ldtoken, method.ReturnType);
			il.Emit(OpCodes.Call, typeGetHandle);
			il.Emit(OpCodes.Call, method.DeclaringType.IsValueType && !method.IsStatic ? bridgeWrapperStr : bridgeWrapper);
			//il.Emit(OpCodes.Call, bridgeWrapper);
			il.Emit(OpCodes.Stloc, resultVar);

			il.Emit(OpCodes.Leave, retStart);

			il.Append(tryEnd);

			il.Append(handlerStart);
			il.Emit(OpCodes.Ldloc, exVar);
			il.Emit(OpCodes.Call, exceptionHandler);

			if (method.ReturnType.MetadataType != MetadataType.Void)
			{
				if (method.ReturnType.IsValueType)
				{
					il.Emit(OpCodes.Ldloca, resultVar);
					il.Emit(OpCodes.Initobj, method.ReturnType);
					il.Emit(OpCodes.Ldloc, resultVar);
				}
				else
				{
					il.Emit(OpCodes.Ldnull);
				}

				il.Emit(OpCodes.Stloc, resultVar);
			}

			il.Emit(OpCodes.Leave, retStart);
			il.Append(handlerEnd);

			il.Append(retStart);

			if (method.ReturnType.MetadataType != MetadataType.Void)
			{
				il.Emit(OpCodes.Ldloc, resultVar);

				if (method.ReturnType.IsValueType)
					il.Emit(OpCodes.Unbox_Any, method.ReturnType);
				else
					il.Emit(OpCodes.Castclass, method.ReturnType);
			}

			il.Emit(OpCodes.Ret);

			body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
			{
				CatchType = exceptionType,
				TryStart = tryStart,
				TryEnd = tryEnd,
				HandlerStart = handlerStart,
				HandlerEnd = handlerEnd
			});
		}
	}
}
