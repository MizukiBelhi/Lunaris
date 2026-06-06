using ImGuiNET;
using Lunaris.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using static Lunaris.PluginInstaller;

namespace Lunaris
{
	internal static class PluginLinker
	{
		private static PluginListItem _target;
		private static string _search = "";
		private static PluginListItem _hovered;
		private static bool _open = false;

		internal static void Open(PluginListItem localPlugin)
		{
			_target = localPlugin;
			_search = "";
			_hovered = null;
			_open = true;
		}

		internal static void Draw()
		{
			if (!_open || _target == null) return;

			ImGui.SetNextWindowSize(new Vector2(540, 440), ImGuiCond.FirstUseEver);
			ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.FirstUseEver, new Vector2(0.5f, 0.5f));

			if (!ImGui.Begin($"Link Plugin###PluginLinker", ref _open, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
			{
				ImGui.End();
				return;
			}

			ImGui.TextDisabled("Select the entry that matches.");
			ImGui.TextDisabled($"Plugin: ");
			ImGui.SameLine();
			ImGui.Text(_target.pluginName);
			ImGui.Spacing();
			ImGui.Separator();
			ImGui.Spacing();


			ImGui.SetNextItemWidth(-1);
			ImGui.InputTextWithHint("##linksearch", "Search...", ref _search, 128);
			ImGui.Spacing();


			var available = UI.installer.pluginsAvailable;
			var filtered = string.IsNullOrEmpty(_search) ? available : [.. available.Where(p => p.pluginName.Contains(_search, StringComparison.OrdinalIgnoreCase) ||
							(p.manifest?.Author?.Contains(_search, StringComparison.OrdinalIgnoreCase) ?? false))];

			ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.1f, 1f));
			if (ImGui.BeginChild("##linklist", new Vector2(-1, -50)))
			{
				if (filtered.Count == 0)
				{
					var size = ImGui.GetContentRegionAvail();
					ImGui.SetCursorPos(new Vector2(size.X * 0.5f - 60f, size.Y * 0.5f));
					ImGui.TextDisabled("No results found.");
				}
				else
				{
					foreach (var item in filtered)
						DrawEntry(item);
				}

				ImGui.EndChild();
			}
			ImGui.PopStyleColor();

			ImGui.Spacing();

			ImGui.BeginDisabled(_hovered == null);
			if (ImGui.Button("Link", new Vector2(120, 0)))
			{
				LinkPlugins(_target, _hovered);
				_open = false;
			}
			ImGui.EndDisabled();

			ImGui.SameLine();
			if (ImGui.Button("Cancel", new Vector2(120, 0)))
				_open = false;

			/*if (_hovered != null)
			{
				ImGui.SameLine();
				ImGui.TextDisabled($"{_hovered.pluginName} by {_hovered.manifest?.Author ?? "Unknown"}");
			}*/

			ImGui.End();
		}

		private static void DrawEntry(PluginListItem item)
		{
			bool selected = _hovered == item;

			if (item.icon != null)
			{
				ImGuiWrap.RegisterTexture(item.icon);
				ImGui.Image(item.icon.GetNativeTexturePtr(), new Vector2(32, 32));
			}
			else
				ImGui.Image(UI.DefaultIcon, new Vector2(32, 32));

			ImGui.SameLine();

			float contentX = ImGui.GetContentRegionAvail().X;
			ImGui.BeginGroup();
			ImGui.Text(item.pluginName);
			ImGui.SameLine(contentX - 80f);

			if (item.manifest != null)
			{
				var tagStr = item.manifest.Tags.ToString();
				ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.8f, 1f, 1f));
				ImGui.TextDisabled(tagStr);
				ImGui.PopStyleColor();
			}

			ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1f));
			ImGui.TextUnformatted($"by {item.manifest?.Author ?? "Unknown"}");
			ImGui.SameLine();
			if (item.manifest != null && item.manifest.AllVersions != null && item.manifest.AllVersions.Count > 1)
			{
				float maxWidth = item.manifest.AllVersions.Max(v => ImGui.CalcTextSize("v" + v).X) + ImGui.GetStyle().FramePadding.X * 2 - 3;
				ImGui.SetNextItemWidth(maxWidth);
				var cursor = ImGui.GetCursorPos();
				ImGui.SetCursorPosY(cursor.Y+3);
				ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(ImGui.GetStyle().FramePadding.X, 0));
				if (ImGui.BeginCombo("", "v"+item.SelectedVersion, ImGuiComboFlags.NoArrowButton))
				{
					foreach (var v in item.manifest.AllVersions)
					{
						if (v == item.SelectedVersion)
							continue;

						if (ImGui.Selectable("v"+v))
						{
							item.SelectedVersion = v;
						}
					}

					ImGui.EndCombo();
				}
				ImGui.PopStyleVar();
			}
			else
			{
				ImGui.TextUnformatted($"v {item.manifest?.Version ?? "0.0.0"}");
			}


			ImGui.PopStyleColor();
			ImGui.EndGroup();

			var min = ImGui.GetItemRectMin() - new Vector2(36, 2);
			var max = new Vector2(ImGui.GetItemRectMax().X + ImGui.GetStyle().ItemSpacing.X, ImGui.GetItemRectMax().Y + 2);
			var dl = ImGui.GetWindowDrawList();

			if (selected)
				dl.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.1f)), 4f);

			ImGui.SetCursorScreenPos(min);
			if (ImGui.InvisibleButton($"##linkentry_{item.pluginName}", max - min))
				_hovered = item;

			if (ImGui.IsItemHovered())
				dl.AddRect(min, max, ImGui.GetColorU32(new Vector4(0.4f, 0.6f, 1f, 0.4f)), 4f);

			ImGui.Spacing();
		}

		private static void LinkPlugins(PluginListItem local, PluginListItem remote)
		{
			if (local == null || remote == null) return;


			if (!string.IsNullOrEmpty(local.desc.OriginalFilePath) && System.IO.File.Exists(local.desc.OriginalFilePath))
			{
				var bytes = System.IO.File.ReadAllBytes(local.desc.OriginalFilePath);
				bytes = PluginAssemblyUtils.InjectGuid(bytes, remote.manifest.Id);
				System.IO.File.WriteAllBytes(local.desc.OriginalFilePath, bytes);
			}

			
			local.manifest = remote.manifest;
			local.manifest.IsEnabled = false;
			local.icon = remote.icon;
			local.IsInstalled = true;
			local.IsOpen = false;
			local.IsLoaded = false;
			local.manifest.FilePath = local.desc.OriginalFilePath;

			local.desc.Version = local.manifest.Version;
			local.desc.Description = local.manifest.Description;
			//local.desc.Name = local.manifest.Name;
			local.desc.Author = local.manifest.Author;
			local.desc.IsLoaded = false;
			local.desc.Manifest = local.manifest;
			local.desc.Id = local.manifest.Id;
			local.desc.Version = remote.SelectedVersion;

			local.pluginName = remote.pluginName;

			UI.installer.pluginsAvailable.Remove(remote);

			PluginManifestHandler.StoreManifest(local.manifest);

			Notifications.Add(new Notification(NotificationType.Info, $"Linked {local.pluginName}", Notifications.DefaultDuration));
		}
	}
}