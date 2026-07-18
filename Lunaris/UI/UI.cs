using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Profiling;
using Lunaris.Message;
using ImGuiNET;
using Lunaris.IGUI;
using System.Threading;
using System.Reflection;
using Lunaris.Config;

namespace Lunaris
{

	/// <summary>
	/// Ugh.
	/// </summary>
	internal static class UI
	{
		[DllImport("winhttp.dll", CallingConvention = CallingConvention.StdCall)]
		public static extern long GetProcessVramUsage();


		public static bool ShowMenuBar = true;
		public static bool ShowConsole = false;
		public static bool ShowInspector = false;
		public static bool ShowTest = false;
		public static bool ShowTest8 = false;
		public static bool ShowAssWin = false;


		public static string _activeMenuDropdown = null;
		public static bool _showAnotherWindow = false;


		public static Texture2D _whiteTexture;
		public static List<ConsoleMessage> consoleLog = [];
		public static bool ScrollToBottom = true;
		public static bool AutoScroll = true;
		public static string searchText = "";
		public static bool OnFirstOpen = false;

		public static IntPtr InstalledIcon;
		public static IntPtr UpdateIcon;
		public static IntPtr TroubleIcon;
		public static IntPtr OutdatedInstallableIcon;
		public static IntPtr DisabledIcon;
		public static IntPtr DefaultIcon;

		public static PluginInstaller installer = new();

		public struct ConsoleMessage
		{
			public ILoggingService.MessageType messageType;
			public string message;
		}

		public static UISettings Settings;

		public class UISettings
		{
			public bool DrawDevAtStart = false;
			public bool ShowDevBarInfo = true;
			public bool AskForPerms = true;
			public bool NotifyPerms = true;
			public bool AutoEnablePlugin = true;
			public bool NotifyPluginUpdate = true;
			public bool NotifyLunarisUpdate = true;
			public bool AutoUpdatePlugin = true;

			[Keybind(KeyCode.None)]
			public KeybindEntry OpenPluginInstaller;
		}

		private static IConfigHandle<UISettings> cfgHandle;

		internal static void Start()
		{
			cfgHandle = Bridge.config.Register(ref Settings);

			ShowMenuBar = Settings.DrawDevAtStart;

			TitleScreenMenu.entries.Add(new TitleScreenMenuEntry("Lunaris", Bridge.icon, null));
			TitleScreenMenu.entries.Add(new TitleScreenMenuEntry("Plugin Installer", Bridge.icon, () => { UI.OpenPluginInstaller(); }));

			Settings.OpenPluginInstaller.OnPressed += () => { UI.OpenPluginInstaller(); };

			ImGuiWrap.OnRender += Draw;
		}

		internal static void Dispose()
		{
			ImGuiWrap.OnRender -= Draw;
		}

		internal static void SaveSettings()
		{
			Bridge.config.Save();
		}


		internal static void Draw()
		{

			ImGui.PushFont(ImGuiWrap.defaultFont.ImFont);

			Notifications.Draw();
			//TitleScreenMenu.Draw();
			installer.OnDraw();
			PluginOptions.Draw();
			LunarisSettings.Draw();

			if (ShowMenuBar)
			{
				ImGui.BeginMainMenuBar();

				if (ImGui.BeginMenu("Lunaris", true))
				{
					if (ImGui.MenuItem("Draw Dev Menu", "", ShowMenuBar, true))
					{
						ShowMenuBar = !ShowMenuBar;
					}

					if (ImGui.MenuItem("Draw Dev Menu at Start", "", Settings.DrawDevAtStart, true))
					{
						Settings.DrawDevAtStart = !Settings.DrawDevAtStart;
						SaveSettings();
					}

					if (ImGui.MenuItem("Show Log Window", "", ShowConsole, true))
					{
						ShowConsole = !ShowConsole;
					}

					if (ImGui.MenuItem("Open Settings", "", false, true))
					{
						LunarisSettings.IsOpen = true;
					}

					ImGui.Separator();

					if (ImGui.MenuItem("Exit Game", "", false, true))
					{
						Application.Quit();
						return;
						//We're still alive after that?
						//CauseCrash(); //Easy way to end the game
					}

					ImGui.Separator();

					//ImGui.MenuItem($"Erenshor: 0.4", false);
					ImGui.MenuItem($"Unity: {Application.unityVersion}", false);
					ImGui.MenuItem($"CLR: {Environment.Version}", false);


					ImGui.End();
				}
				if (ImGui.BeginMenu("GUI", true))
				{
					if (ImGui.MenuItem("Unity Inspector", "", ShowInspector, true))
					{
						ShowInspector = !ShowInspector;
					}
					ImGui.Separator();
					if (ImGui.MenuItem("Draw ImGui Demo", "", _showAnotherWindow, true))
					{
						_showAnotherWindow = !_showAnotherWindow;
					}
					if (ImGui.MenuItem("Game Statistics", "", ShowTest8, true))
					{
						ShowTest8 = !ShowTest8;
					}
					
					ImGui.Separator();
					if (ImGui.MenuItem("Add Notification", "", false, true))
					{
						Notifications.Add(new Notification("This is a test", TimeSpan.FromSeconds(20)));
					}
					if (ImGui.MenuItem("Add Short Notification", "", false, true))
					{
						Notifications.Add(new Notification("This is short a test", Notifications.DefaultDuration));
					}
					if (ImGui.MenuItem("Add Error Notification", "", false, true))
					{
						Notifications.Add(new Notification(NotificationType.Error, "THIS IS A VERY BAD ERROR!!!", TimeSpan.FromSeconds(20)));
					}
					if (ImGui.MenuItem("Add Warning Notification", "", false, true))
					{
						Notifications.Add(new Notification(NotificationType.Warning, "Wow watch out", Notifications.DefaultDuration));
					}
					if (ImGui.MenuItem("Add Info Notification", "", false, true))
					{
						Notifications.Add(new Notification(NotificationType.Info, "This is very important", Notifications.DefaultDuration));
					}
					if(ImGui.MenuItem("Bepin Deprecation Warning", "", false, true))
					{
						Notifications.Add(new Notification(NotificationType.Warning, $"TestNotifyNameVeryVeryLong is using BepInEx, which is deprecated.", TimeSpan.FromSeconds(20)));
					}

					ImGui.Separator();
					if (ImGui.MenuItem("Show Info", "", Settings.ShowDevBarInfo, true))
					{
						Settings.ShowDevBarInfo = !Settings.ShowDevBarInfo;
						SaveSettings();
					}


					ImGui.End();
				}

				if (ImGui.BeginMenu("Plugins", true))
				{
					if (ImGui.MenuItem("Show Plugin Installer", "", PluginInstaller.isWindowOpen, true))
					{
						OpenPluginInstaller();
					}
					
					if (ImGui.MenuItem("Assembly Viewer", "", ShowAssWin, true))
					{
						ShowAssWin = !ShowAssWin;
					}
					ImGui.Separator();

					ImGui.MenuItem($"API Level: 1", false);
					ImGui.MenuItem($"Loaded Plugins: {installer.GetEnabledPluginCount()}", false);

					ImGui.End();
				}

				if (Settings.ShowDevBarInfo)
				{
					long totalRam = Profiler.GetTotalReservedMemoryLong();
					long totalVram = (long)SystemInfo.graphicsMemorySize * 1024 * 1024;
					float msec = Time.smoothDeltaTime * 1000.0f;
					float fps = 1.0f / Time.smoothDeltaTime;
					var _gc = GC.GetTotalMemory(false);

					var items = new[]
					{
						$"Ver {Bridge.version.ToString()}",
						Time.frameCount.ToString("000000"),
						$"{fps:000} FPS ({msec:00.0}ms)",
						$"GC:{FormatBytes(_gc)}",
						$"RAM:{FormatBytes(totalRam)}",
						$"VRAM:{FormatBytes(GetProcessVramUsage())}/{FormatBytes(totalVram)}",
					};

					float totalWidth = items.Sum(s => ImGui.CalcTextSize(s).X + ImGui.GetStyle().ItemSpacing.X * 2);
					ImGui.SetCursorPosX(ImGui.GetWindowWidth() - totalWidth);

					foreach (var item in items)
						ImGui.BeginMenu(item, false);
				}

				ImGui.EndMainMenuBar();
			}

			if (ShowConsole)
				DrawConsole();
			//else
			//	Main.Log.EnableConsoleLogging = true;*/

			if (ShowInspector)
				UnityExplorer.Draw(ref ShowInspector);

			if (ShowTest8)
			{
				//var IO = ImGui.GetIO();
				//Signatures signatures = Main.signatures;


				ImGui.Begin($"Game Statistics ::: FPS:{Bridge.fpsFrame}###GameStatistics", ImGuiWindowFlags.None);
				ImGui.Text($"ClientVer: {0:X}");

				ImGui.End();
			}


			if (_showAnotherWindow)
				ImGui.ShowDemoWindow(ref _showAnotherWindow);

			if (ShowAssWin)
			{
				DrawAssemblyWindow();
			}


			//OnRender?.Invoke();
			//ImGui.PopAllStacks();

			ImGui.PopFont();
		}


		private static void DrawAssemblyWindow()
		{
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(400, 300), ImGuiCond.FirstUseEver);

			if (!ImGui.Begin("Assembly Viewer", ImGuiWindowFlags.NoCollapse))
			{
				ImGui.End();
				return;
			}
			if (ImGui.BeginChild("AssemblyScrollArea", new System.Numerics.Vector2(0, 200)))
			{
				foreach (var item in Bridge.loadedRuntimeAssemblies)
				{
					ImGui.Text($"{item.Key} - {item.Value}");
					ImGui.Separator();
				}
				ImGui.EndChild();
			}

			ImGui.End();
		}


		internal static async Task<object> TryEval(string code)
		{
			try
			{
				var scriptingAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Microsoft.CodeAnalysis.CSharp.Scripting");

				if (scriptingAsm == null)
					return "Roslyn not loaded. Drop Microsoft.CodeAnalysis.CSharp.Scripting.dll in the plugins folder to run code.";

				var scriptType = scriptingAsm.GetType("Microsoft.CodeAnalysis.CSharp.Scripting.CSharpScript");
				var optionsType = AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => { try { return a.GetTypes(); } catch { return []; } }).FirstOrDefault(t => t.FullName == "Microsoft.CodeAnalysis.Scripting.ScriptOptions");

				var defaultOptions = optionsType.GetProperty("Default").GetValue(null);
				var withRefs = optionsType.GetMethod("WithReferences", [typeof(IEnumerable<Assembly>)]).Invoke(defaultOptions, [AppDomain.CurrentDomain.GetAssemblies()]);
				var withImports = optionsType.GetMethod("WithImports", [typeof(IEnumerable<string>)]).Invoke(withRefs, [new[] { "System", "UnityEngine", "Lunaris" }]);

				var task = (Task)scriptType.GetMethod("EvaluateAsync", [typeof(string), optionsType, typeof(Type), typeof(CancellationToken)]).Invoke(null, [code, withImports, null, CancellationToken.None]);

				await task;
				var resultProp = task.GetType().GetProperty("Result");
				return resultProp?.GetValue(task);
			}
			catch (Exception e)
			{
				DispatcherBehaviour.RunOnMainThread(() => Bridge.Logger.Log($"EEE {e}"));
				return e.Message;
			}
		}

		private static List<string> _history = [];
		private static int _historyPos = -1;

		private static unsafe int ConsoleInputCallback(ImGuiInputTextCallbackData* data)
		{
			if (data->EventFlag == ImGuiInputTextFlags.CallbackHistory)
			{
				if (data->EventKey == ImGuiKey.UpArrow && _historyPos < _history.Count - 1)
					_historyPos++;
				else if (data->EventKey == ImGuiKey.DownArrow && _historyPos > -1)
					_historyPos--;

				var entry = _historyPos >= 0 ? _history[_history.Count - 1 - _historyPos] : "";
				var bytes = System.Text.Encoding.UTF8.GetBytes(entry + "\0");
				Marshal.Copy(bytes, 0, (IntPtr)data->Buf, Math.Min(bytes.Length, data->BufSize));
				data->BufTextLen = entry.Length;
				data->CursorPos = entry.Length;
				data->BufDirty = 1;
			}
			return 0;
		}

		private static void SendTextCon()
		{
			var code = searchText;

			Task.Run(async () =>
			{
				try
				{
					var result = await TryEval(code);
					if (result != null)
						DispatcherBehaviour.RunOnMainThread(() => Bridge.Logger.Log(result.ToString()));
				}
				catch (Exception e)
				{
					Bridge.Logger.Log($"Error evaluating code: {e}");
					return;
				}
			});
		}

		public unsafe static void DrawConsole()
		{
			ImGuiWindowFlags flags = ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar | ImGuiWindowFlags.NoScrollbar;


			//if (!OnFirstOpen)
			{
				var vector = new System.Numerics.Vector2(600, 300);
				ImGui.SetNextWindowSize(vector, ImGuiCond.FirstUseEver);
				//	OnFirstOpen = true;
			}

			ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = true;


			if (!ImGui.Begin("Console", ref ShowConsole, flags))
			{
				ImGui.End();
				return;
			}

			

			if (ImGui.BeginMenuBar())
			{
				if (ImGui.BeginMenu("Settings", true))
				{
					if (ImGui.MenuItem("Autoscroll", "", AutoScroll, true))
					{
						AutoScroll = !AutoScroll;
					}

					if (ImGui.MenuItem("Scroll To Bottom", "", false, true))
					{
						ScrollToBottom = true;
					}




					ImGui.End();
				}
				ImGui.EndMenuBar();
			}

			ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 0f);
			ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 0f);
			ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 0f);
			ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0f);
			ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0f, 0f));

			float footer_height_to_reserve = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing();

			var scrollRegionVec = new System.Numerics.Vector2(0, -footer_height_to_reserve);

			if (ImGui.BeginChild("ScrollingRegion###1", scrollRegionVec, true, ImGuiWindowFlags.HorizontalScrollbar))
			{
				ImGui.BeginGroup();
				var cursorPos = ImGui.GetCursorPos();
				int id = 0;
				string actualMessages = "";
				foreach (var mes in consoleLog)
				{
					var msgType = mes.messageType;
					var color = LunarisColors.White;
					var mt = "[Unknown]";
					switch (msgType)
					{
						case ILoggingService.MessageType.Info:
						color = LunarisColors.Green;
						mt = "[Info]";
						break;
						case ILoggingService.MessageType.Warn:
						color = LunarisColors.Yellow;
						mt = "[Warning]";
						break;
						case ILoggingService.MessageType.Error:
						color = LunarisColors.Red;
						mt = "[Error]";
						break;
						case ILoggingService.MessageType.Debug:
						color = LunarisColors.Blue;
						mt = "[Debug]";
						break;
						case ILoggingService.MessageType.None:
						color = LunarisColors.White;
						mt = "";
						break;
						default:
						break;
					}

					var ncursorPos = ImGui.GetCursorPos();
					var msz = ImGui.CalcTextSize(mes.message);
					var mesSize = msz.Y / 16;
					ncursorPos.Y -= 4.5f;
					if (id > 0)
						ImGui.SetCursorPos(ncursorPos);

					var app = "";

					if (mesSize > 1)
						app = string.Concat(Enumerable.Repeat("\r\n", (int)mesSize));

					ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0.0f, 0.0f));
					ImGui.PushStyleColor(ImGuiCol.FrameBg, new System.Numerics.Vector4(0, 0, 0, 0));
					ImGui.PushStyleColor(ImGuiCol.Text, color);

					ImGui.PushItemWidth(ImGui.CalcTextSize(mt).X);

					ImGui.Text(mt + app);
					ImGui.PopItemWidth();
					ImGui.PopStyleColor();
					ImGui.PopStyleColor();
					ImGui.PopStyleVar(1);

					if (id > 0)
						actualMessages = actualMessages + "\r\n" + mes.message;
					else
						actualMessages = actualMessages + mes.message;

					id++;
				}

				cursorPos.X += ImGui.CalcTextSize("[Unknown]").X + 10;
				ImGui.SetCursorPos(cursorPos);

				ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(0.0f, 0.0f));
				ImGui.PushStyleColor(ImGuiCol.FrameBg, new System.Numerics.Vector4(0, 0, 0, 0));
				var textSize = ImGui.CalcTextSize(actualMessages);

				var outStr = actualMessages;

				ImGui.InputTextMultiline($"##2", ref outStr, (uint)outStr.Length + 1, new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, textSize.Y), ImGuiInputTextFlags.ReadOnly);


				ImGui.PopStyleColor();
				ImGui.PopStyleVar(1);

				ImGui.EndGroup();

				if (ScrollToBottom || (AutoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY()))
					ImGui.SetScrollHereY(1.0f);

				ScrollToBottom = false;


				//ImGui.ImageButton("dasd", tex.textureId, new Vector2(64, 64));
			}
			ImGui.EndChild();

			ImGui.PopStyleVar(5);

			ImGui.SetNextItemWidth(ImGui.GetWindowWidth() - 100);

			//ImGuiInputTextFlags input_text_flags = ImGuiInputTextFlags.EnterReturnsTrue | ImGuiInputTextFlags.EscapeClearsAll | ImGuiInputTextFlags.CallbackCompletion | ImGuiInputTextFlags.CallbackHistory;

			if (ImGui.InputTextWithHint("###ConsoleInput", "", ref searchText, 100, 
				ImGuiInputTextFlags.AutoSelectAll | ImGuiInputTextFlags.CallbackHistory | ImGuiInputTextFlags.CallbackCompletion | ImGuiInputTextFlags.EnterReturnsTrue, ConsoleInputCallback))
			{
				if (!string.IsNullOrEmpty(searchText))
				{
					SendTextCon();
					if (_history.Count == 0 || _history[_history.Count - 1] != searchText)
						_history.Add(searchText);
					_historyPos = -1;
					searchText = "";
					ImGui.SetKeyboardFocusHere(-1);
				}
			}
			ImGui.SameLine();
			if (ImGui.Button("Send"))
			{
				if (!string.IsNullOrEmpty(searchText))
				{
					SendTextCon();
					if (_history.Count == 0 || _history[_history.Count - 1] != searchText)
						_history.Add(searchText);
					_historyPos = -1;
					searchText = "";
					ImGui.SetKeyboardFocusHere(-1);
				}
			}

			//ImGui.Image(tex.textureId, new Vector2(64f, 64f));

			ImGui.End();
			ImGui.GetIO().ConfigWindowsMoveFromTitleBarOnly = false;
		}


		public static void OpenPluginInstaller()
		{
			PluginInstaller.Open();
		}

		static unsafe void CauseCrash()
		{
			int* ptr = null;
			*ptr = 42;  // Dereferencing a null pointer causes a crash
		}

		public static string FormatBytes(long bytes)
		{
			string[] suffix = { "B", "KB", "MB", "GB", "TB" };
			int i;
			double dblSByte = bytes;
			for (i = 0; i < suffix.Length && bytes >= 1024; i++, bytes /= 1024)
			{
				dblSByte = bytes / 1024.0;
			}

			return $"{dblSByte:0.00} {suffix[i]}";
		}

		public static bool ToggleButton(string id, ref bool v)
		{
			var colors = ImGui.GetStyle().Colors;
			var p = ImGui.GetCursorScreenPos();
			var drawList = ImGui.GetWindowDrawList();

			var height = ImGui.GetFrameHeight();
			var width = height * 1.55f;
			var radius = height * 0.50f;

			// TODO: animate

			var changed = false;
			ImGui.InvisibleButton(id, new System.Numerics.Vector2(width, height));
			if (ImGui.IsItemClicked())
			{
				v = !v;
				changed = true;
			}


			if (ImGui.IsItemHovered())
				drawList.AddRectFilled(p, new System.Numerics.Vector2(p.X + width, p.Y + height), ImGui.GetColorU32(!v ? ImGuiStyle.Button* 2f : ImGuiStyle.ButtonHover), height * 0.5f);
			else
				drawList.AddRectFilled(p, new System.Numerics.Vector2(p.X + width, p.Y + height), ImGui.GetColorU32(!v ? ImGuiStyle.Button*1.2f : ImGuiStyle.ButtonHover * 0.8f), height * 0.5f);
			drawList.AddCircleFilled(new System.Numerics.Vector2(p.X + radius + ((v ? 1 : 0) * (width - (radius * 2.0f))), p.Y + radius), radius - 1.5f, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1, 1, 1, 1)));

			return changed;
		}

		private static float _spinAngle = 0f;

		internal static void DrawSpinner(float size = 16f)
		{
			_spinAngle = (_spinAngle + ImGui.GetIO().DeltaTime * 4f) % (2f * (float)Math.PI);

			var pos = ImGui.GetCursorScreenPos();
			var center = new System.Numerics.Vector2(pos.X + size * 0.5f, pos.Y + size * 0.5f);
			var dl = ImGui.GetWindowDrawList();
			uint color = ImGui.GetColorU32(ImGuiCol.Text);

			int segments = 8;
			float radius = size * 0.5f;
			for (int i = 0; i < segments; i++)
			{
				float angle = _spinAngle + i * (2f * (float)Math.PI / segments);
				float alpha = (float)i / segments;
				var dotPos = new System.Numerics.Vector2(
					center.X + MathF.Cos(angle) * radius,
					center.Y + MathF.Sin(angle) * radius);
				uint col = (color & 0x00FFFFFF) | ((uint)(alpha * 255) << 24);
				dl.AddCircleFilled(dotPos, 2f, col);
			}

			ImGui.Dummy(new System.Numerics.Vector2(size, size));
		}

		public static bool IconButton(string iconText, System.Numerics.Vector4? defaultColor = null, System.Numerics.Vector4? activeColor = null, System.Numerics.Vector4? hoveredColor = null)
		{
			var numColors = 0;

			if (defaultColor.HasValue)
			{
				ImGui.PushStyleColor(ImGuiCol.Button, defaultColor.Value);
				numColors++;
			}

			if (activeColor.HasValue)
			{
				ImGui.PushStyleColor(ImGuiCol.ButtonActive, activeColor.Value);
				numColors++;
			}

			if (hoveredColor.HasValue)
			{
				ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hoveredColor.Value);
				numColors++;
			}

			var icon = iconText;

			ImGui.PushID(iconText);

			ImGui.PushFont(ImGuiWrap.iconFont.ImFont);
			var iconSize = ImGui.CalcTextSize(icon);
			ImGui.PopFont();

			var dl = ImGui.GetWindowDrawList();
			var cursor = ImGui.GetCursorScreenPos();

			var buttonWidth = iconSize.X + (ImGui.GetStyle().FramePadding.X * 2);
			var buttonHeight = ImGui.GetFrameHeight();
			var button = ImGui.Button(string.Empty, new System.Numerics.Vector2(buttonWidth, buttonHeight));

			var iconPos = new System.Numerics.Vector2(cursor.X + ImGui.GetStyle().FramePadding.X, cursor.Y + ImGui.GetStyle().FramePadding.Y);

			ImGui.PushFont(ImGuiWrap.iconFont.ImFont);
			dl.AddText(iconPos, ImGui.GetColorU32(ImGuiCol.Text), icon);
			ImGui.PopFont();

			ImGui.PopID();

			if (numColors > 0)
				ImGui.PopStyleColor(numColors);

			return button;
		}


		public static void DisabledToggleButton(string id, bool v)
		{
			var colors = ImGui.GetStyle().Colors;
			var p = ImGui.GetCursorScreenPos();
			var drawList = ImGui.GetWindowDrawList();

			var height = ImGui.GetFrameHeight();
			var width = height * 1.55f;
			var radius = height * 0.50f;

			// TODO: animate
			ImGui.InvisibleButton(id, new System.Numerics.Vector2(width, height));

			var dimFactor = 0.5f;

			drawList.AddRectFilled(p, new System.Numerics.Vector2(p.X + width, p.Y + height), ImGui.GetColorU32(v ? colors[(int)ImGuiCol.Button] * dimFactor : new System.Numerics.Vector4(0.55f, 0.55f, 0.55f, 1.0f) * dimFactor), height * 0.50f);
			drawList.AddCircleFilled(new System.Numerics.Vector2(p.X + radius + ((v ? 1 : 0) * (width - (radius * 2.0f))), p.Y + radius), radius - 1.5f, ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1, 1, 1, 1) * dimFactor));
		}

		public static bool DisabledButton(FontAwesomeIcon icon, int? id = null, System.Numerics.Vector4? defaultColor = null, System.Numerics.Vector4? activeColor = null, System.Numerics.Vector4? hoveredColor = null, float alphaMult = .5f)
		{
			ImGui.PushFont(ImGuiWrap.iconFont.ImFont);

			var text = ToIconString(icon);
			if (id.HasValue)
				text = $"{text}##{id}";

			var button = DisabledButton(text, defaultColor, activeColor, hoveredColor, alphaMult);

			ImGui.PopFont();

			return button;
		}

		public static bool DisabledButton(string labelWithId, System.Numerics.Vector4? defaultColor = null, System.Numerics.Vector4? activeColor = null, System.Numerics.Vector4? hoveredColor = null, float alphaMult = .5f)
		{
			if (defaultColor.HasValue)
				ImGui.PushStyleColor(ImGuiCol.Button, defaultColor.Value);

			if (activeColor.HasValue)
				ImGui.PushStyleColor(ImGuiCol.ButtonActive, activeColor.Value);

			if (hoveredColor.HasValue)
				ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hoveredColor.Value);

			var style = ImGui.GetStyle();
			ImGui.PushStyleVar(ImGuiStyleVar.Alpha, style.Alpha * alphaMult);

			var button = ImGui.Button(labelWithId);

			ImGui.PopStyleVar();

			if (defaultColor.HasValue)
				ImGui.PopStyleColor();

			if (activeColor.HasValue)
				ImGui.PopStyleColor();

			if (hoveredColor.HasValue)
				ImGui.PopStyleColor();

			return button;
		}

		private static System.Numerics.Vector2 _cardStartCursor;
		private static System.Numerics.Vector2 _cardSize;
		internal static void BeginCard(System.Numerics.Vector2 size)
		{
			ImGui.PushStyleColor(ImGuiCol.Button, ImGuiStyle.CardBg);
			ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGuiStyle.CardBgHover);
			ImGui.PushStyleColor(ImGuiCol.Border, ImGuiStyle.BorderColor);
			ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 8.0f);
			ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, 1.0f);

			//ImGui.BeginChild(id, size, true, ImGuiWindowFlags.AlwaysAutoResize);
			_cardStartCursor = ImGui.GetCursorPos();
			_cardSize = size;

			var screenPos = ImGui.GetCursorScreenPos();
			//ImGui.GetWindowDrawList().PushClipRect(screenPos, screenPos + size, true);
		}

		internal static void EndCard()
		{
			//ImGui.GetWindowDrawList().PopClipRect();
			ImGui.SetCursorPos(_cardStartCursor + new System.Numerics.Vector2(0, _cardSize.Y + ImGui.GetStyle().ItemSpacing.Y));

			//ImGui.EndChild();
			ImGui.PopStyleVar(2);
			ImGui.PopStyleColor(3);
		}

		internal static void DrawTruncatedText(string text, float maxWidth, float fadeStartNormalized = 0.75f)
		{
			DrawTruncatedFuzzyText(text, null, default, maxWidth, fadeStartNormalized);
		}

		internal static void DrawTruncatedFuzzyText(string text, string query, System.Numerics.Vector4 matchCol, float maxWidth, float fadeStartNormalized = 0.75f)
		{
			var drawList = ImGui.GetWindowDrawList();
			var baseCol = ImGui.GetStyle().Colors[(int)ImGuiCol.Text];
			float thickness = 0.6f;

			float ellipsisWidth = maxWidth > 0f ? ImGui.CalcTextSize("...").X : 0f;
			float effectiveWidth = maxWidth > 0f ? maxWidth - ellipsisWidth : float.MaxValue;
			float totalTextWidth = ImGui.CalcTextSize(text).X;
			bool willTruncate = maxWidth > 0f && totalTextWidth > effectiveWidth;

			float fadeBegin = willTruncate ? maxWidth * fadeStartNormalized : float.MaxValue;
			float fadeWidth = willTruncate ? maxWidth - fadeBegin : 1f;

			// build match map
			var matchMap = new bool[text.Length];
			if (!string.IsNullOrEmpty(query))
			{
				string textLower = text.ToLower();
				string queryLower = query.ToLower();
				int lastIdx = 0;
				for (int i = 0; i < queryLower.Length; i++)
				{
					int foundIdx = textLower.IndexOf(queryLower[i], lastIdx);
					if (foundIdx == -1) break;
					matchMap[foundIdx] = true;
					lastIdx = foundIdx + 1;
				}
			}

			var startPos = ImGui.GetCursorScreenPos();
			float charX = 0f;
			bool truncated = false;

			for (int i = 0; i < text.Length; i++)
			{
				var ch = text[i].ToString();
				float charWidth = ImGui.CalcTextSize(ch).X;

				if (maxWidth > 0f && charX + charWidth > effectiveWidth)
				{
					truncated = true;
					break;
				}

				float alpha = baseCol.W;
				if (charX >= fadeBegin)
					alpha *= Math.Max(0f, 1f - ((charX - fadeBegin) / fadeWidth));

				var pos = new System.Numerics.Vector2(startPos.X + charX, startPos.Y);

				if (matchMap[i])
				{
					uint col = ImGui.ColorConvertFloat4ToU32(matchCol with { W = Math.Max(0f, matchCol.W * (alpha / baseCol.W)) });
					drawList.AddText(new System.Numerics.Vector2(pos.X - thickness, pos.Y), col, ch);
					drawList.AddText(new System.Numerics.Vector2(pos.X + thickness, pos.Y), col, ch);
					drawList.AddText(new System.Numerics.Vector2(pos.X, pos.Y - thickness), col, ch);
					drawList.AddText(new System.Numerics.Vector2(pos.X, pos.Y + thickness), col, ch);
					drawList.AddText(pos, col, ch);
				}
				else
				{
					uint col = ImGui.ColorConvertFloat4ToU32(baseCol with { W = alpha });
					drawList.AddText(pos, col, ch);
				}

				charX += charWidth;
			}

			if (truncated)
			{
				float dotWidth = ImGui.CalcTextSize(".").X;
				for (int j = 0; j < 3; j++)
				{
					float dotX = charX + dotWidth * j;
					float alpha = baseCol.W;
					if (dotX >= fadeBegin)
						alpha *= Math.Max(0f, 1f - ((dotX - fadeBegin) / fadeWidth));
					uint col = ImGui.ColorConvertFloat4ToU32(baseCol with { W = alpha });
					drawList.AddText(new System.Numerics.Vector2(startPos.X + dotX, startPos.Y), col, ".");
				}
			}

			ImGui.Dummy(new System.Numerics.Vector2(maxWidth > 0f ? maxWidth : charX, ImGui.GetTextLineHeight()));
		}


		internal static string ToIconString(FontAwesomeIcon i)
		{
			return ((char)i).ToString();
		}

		public static class LunarisColors
		{
			public static System.Numerics.Vector4 Red = new(1.0f, 0.0f, 0.0f, 1.0f);
			public static System.Numerics.Vector4 Blue = new(0.0f, 0.0f, 1.0f, 1.0f);
			public static System.Numerics.Vector4 Green = new(0.0f, 1.0f, 0.0f, 1.0f);
			public static System.Numerics.Vector4 Yellow = new(1.0f, 1.0f, 0.0f, 1.0f);
			public static System.Numerics.Vector4 White = new(1.0f, 1.0f, 1.0f, 1.0f);
			public static System.Numerics.Vector4 Black = new(0f, 0f, 0f, 1f);
			public static System.Numerics.Vector4 LunarisRed = new(1f, 0f, 0f, 1f);
			public static System.Numerics.Vector4 LunarisGrey = new(0.7f, 0.7f, 0.7f, 1f);
			public static System.Numerics.Vector4 LunarisGrey2 = new(0.7f, 0.7f, 0.7f, 1f);
			public static System.Numerics.Vector4 LunarisGrey3 = new(0.5f, 0.5f, 0.5f, 1f);
			public static System.Numerics.Vector4 LunarisWhite = new(1f, 1f, 1f, 1f);
			public static System.Numerics.Vector4 LunarisWhite2 = new(0.878f, 0.878f, 0.878f, 1f);
			public static System.Numerics.Vector4 LunarisOrange = new(1f, 0.709f, 0f, 1f);
			public static System.Numerics.Vector4 LunarisYellow = new(1f, 1f, .4f, 1f);
			public static System.Numerics.Vector4 LunarisViolet = new(0.770f, 0.700f, 0.965f, 1.000f);
			public static System.Numerics.Vector4 TankBlue = new(0f, 0.6f, 1f, 1f);
			public static System.Numerics.Vector4 HealerGreen = new(0f, 0.8f, 0.1333333f, 1f);
			public static System.Numerics.Vector4 DPSRed = new(0.7058824f, 0f, 0f, 1f);
			public static System.Numerics.Vector4 ParsedGrey = new(0.4f, 0.4f, 0.4f, 1f);
			public static System.Numerics.Vector4 ParsedGreen = new(0.117f, 1f, 0f, 1f);
			public static System.Numerics.Vector4 ParsedBlue = new(0f, 0.439f, 1f, 1f);
			public static System.Numerics.Vector4 ParsedPurple = new(0.639f, 0.207f, 0.933f, 1f);
			public static System.Numerics.Vector4 ParsedOrange = new(1f, 0.501f, 0f, 1f);
			public static System.Numerics.Vector4 ParsedPink = new(0.886f, 0.407f, 0.658f, 1f);
			public static System.Numerics.Vector4 ParsedGold = new(0.898f, 0.8f, 0.501f, 1f);
		}
	}
}
