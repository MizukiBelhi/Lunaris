using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using UnityEngine;
using static Lunaris.PluginInstaller;
using Lunaris.Config;
using Lunaris.IGUI;

namespace Lunaris
{
	internal class PluginOptions
	{
		private static readonly List<PluginListItem> _plugins = [];
		private static (string Plugin, string Handle, string Prop)? _capturing = null;
		private static readonly HashSet<KeyCode> _captureKeys = [];
		private static readonly Dictionary<string, float> _sizeCache = [];
		private static readonly Dictionary<string, List<_SECTIONSTORE>> _sectionCache = [];

		public static void Add(PluginListItem plugin)
		{
			if (!_plugins.Contains(plugin))
				_plugins.Add(plugin);
			plugin.IsOptionsOpen = true;
		}

		public static void Close(string pluginId, string pluginName = null)
		{
			var plugins = _plugins.Where(p => p.desc?.Id == pluginId || p.desc?.Manifest?.Id == pluginId).ToList();
			foreach (var plugin in plugins)
			{
				plugin.IsOptionsOpen = false;
				_plugins.Remove(plugin);
			}

			if (_capturing?.Plugin == pluginId || (!string.IsNullOrEmpty(pluginName) && _capturing?.Plugin == pluginName))
			{
				_capturing = null;
				_captureKeys.Clear();
			}
		}

		public static void Draw()
		{
			var copy = new List<PluginListItem>(_plugins);
			foreach (var plugin in copy)
			{
				DrawOptions(plugin, ref plugin.IsOptionsOpen);
				if (!plugin.IsOptionsOpen)
					_plugins.Remove(plugin);
			}
		}

		private static string NormalizeSection(string s)
		{
			if (string.IsNullOrEmpty(s) || s.Equals("Default", StringComparison.OrdinalIgnoreCase))
				return "General";
			return s;
		}

		private static void EnsureSectionCache(string pluginName)
		{
			if (_sectionCache.ContainsKey(pluginName)) return;

			var cfg = ConfigHandler.Get(pluginName);
			if (cfg == null) { _sectionCache[pluginName] = []; return; }

			var settings = cfg.GetSettings().ToList();
			var hidden = ((ConfigInstance)cfg).GetHiddenKeys();
			var descs = ((ConfigInstance)cfg).GetDescs();
			var ranges = ((ConfigInstance)cfg).GetRanges();
			var sections = ((ConfigInstance)cfg).GetSections();

			var sc = new List<_SECTIONSTORE>();

			for (int i = 0; i < settings.Count; i++)
			{
				var set = settings[i];
				if (set.Key.StartsWith("KB.")) continue;
				if (set.Key.StartsWith("__")) continue; // internal/meta keys
				if (hidden.Contains(set.Key)) continue;

				var desc = descs.ContainsKey(set.Key) ? descs[set.Key] : "";
				var sect = sections.ContainsKey(set.Key) ? sections[set.Key] : "Default";

				var selSC = sc.FirstOrDefault(s => s.sectionName == sect);
				if (selSC != null)
				{
					if (ranges.ContainsKey(set.Key))
						selSC.ranges[set.Key] = ranges[set.Key];
					selSC.descs[set.Key] = desc;
					selSC.settings.Add(new KeyValuePair<string, object>(set.Key, set.Value));
				}
				else
				{
					sc.Add(new _SECTIONSTORE
					{
						sectionName = sect,
						settings = [new KeyValuePair<string, object>(set.Key, set.Value)],
						descs = new Dictionary<string, string> { [set.Key] = desc },
						ranges = ranges.ContainsKey(set.Key) ? new Dictionary<string, (float, float)> { [set.Key] = ranges[set.Key] } : []
					});
				}
			}

			_sectionCache[pluginName] = sc;
		}

		private static List<string> CollectAllSections(string pluginName, IReadOnlyList<IConfigHandleInternal> handles)
		{
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var result = new List<string>();

			var handledKeys = handles.SelectMany(h => h.GetMemberInfos().Select(m => m.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);

			foreach (var handle in handles)
			{
				if (handle.GetMemberInfos() == null || handle.GetMemberInfos().Count == 0) continue;
				var members = handle.GetMemberInfos().Where(m => !m.Hidden).ToList();
				if (members.Count == 0) continue;

				foreach (var grp in members.GroupBy(m => m.Section))
				{
					var key = NormalizeSection(grp.Key);
					if (seen.Add(key)) result.Add(key);
				}
			}
			if (_sectionCache.TryGetValue(pluginName, out var cached))
			{
				foreach (var s in cached)
				{
					if (s.settings == null || s.settings.Count == 0) continue;

					bool hasUnhandled = s.settings.Any(set =>
					{
						if (set.Key.StartsWith("KB.")) return false;
						var kk = set.Key.Contains('.') ? set.Key.Substring(set.Key.IndexOf('.') + 1) : set.Key;
						return !handledKeys.Contains(kk);
					});

					if (!hasUnhandled) continue;

					var key = NormalizeSection(s.sectionName);
					if (seen.Add(key)) result.Add(key);
				}
			}

			return result;
		}

		private static void DrawOptions(PluginListItem plugin, ref bool open)
		{
			var sanName = plugin.desc.SetPluginName.Replace(" ", "").ToLower();

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(640, 420), ImGuiCond.FirstUseEver);
			if (!ImGui.Begin($"{plugin.pluginName} Options", ref open, ImGuiWindowFlags.NoCollapse))
			{
				ImGui.End();
				return;
			}

			var handles = ConfigHandler.GetHandles(sanName);

			// Calc the width of the longest label so fields stay aligned.
			if (!_sizeCache.ContainsKey(sanName))
			{
				_sizeCache[sanName] = -9999;
				var cfg = ConfigHandler.Get(sanName);
				if (cfg != null)
				{
					var settings = cfg.GetSettings().ToList();
					var handledKeys = handles.SelectMany(h => h.GetMemberInfos().Select(m => m.Name))
											 .ToHashSet(StringComparer.OrdinalIgnoreCase);

					foreach (var set in settings)
					{
						if (set.Key.StartsWith("KB.")) continue;
						if (set.Key.StartsWith("__")) continue;
						if (((ConfigInstance)cfg).GetHiddenKeys().Contains(set.Key)) continue;
						var kk = set.Key.Contains('.') ? set.Key.Substring(set.Key.IndexOf('.') + 1) : set.Key;
						if (handledKeys.Contains(kk)) continue;
						var n = ImGui.CalcTextSize(set.Key).X;
						if (_sizeCache[sanName] < n) _sizeCache[sanName] = n;
					}
				}

				foreach (var handle in handles)
				{
					if (handle.GetMemberInfos() == null || handle.GetMemberInfos().Count == 0) continue;
					foreach (var m in handle.GetMemberInfos().Where(m => !m.Hidden))
					{
						var n = ImGui.CalcTextSize(m.Label).X;
						if (_sizeCache[sanName] < n) _sizeCache[sanName] = n;
					}
				}

				_sizeCache[sanName] += 20;
			}

			EnsureSectionCache(sanName);

			var allSections = CollectAllSections(sanName, handles);
			var footerHeight = ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().ItemSpacing.Y;

			ImGui.BeginChild($"##optcnt{sanName}", new System.Numerics.Vector2(0, -footerHeight), false);

			if (ImGui.BeginTabBar($"##opttabs{sanName}"))
			{
				foreach (var sectionName in allSections)
				{
					if (ImGui.BeginTabItem(sectionName))
					{
						if (ImGui.BeginChild($"##opttabs2{sanName}", new System.Numerics.Vector2(0, 0)))
						{
							if (handles.Count > 0)
								DrawTypedHandlesSection(sanName, handles, sectionName);

							DrawRawFallbackSection(sanName, handles, sectionName);
							ImGui.EndChild();
						}

						ImGui.EndTabItem();
					}
				}
				ImGui.EndTabBar();
			}

			ImGui.EndChild();

			DrawFooter(plugin, sanName, ref open);

			ImGui.End();
		}

		private static void DrawTypedHandlesSection(string pluginName, IReadOnlyList<IConfigHandleInternal> handles, string sectionFilter)
		{
			var conflicts = ConfigHandler.GetKeybindConflicts();

			foreach (var handle in handles)
			{
				if (handle.GetMemberInfos() == null || handle.GetMemberInfos().Count == 0) continue;

				var members = handle.GetMemberInfos().Where(m => !m.Hidden).ToList();
				if (members.Count == 0) continue;

				var filtered = members.Where(m => NormalizeSection(m.Section).Equals(sectionFilter, StringComparison.OrdinalIgnoreCase)).ToList();
				if (filtered.Count == 0) continue;

				for (int i = 0; i < filtered.Count; i++)
				{
					var m = filtered[i];

					ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
					ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
					if (m.Tooltip != null)
						ImGui.TextWrapped(m.Tooltip);
					ImGui.PopStyleColor();
					ImGui.Text($"{m.Label}:");
					ImGui.SameLine();
					ImGui.SetCursorPosX(_sizeCache[pluginName]);

					if (!IsComplexType(m.Type) && m.IsKeybind && handle.GetKeybinds().TryGetValue(m.Name, out var kb))
					{
						bool hasConflict = conflicts.Values.Any(list => list.Count > 1 && list.Any(e => e.Plugin == pluginName && e.Property == m.Name));
						DrawKeybindField(pluginName, handle.ConfigTypeName, m.Name, kb, hasConflict, newKeys => handle.SetProperty(m.Name, newKeys));
					}
					else
					{
						DrawField($"##{pluginName}_{handle.ConfigTypeName}_{m.Name}",
							handle.GetPropertyValue(m.Name),
							IsComplexType(m.Type) ? null : m.Range,
							updated => handle.SetProperty(m.Name, updated),
							inTable: true);
					}
				}
			}
		}

		private static void DrawRawFallbackSection(string pluginName, IReadOnlyList<IConfigHandleInternal> handles, string sectionFilter)
		{
			var cfg = ConfigHandler.Get(pluginName);
			if (cfg == null) return;

			if (!_sectionCache.TryGetValue(pluginName, out var allCached)) return;

			var handledKeys = handles.SelectMany(h => h.GetMemberInfos().Select(m => m.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);

			foreach (var sectionVar in allCached)
			{
				if (!NormalizeSection(sectionVar.sectionName).Equals(sectionFilter, StringComparison.OrdinalIgnoreCase)) continue;

				if (sectionVar.settings == null || sectionVar.settings.Count == 0) continue;

				for (int i = 0; i < sectionVar.settings.Count; i++)
				{
					var set = sectionVar.settings[i];
					if (set.Key.StartsWith("KB.")) continue;

					var kk = set.Key.Contains('.') ? set.Key.Substring(set.Key.IndexOf('.') + 1) : set.Key;
					if (handledKeys.Contains(kk)) continue;

					var desc = sectionVar.descs.ContainsKey(set.Key) ? sectionVar.descs[set.Key] : "";

					ConfigRangeAttribute range = null;
					if (sectionVar.ranges.ContainsKey(set.Key))
						range = new(sectionVar.ranges[set.Key].Item1, sectionVar.ranges[set.Key].Item2);

					ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
					ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
					if (!string.IsNullOrEmpty(desc))
						ImGui.TextWrapped(desc);
					ImGui.PopStyleColor();
					ImGui.Text($"{set.Key.Replace("KB.", "")}:");
					ImGui.SameLine();
					ImGui.SetCursorPosX(_sizeCache[pluginName]);

					DrawField(set.Key, set.Value, range, updated => WriteCachedSetting(cfg, sectionVar, i, set.Key, updated), true);
				}
			}
		}

		private static void WriteCachedSetting(IConfig cfg, _SECTIONSTORE section, int index, string key, object value)
		{
			cfg.Write(key, value);
			section.settings[index] = new KeyValuePair<string, object>(key, value);
		}

		private static void InvalidateCache(string pluginName)
		{
			_sizeCache.Remove(pluginName);
			_sectionCache.Remove(pluginName);
		}

		internal static void DrawFooter(PluginListItem plugin, string sanName, ref bool opn)
		{
			var windowSize = ImGui.GetWindowSize();
			var placeholderButtonSize = ImGui.CalcTextSize("placeholder") + (ImGui.GetStyle().FramePadding * 2);

			ImGui.Separator();

			ImGui.SetCursorPosY(windowSize.Y - placeholderButtonSize.Y - 10);

			//ImGui.Dummy(placeholderButtonSize);



			//ImGui.SameLine();
			if (ImGui.Button($"Reset##optionReset{plugin.pluginName}"))
				ImGui.OpenPopup($"##ResetConfirm_{sanName}");

			bool dummy = true;
			if (ImGui.BeginPopupModal($"##ResetConfirm_{sanName}", ref dummy, ImGuiWindowFlags.AlwaysAutoResize))
			{
				ImGui.Text("Restore all settings to defaults?\nThis cannot be undone.\n\n");
				ImGui.Separator();

				if (ImGui.Button("OK", new System.Numerics.Vector2(120, 0)))
				{
					ConfigHandler.Get(sanName)?.Reset();
					InvalidateCache(sanName);
					ImGui.CloseCurrentPopup();
				}

				ImGui.SetItemDefaultFocus();
				ImGui.SameLine();

				if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
					ImGui.CloseCurrentPopup();

				ImGui.EndPopup();
			}

			var closeText = "Close";
			var closeButtonSize = ImGui.CalcTextSize(closeText) + (ImGui.GetStyle().FramePadding * 2);
			ImGui.SameLine(windowSize.X - closeButtonSize.X - 20);

			if (ImGui.Button(closeText))
			{
				opn = false;
			}
		}

		private static bool IsComplexType(Type t)
		{
			if (t == typeof(string)) return false;
			if (t.IsPrimitive) return false;
			if (t.IsEnum) return false;
			if (t == typeof(UnityEngine.Color)) return false;
			if (t == typeof(UnityEngine.Vector2)) return false;
			if (t == typeof(UnityEngine.Vector3)) return false;
			if (t == typeof(UnityEngine.Vector4)) return false;
			if (t == typeof(UnityEngine.Quaternion)) return false;
			if (typeof(IKeybind).IsAssignableFrom(t)) return false;
			if (typeof(System.Collections.IList).IsAssignableFrom(t)) return false;
			if (!t.IsValueType) return true;
			return false;
		}

		private class _SECTIONSTORE
		{
			public string sectionName;
			public List<KeyValuePair<string, object>> settings;
			public Dictionary<string, string> descs;
			public Dictionary<string, (float, float)> ranges;
		}

		public static void DrawKeybindField(string plugin, string handleType, string propName, KeybindEntry kb, bool hasConflict, Action<KeyCode[]> onChanged)
		{
			var captureId = (plugin, handleType, propName);
			bool isCapturing = _capturing == captureId;

			if (hasConflict)
				ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.DPSRed);


			float btnWidth = ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X - 32f;
			if (isCapturing)
			{
				ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.3f, 0.5f, 0.9f, 1f));
				ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.3f, 0.5f, 0.9f, 1f));

				foreach (var kc in _captureableCodes)
				{
					if (IsModifier(kc)) continue;
					var ik = KeyCodeToImGuiKey(kc);
					if (ik == ImGuiKey.None) continue;
					if (ImGui.IsKeyDown(ik))
						_captureKeys.Add(kc);
				}

				if (ImGuiWrap.LShift) _captureKeys.Add(KeyCode.LeftShift);
				if (ImGuiWrap.RShift) _captureKeys.Add(KeyCode.RightShift);
				if (ImGuiWrap.LCtrl) _captureKeys.Add(KeyCode.LeftControl);
				if (ImGuiWrap.RCtrl) _captureKeys.Add(KeyCode.RightControl);
				if (ImGuiWrap.LAlt) _captureKeys.Add(KeyCode.LeftAlt);
				if (ImGuiWrap.RAlt) _captureKeys.Add(KeyCode.RightAlt);


				bool hasNonModifier = _captureKeys.Any(k => !IsModifier(k));
				bool escape = ImGui.IsKeyPressed(ImGuiKey.Escape);
				bool confirm = hasNonModifier && _captureKeys.Any(kc => {
					var ik = KeyCodeToImGuiKey(kc);
					return ik != ImGuiKey.None && !IsModifier(kc) && ImGui.IsKeyReleased(ik);
				});

				string btnLabel = _captureKeys.Count > 0 ? string.Join(" + ", _captureKeys.OrderByDescending(k => IsModifier(k)).ThenBy(k => (int)k).Select(KeycodeDisplayName)) : "Press keys...";

				if (ImGui.Button($"{btnLabel}##kbcap_{propName}", new System.Numerics.Vector2(btnWidth, 0)) || confirm || escape)
				{
					if (!escape)
					{
						var finalKeys = _captureKeys.Where(k => k != KeyCode.Return).OrderByDescending(k => IsModifier(k)).ThenBy(k => (int)k).ToArray();
						if (finalKeys.Length > 0)
							onChanged(finalKeys);
					}

					_capturing = null;
					_captureKeys.Clear();
				}


				ImGui.PopStyleColor(2);
			}
			else
			{
				string display = kb.DisplayString;
				if (ImGui.Button($"{display}##kb_{propName}", new System.Numerics.Vector2(btnWidth, 0)))
				{
					_capturing = captureId;
					_captureKeys.Clear();
				}

				if (hasConflict && ImGui.IsItemHovered())
					ImGui.SetTooltip("Keybind conflict with another plugin.");
			}

			ImGui.SameLine();


			if (UI.IconButton(UI.ToIconString(FontAwesomeIcon.X), UI.LunarisColors.DPSRed * 0.8f, UI.LunarisColors.DPSRed * 0.9f, UI.LunarisColors.DPSRed * 1.2f))
				onChanged([]);

			if (hasConflict)
				ImGui.PopStyleColor();
		}


		internal static void DrawField(string label, object val, ConfigRangeAttribute range, Action<object> onWrite, bool inTable = false, int tableStartColumn = 0)
		{
			if (val == null) { ImGui.TextDisabled("null"); return; }

			ImGui.PushID(label);
			bool changed = false;
			string id = $"##{label}";

			switch (val)
			{
				case bool b:
				if (ImGui.Checkbox(id, ref b)) { val = b; changed = true; }
				break;

				case int i:
				if (range != null)
				{
					if (ImGui.SliderInt(id, ref i, (int)range.Min, (int)range.Max)) { val = i; changed = true; }
				}
				else
				{
					if (ImGui.InputInt(id, ref i)) { val = i; changed = true; }
				}
				break;

				case long l:
				var li = (int)l;
				if (range != null)
				{
					if (ImGui.SliderInt(id, ref li, (int)range.Min, (int)range.Max)) { val = li; changed = true; }
				}
				else
				{
					if (ImGui.InputInt(id, ref li)) { val = li; changed = true; }
				}
				break;

				case float f:
				if (range != null)
				{
					if (ImGui.SliderFloat(id, ref f, range.Min, range.Max)) { val = f; changed = true; }
				}
				else
				{
					if (ImGui.DragFloat(id, ref f, 0.05f)) { val = f; changed = true; }
				}
				break;

				case double d:
				var df = (float)d;
				if (range != null)
				{
					if (ImGui.SliderFloat(id, ref df, range.Min, range.Max)) { val = df; changed = true; }
				}
				else
				{
					if (ImGui.DragFloat(id, ref df, 0.05f)) { val = df; changed = true; }
				}
				break;

				case decimal dm:
				var dmf = (float)dm;
				if (range != null)
				{
					if (ImGui.SliderFloat(id, ref dmf, range.Min, range.Max)) { val = dmf; changed = true; }
				}
				else
				{
					if (ImGui.DragFloat(id, ref dmf, 0.05f)) { val = dmf; changed = true; }
				}
				break;

				case string s:
				if (ImGui.InputText(id, ref s, 512)) { val = s; changed = true; }
				break;

				case UnityEngine.Vector2 v2:
				var sv2 = new System.Numerics.Vector2(v2.x, v2.y);
				if (ImGui.DragFloat2(id, ref sv2, 0.05f)) { val = new UnityEngine.Vector2(sv2.X, sv2.Y); changed = true; }
				break;

				case UnityEngine.Vector3 v3:
				var sv3 = new System.Numerics.Vector3(v3.x, v3.y, v3.z);
				if (ImGui.DragFloat3(id, ref sv3, 0.05f)) { val = new UnityEngine.Vector3(sv3.X, sv3.Y, sv3.Z); changed = true; }
				break;

				case UnityEngine.Vector4 v4:
				var sv4 = new System.Numerics.Vector4(v4.x, v4.y, v4.z, v4.w);
				if (ImGui.DragFloat4(id, ref sv4, 0.05f)) { val = new UnityEngine.Vector4(sv4.X, sv4.Y, sv4.Z, sv4.W); changed = true; }
				break;

				case UnityEngine.Quaternion q:
				var euler = q.eulerAngles;
				var se = new System.Numerics.Vector3(euler.x, euler.y, euler.z);
				if (ImGui.DragFloat3($"{id}_euler", ref se, 0.1f)) { val = UnityEngine.Quaternion.Euler(se.X, se.Y, se.Z); changed = true; }
				break;

				case UnityEngine.Color col:
				var sc = new System.Numerics.Vector4(col.r, col.g, col.b, col.a);
				if (ImGui.ColorEdit4(id, ref sc, ImGuiColorEditFlags.AlphaBar | ImGuiColorEditFlags.AlphaPreview))
				{ val = new UnityEngine.Color(sc.X, sc.Y, sc.Z, sc.W); changed = true; }
				break;

				case Enum e:
				string[] names = Enum.GetNames(e.GetType());
				int selected = Array.IndexOf(names, e.ToString());
				if (ImGui.Combo(id, ref selected, names, names.Length))
				{ val = Enum.Parse(e.GetType(), names[selected]); changed = true; }
				break;

				case System.Collections.IList list:
				if (ImGui.TreeNode($"[{list.Count}]##{label}"))
				{
					for (int j = 0; j < list.Count; j++)
					{
						if (inTable)
						{
							//ImGui.TableNextRow();
							//ImGui.TableSetColumnIndex(tableStartColumn);
							ImGui.Text($"  [{j}]");
							//ImGui.TableSetColumnIndex(tableStartColumn + 1);
							ImGui.PushItemWidth(-1);
							DrawField($"({label}_{j})", list[j], null,
								newVal => { list[j] = newVal; onWrite(list); },
								inTable: true, tableStartColumn: tableStartColumn);
							ImGui.PopItemWidth();
						}
					}
					ImGui.TreePop();
				}
				break;

				default:
				if (!val.GetType().IsValueType)
				{
					if (ImGui.TreeNodeEx($"{label}##obj", ImGuiTreeNodeFlags.SpanFullWidth))
					{
						foreach (var field in val.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
						{
							ImGui.Text($"  {field.Name}");
							ImGui.SameLine();
							ImGui.PushItemWidth(200f);
							DrawField(field.Name, field.GetValue(val), null, newVal => { field.SetValue(val, newVal); onWrite(val); });
							ImGui.PopItemWidth();
						}
						ImGui.TreePop();
					}
				}
				else
				{
					if (val == null)
						ImGui.TextDisabled("null");
					else
						ImGui.TextDisabled(val.ToString());
				}
				break;
			}

			if (changed) onWrite(val);
			ImGui.PopID();
		}


		private static readonly KeyCode[] _captureableCodes = Enum.GetValues(typeof(KeyCode)).Cast<KeyCode>().Where(kc => kc != KeyCode.None && KeyCodeToImGuiKey(kc) != ImGuiKey.None).ToArray();

		private static string KeycodeDisplayName(KeyCode k) => k switch
		{
			KeyCode.LeftShift => "LShift",
			KeyCode.RightShift => "RShift",
			KeyCode.LeftControl => "LCtrl",
			KeyCode.RightControl => "RCtrl",
			KeyCode.LeftAlt => "LAlt",
			KeyCode.RightAlt => "RAlt",
			KeyCode.Return => "Enter",
			KeyCode.BackQuote => "`",
			KeyCode.Minus => "-",
			KeyCode.Equals => "=",
			KeyCode.LeftBracket => "[",
			KeyCode.RightBracket => "]",
			KeyCode.Backslash => "\\",
			KeyCode.Semicolon => ";",
			KeyCode.Quote => "'",
			KeyCode.Comma => ",",
			KeyCode.Period => ".",
			KeyCode.Slash => "/",
			_ => k.ToString()
		};

		private static bool IsModifier(KeyCode kc) => kc is KeyCode.LeftShift or KeyCode.RightShift or KeyCode.LeftControl or KeyCode.RightControl or
													KeyCode.LeftAlt or KeyCode.RightAlt or KeyCode.LeftWindows or KeyCode.RightWindows;


		private static ImGuiKey KeyCodeToImGuiKey(KeyCode kc) => kc switch
		{
			KeyCode.Tab => ImGuiKey.Tab,
			KeyCode.LeftArrow => ImGuiKey.LeftArrow,
			KeyCode.RightArrow => ImGuiKey.RightArrow,
			KeyCode.UpArrow => ImGuiKey.UpArrow,
			KeyCode.DownArrow => ImGuiKey.DownArrow,
			KeyCode.PageUp => ImGuiKey.PageUp,
			KeyCode.PageDown => ImGuiKey.PageDown,
			KeyCode.Home => ImGuiKey.Home,
			KeyCode.End => ImGuiKey.End,
			KeyCode.Insert => ImGuiKey.Insert,
			KeyCode.Delete => ImGuiKey.Delete,
			KeyCode.Backspace => ImGuiKey.Backspace,
			KeyCode.Space => ImGuiKey.Space,
			KeyCode.Return => ImGuiKey.Enter,
			KeyCode.Escape => ImGuiKey.Escape,
			KeyCode.Quote => ImGuiKey.Apostrophe,
			KeyCode.Comma => ImGuiKey.Comma,
			KeyCode.Minus => ImGuiKey.Minus,
			KeyCode.Period => ImGuiKey.Period,
			KeyCode.Slash => ImGuiKey.Slash,
			KeyCode.Semicolon => ImGuiKey.Semicolon,
			KeyCode.Equals => ImGuiKey.Equal,
			KeyCode.LeftBracket => ImGuiKey.LeftBracket,
			KeyCode.Backslash => ImGuiKey.Backslash,
			KeyCode.RightBracket => ImGuiKey.RightBracket,
			KeyCode.BackQuote => ImGuiKey.GraveAccent,
			KeyCode.CapsLock => ImGuiKey.CapsLock,
			KeyCode.ScrollLock => ImGuiKey.ScrollLock,
			KeyCode.Numlock => ImGuiKey.NumLock,
			KeyCode.Print => ImGuiKey.PrintScreen,
			KeyCode.Pause => ImGuiKey.Pause,
			KeyCode.Keypad0 => ImGuiKey.Keypad0,
			KeyCode.Keypad1 => ImGuiKey.Keypad1,
			KeyCode.Keypad2 => ImGuiKey.Keypad2,
			KeyCode.Keypad3 => ImGuiKey.Keypad3,
			KeyCode.Keypad4 => ImGuiKey.Keypad4,
			KeyCode.Keypad5 => ImGuiKey.Keypad5,
			KeyCode.Keypad6 => ImGuiKey.Keypad6,
			KeyCode.Keypad7 => ImGuiKey.Keypad7,
			KeyCode.Keypad8 => ImGuiKey.Keypad8,
			KeyCode.Keypad9 => ImGuiKey.Keypad9,
			KeyCode.KeypadPeriod => ImGuiKey.KeypadDecimal,
			KeyCode.KeypadDivide => ImGuiKey.KeypadDivide,
			KeyCode.KeypadMultiply => ImGuiKey.KeypadMultiply,
			KeyCode.KeypadMinus => ImGuiKey.KeypadSubtract,
			KeyCode.KeypadPlus => ImGuiKey.KeypadAdd,
			KeyCode.KeypadEnter => ImGuiKey.KeypadEnter,
			KeyCode.LeftShift => ImGuiKey.LeftShift,
			KeyCode.LeftControl => ImGuiKey.LeftCtrl,
			KeyCode.LeftAlt => ImGuiKey.LeftAlt,
			KeyCode.LeftWindows => ImGuiKey.LeftSuper,
			KeyCode.RightShift => ImGuiKey.RightShift,
			KeyCode.RightControl => ImGuiKey.RightCtrl,
			KeyCode.RightAlt => ImGuiKey.RightAlt,
			KeyCode.RightWindows => ImGuiKey.RightSuper,
			KeyCode.Alpha0 => ImGuiKey._0,
			KeyCode.Alpha1 => ImGuiKey._1,
			KeyCode.Alpha2 => ImGuiKey._2,
			KeyCode.Alpha3 => ImGuiKey._3,
			KeyCode.Alpha4 => ImGuiKey._4,
			KeyCode.Alpha5 => ImGuiKey._5,
			KeyCode.Alpha6 => ImGuiKey._6,
			KeyCode.Alpha7 => ImGuiKey._7,
			KeyCode.Alpha8 => ImGuiKey._8,
			KeyCode.Alpha9 => ImGuiKey._9,
			KeyCode.A => ImGuiKey.A,
			KeyCode.B => ImGuiKey.B,
			KeyCode.C => ImGuiKey.C,
			KeyCode.D => ImGuiKey.D,
			KeyCode.E => ImGuiKey.E,
			KeyCode.F => ImGuiKey.F,
			KeyCode.G => ImGuiKey.G,
			KeyCode.H => ImGuiKey.H,
			KeyCode.I => ImGuiKey.I,
			KeyCode.J => ImGuiKey.J,
			KeyCode.K => ImGuiKey.K,
			KeyCode.L => ImGuiKey.L,
			KeyCode.M => ImGuiKey.M,
			KeyCode.N => ImGuiKey.N,
			KeyCode.O => ImGuiKey.O,
			KeyCode.P => ImGuiKey.P,
			KeyCode.Q => ImGuiKey.Q,
			KeyCode.R => ImGuiKey.R,
			KeyCode.S => ImGuiKey.S,
			KeyCode.T => ImGuiKey.T,
			KeyCode.U => ImGuiKey.U,
			KeyCode.V => ImGuiKey.V,
			KeyCode.W => ImGuiKey.W,
			KeyCode.X => ImGuiKey.X,
			KeyCode.Y => ImGuiKey.Y,
			KeyCode.Z => ImGuiKey.Z,
			KeyCode.F1 => ImGuiKey.F1,
			KeyCode.F2 => ImGuiKey.F2,
			KeyCode.F3 => ImGuiKey.F3,
			KeyCode.F4 => ImGuiKey.F4,
			KeyCode.F5 => ImGuiKey.F5,
			KeyCode.F6 => ImGuiKey.F6,
			KeyCode.F7 => ImGuiKey.F7,
			KeyCode.F8 => ImGuiKey.F8,
			KeyCode.F9 => ImGuiKey.F9,
			KeyCode.F10 => ImGuiKey.F10,
			KeyCode.F11 => ImGuiKey.F11,
			KeyCode.F12 => ImGuiKey.F12,
			_ => ImGuiKey.None
		};
	}
}
