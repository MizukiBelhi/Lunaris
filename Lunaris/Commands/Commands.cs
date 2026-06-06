using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Lunaris
{
	internal class Commands
	{
		private static readonly Dictionary<string, ICommand> commands = new(StringComparer.OrdinalIgnoreCase);

		public interface ICommand
		{
			bool Invoke(string[] args);
			string Name { get; }
			string Description { get; }
			string ParamTypes { get; }
			bool FromPlugin { get; }
			public Delegate Action { get; }
		}

		class BaseCommand(Delegate action, string paramTypes, bool fromPlugin, string desc, string name) : ICommand
		{
			public string Name => name;
			public string Description => desc;
			public string ParamTypes => paramTypes;
			public Delegate Action => action;
			public bool FromPlugin => fromPlugin;

			public bool Invoke(string[] args)
			{
				var ParamCount = ParamTypes.Length;
				if (ParamCount == 0)
				{
					Action.DynamicInvoke();
					return true;
				}
				if (args.Length != ParamCount) return false;


				var cv = new object[ParamCount];
				for(int i=0;i<ParamCount; i++)
				{
					var param = args[i];
					var cParam = ParamTypes[i];

					if (!ParseParam(param, cParam, out var val)) return false;
					cv[i] = val;
				}

				Action.DynamicInvoke(cv);
				return true;
			}
		}

		class Command(Action handler, string desc, bool fp, string name) : BaseCommand(handler, "", fp, desc, name)
		{ }
		class Command<T1>(Action<T1> handler, string paramTypes, string desc, bool fp, string name) : BaseCommand(handler, paramTypes, fp, desc, name)
		{ }
		class Command<T1, T2>(Action<T1, T2> handler, string paramTypes, string desc, bool fp, string name) : BaseCommand(handler, paramTypes, fp, desc, name)
		{ }
		class Command<T1, T2, T3>(Action<T1, T2, T3> handler, string paramTypes, string desc, bool fp, string name) :  BaseCommand(handler, paramTypes, fp, desc, name)
		{ }
		class Command<T1, T2, T3, T4>(Action<T1, T2, T3, T4> handler, string paramTypes, string desc, bool fp, string name) : BaseCommand(handler, paramTypes, fp, desc, name)
		{ }
		class Command<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> handler, string paramTypes, string desc, bool fp, string name) : BaseCommand(handler, paramTypes, fp, desc, name)
		{ }
		class Command<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> handler, string paramTypes, string desc, bool fp, string name) : BaseCommand(handler, paramTypes, fp, desc, name)
		{ }
		class Command<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> handler, string paramTypes, string desc, bool fp, string name) : BaseCommand(handler, paramTypes, fp, desc, name)
		{ }
		class Command<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> handler, string paramTypes, string desc, bool fp, string name) : BaseCommand(handler, paramTypes, fp, desc, name)
		{ }


		private static bool ParseParam(string arg, char type, out object value)
		{
			object result = null!;
			switch (type)
			{
				case 's': if (string.IsNullOrWhiteSpace(arg)) { value = default!; return false; } result = arg; break;
				case 'i': if (!int.TryParse(arg, out int i)) { value = default!; return false; } result = i; break;
				case 'f': if (!float.TryParse(arg, out float f)) { value = default!; return false; } result = f; break;
				default: value = default!; return false;
			}
			value = result;
			return true;
		}

		public static void Init()
		{
			foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
			{
				ParseTypes(type, "lunaris");
			}
		}

		public static void ParsePluginCommands(Assembly pluginAssembly, string pluginName)
		{
			foreach (var type in pluginAssembly.GetTypes())
			{
				ParseTypes(type, pluginName, true);
			}
		}

		public static List<ICommand> GetCommandsForPlugin(string pluginName)
		{
			return commands.Where(x => x.Key.Contains(pluginName)).Select(x => x.Value).ToList();
		}

		private static void ParseTypes(Type type, string prefix, bool fromPlugin=false)
		{
			foreach (var method in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				var attr = method.GetCustomAttribute<LunarisCommandAttribute>();
				if (attr != null)
				{
					var methodParams = method.GetParameters().Select(p => p.ParameterType).ToArray();

					string autoParamString = new([.. methodParams.Select(t => t switch
					{
						Type _ when t == typeof(string) => 's',
						Type _ when t == typeof(int) => 'i',
						Type _ when t == typeof(float) => 'f',
						_ => throw new ArgumentException($"Unknown param type '{t}'", nameof(t))
					})]);


					//Debug.Log($"Method:'{method.Name}' typeArgs:'{methodParams.Length}' ParamsAs:'{FormatUsage(autoParamString)}'  Params:'{autoParamString}' Name:'{attr.Name}' Desc:'{attr.Description}'");

					Type actionType;
					Type commandType;

					if (methodParams.Length == 0)
					{
						actionType = typeof(Action);
						commandType = typeof(Command);

						commands[prefix + attr.Name] = (ICommand)Activator.CreateInstance(commandType, method.CreateDelegate(actionType), attr.Description, fromPlugin, attr.Name)!;
					}
					else
					{
						actionType = Expression.GetActionType(methodParams);
						Type genericCommandDefinition = methodParams.Length switch
						{
							1 => typeof(Command<>),
							2 => typeof(Command<,>),
							3 => typeof(Command<,,>),
							4 => typeof(Command<,,,>),
							5 => typeof(Command<,,,,>),
							6 => typeof(Command<,,,,,>),
							7 => typeof(Command<,,,,,,>),
							8 => typeof(Command<,,,,,,,>),
							_ => throw new NotSupportedException("Too many params!")
						};

						commandType = genericCommandDefinition.MakeGenericType(methodParams);
						commands[prefix+attr.Name] = (ICommand)Activator.CreateInstance(commandType, method.CreateDelegate(actionType), autoParamString, attr.Description, fromPlugin, attr.Name)!;
					}
				}
			}
		}


		public static bool RunCommand(string txt)
		{
			if (string.IsNullOrWhiteSpace(txt) || !txt.StartsWith("/")) return true;
			string[] tokens = txt.TrimStart('/').Split(' ');

			if (tokens.Length < 2) return true;
			string prefix = tokens[0].ToLower();
			string cmdName = tokens[1].ToLower();
			if (!commands.TryGetValue(prefix+cmdName, out ICommand cmd))
				return true;

			string[] args = [.. tokens.Skip(2)];

			if (!cmd.Invoke(args))
				WriteToGameLog($"Usage: /{prefix} {cmdName} {FormatUsage(cmd.ParamTypes)}");

			return false;
		}


		public static string FormatUsage(string types)
		{
			return string.Join(" ", types.Select(c =>
			{
				return c switch
				{
					's' => "<string>",
					'i' => "<int>",
					'f' => "<float>",
					_ => "<unknown>"
				};
			}));
		}

		private static void WriteToGameLog(string txt)
		{
			if (GameData.ChatLog != null)
				UpdateSocialLog.LogAdd(txt, "#00A1FF");
		}


		[LunarisCommand("plugins", "Opens Plugin Installer.")]
		public static void Command_OpenPlugin()
		{
			UI.OpenPluginInstaller();
		}

		[LunarisCommand("dev", "Toggles Developer Bar.")]
		public static void Command_OpenDev()
		{
			UI.ShowMenuBar = !UI.ShowMenuBar;
		}

		[LunarisCommand("help", "This command.")]
		public static void Command_DisplayHelp()
		{
			WriteToGameLog($"== COMMANDS ==");
			var _commands = commands.Where(r => r.Value.FromPlugin == false);
			foreach (var c in _commands)
			{
				var cmdName = c.Key;
				var cmd = c.Value;
				var prfx = cmdName.Replace(cmd.Name, "");
				WriteToGameLog($"/{prfx} {cmd.Name} {FormatUsage(cmd.ParamTypes)} | {cmd.Description}");
			}

			_commands = commands.Where(r => r.Value.FromPlugin == true);
			if (_commands.Count() > 0)
			{
				WriteToGameLog($"== PLUGIN COMMANDS ==");
				foreach (var c in _commands)
				{
					var cmdName = c.Key;
					var cmd = c.Value;
					var prfx = cmdName.Replace(cmd.Name, "");
					WriteToGameLog($"/{prfx} {cmd.Name} {FormatUsage(cmd.ParamTypes)} | {cmd.Description}");
				}
			}
		}

		[LunarisCommand("test", "Test Command.")]
		public static void Command_DisplayTest(string a, int i)
		{
			
		}
	}
}
