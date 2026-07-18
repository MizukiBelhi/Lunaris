using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Lunaris.Message;
using ImGuiNET;
using Lunaris.Config;
using System.Threading.Tasks;
using System.IO;
using Lunaris.IGUI;
using Mono.Cecil;
using Version = SemanticVersioning.Version;

namespace Lunaris
{
	/// <summary>
	/// Ugh.
	/// </summary>
	internal class PluginInstaller
	{

		public static Sprite InstalledIcon;
		public static Sprite UpdateIcon;
		public static Sprite TroubleIcon;
		public static Sprite OutdatedInstallableIcon;
		public static Sprite DisabledIcon;
		public static Sprite DefaultIcon;


		public static bool isWindowOpen = false;
		private string searchText = "";
		private PluginSortKind sortKind = PluginSortKind.Alphabetical;
		private string filterText = "Alphabetical";
		private bool adaptiveSort = false;
		private bool resetPluginScroll = false;
		private readonly object listLock = new();
		private OperationStatus updateStatus = OperationStatus.Idle;
		private OperationStatus installStatus = OperationStatus.Idle;

		public List<PluginListItem> pluginsInstalled = [];
		public List<PluginListItem> pluginsAvailable = [];

		public int GetEnabledPluginCount()
		{
			return pluginsInstalled.Count(x => x.IsLoaded == true);
		}

		public PluginInstaller()
		{
			InstalledIcon = Bridge.LoadSpriteFromResource("Lunaris.installedIcon.png");
			UpdateIcon = Bridge.LoadSpriteFromResource("Lunaris.updateIcon.png");
			TroubleIcon = Bridge.LoadSpriteFromResource("Lunaris.troubleIcon.png");
			OutdatedInstallableIcon = Bridge.LoadSpriteFromResource("Lunaris.outdatedInstallableIcon.png");
			DisabledIcon = Bridge.LoadSpriteFromResource("Lunaris.disabledIcon.png");
			DefaultIcon = Bridge.LoadSpriteFromResource("Lunaris.defaultIcon.png");

			UI.InstalledIcon = ImGuiWrap.RegisterTexture(InstalledIcon.texture);
			UI.UpdateIcon = ImGuiWrap.RegisterTexture(UpdateIcon.texture);
			UI.TroubleIcon = ImGuiWrap.RegisterTexture(TroubleIcon.texture);
			UI.OutdatedInstallableIcon = ImGuiWrap.RegisterTexture(OutdatedInstallableIcon.texture);
			UI.DisabledIcon = ImGuiWrap.RegisterTexture(DisabledIcon.texture);
			UI.DefaultIcon = ImGuiWrap.RegisterTexture(DefaultIcon.texture);

			categories.Add(new CategoryInfo("Installed", GroupKind.Installed));
			categories.Add(new CategoryInfo("Available", GroupKind.Available));
			categories.Add(new CategoryInfo("All", GroupKind.All));
		}

		public void AddInstalledPlugin(PluginDescriptor desc, PluginDescriptor replaced = null)
		{
			var plg = replaced != null ? pluginsInstalled.FirstOrDefault(t => t.desc == replaced) : pluginsInstalled.FirstOrDefault(t => t.desc.Id == desc.Id);

			if (plg == null)
			{
				plg = new PluginListItem(desc.Manifest.DisplayName, desc) { IsInstalled = true };
				pluginsInstalled.Add(plg);
			}
			else
			{
				plg.pluginName = desc.Manifest.DisplayName;
				plg.desc = desc;
				plg.manifest = desc.Manifest;
				plg.SelectedVersion = desc.Manifest.Version;
				plg.Downloads = desc.Manifest.DownloadCount;
				plg.IsInstalled = true;
				plg.IsLoaded = desc.IsLoaded;
				plg.hasUpdate = false;
			}

			plg.icon = desc.Manifest.IconBlob != null && desc.Manifest.IconBlob.Length > 0 ? BlobToTexture(desc.Manifest.IconBlob) : null;
			ResortPlugins(sortAvailable: false);
		}

		public void SetInstalledPluginLoaded(PluginDescriptor desc)
		{
			var f = UI.installer.pluginsInstalled.FirstOrDefault(t => t.desc == desc);
			if (f != null)
			{
				f.IsLoaded = true;
			}
		}

		public void RemoveInstalledPlugin(string Id)
		{
			var f = pluginsInstalled.FirstOrDefault(t => t.desc.Id == Id);
			if (f != null)
				pluginsInstalled.Remove(f);
		}

		public bool IsPluginLoaded(string Id)
		{
			var f = pluginsInstalled.FirstOrDefault(t => t.desc.Id == Id);
			if (f != null)
				return f.IsLoaded;
			return false;
		}

		public static void Open()
		{
			isWindowOpen = true;
		}

		static bool hasPulledAPI = false;
		static bool isCompleteApi = false;
		private static int _apiRequestGeneration = 0;
		private static bool _isApiLoading = false;
		private static bool _apiLoadFailed = false;
		private static PluginListItem _modalPlugin;

		public void OnDraw()
		{
			if (!isWindowOpen) return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(830, 570)*1.2f);
			ImGui.Begin("Plugin Installer##Plugin Installer", ref isWindowOpen, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse);
			DrawHeader();
			DrawPluginCategories();
			DrawFooter();
			DrawPopUp();
			ImGui.End();

			PluginLinker.Draw();

			if(!hasPulledAPI)
			{
				isCompleteApi = false;
				FetchApprovedAsync(true);

				hasPulledAPI = true;
			}

		}

		internal void DrawPopUp()
		{
			if (_modalPlugin == null) return;

			if(_modalPlugin != null)
			{
				ImGui.OpenPopup($"Are you sure you want to enable {_modalPlugin.pluginName}?##AskPluginEnable");
			}

			if (ImGui.BeginPopupModal($"Are you sure you want to enable {_modalPlugin.pluginName}?##AskPluginEnable", ref _askPluginPerm, ImGuiWindowFlags.AlwaysAutoResize))
			{
				var titleSize = new System.Numerics.Vector2(ImGui.CalcTextSize($"Are you sure you want to enable {_modalPlugin.pluginName}?").X + 40, 0);
				ImGui.Dummy(titleSize);
				string permStr = "This Plugin does the following:\n";
				foreach (LunarisPermission perm in Enum.GetValues(typeof(LunarisPermission)))
				{
					if (perm == LunarisPermission.None) continue;
					if (perm == LunarisPermission.All) continue;
					if (perm == LunarisPermission.BepinPlugin) continue;
					if (perm == LunarisPermission.LunarisPlugin) continue;

					if (_modalPlugin.desc.EffectivePermissions.HasFlag(perm))
					{
						switch (perm)
						{
							/*case LunarisPermission.Harmony:
								if(!permStr.Contains("Modifies the"))
									permStr += $"\t\tModifies the game.\n";
							break;*/
							case LunarisPermission.Reflection:
							permStr += $"\t\tAccesses the game in an unknown way.\n";
							break;
							case LunarisPermission.FileAccess:
							permStr += $"\t\tReads/Writes or Deletes files.\n";
							break;
							case LunarisPermission.Network:
							permStr += $"\t\tAccesses the internet.\n";
							break;
							case LunarisPermission.Unsafe:
							permStr += $"\t\tGenerates code at runtime and might be unsafe to use.\n";
							break;
						}
						//permStr += $"{perm}\n";
					}
				}
				ImGui.Text($"{permStr}\n\n");
				ImGui.Separator();
				ImGui.SetCursorPosX(ImGui.GetStyle().FramePadding.X + 10);
				if (ImGui.Button("OK", new System.Numerics.Vector2(120, 0)))
				{
					LoadPlugin(_modalPlugin);
					_modalPlugin = null;
					ImGui.CloseCurrentPopup();
				}

				ImGui.SetItemDefaultFocus();
				ImGui.SameLine();

				var contentMaxX = ImGui.GetWindowContentRegionMax().X;
				var cursorPosX = contentMaxX - 120 - ImGui.GetStyle().FramePadding.X;

				ImGui.SetCursorPosX(cursorPosX);

				if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
				{
					_modalPlugin = null;
					ImGui.CloseCurrentPopup();
				}

				ImGui.EndPopup();
			}
		}

		internal static Texture2D BlobToTexture(byte[] blob)
		{
			if (blob == null || blob.Length == 0) return null;
			var tex = new Texture2D(2, 2);
			return tex.LoadImage(blob) ? tex : null;
		}

		internal static void FetchApprovedAsync(bool forceRefresh)
		{
			if (_isApiLoading) return;

			_isApiLoading = true;
			_apiLoadFailed = false;
			isCompleteApi = false;
			var generation = _apiRequestGeneration;

			Task.Run(async () =>
			{
				PluginManifestPage result;
				try
				{
					result = await Bridge.PluginApi.FetchAllApprovedAsync(forceRefresh);
				}
				catch (Exception e)
				{
					Bridge.Logger.LogError($"[PluginInstaller] Failed to fetch the mod catalog: {e}");
					result = new PluginManifestPage();
				}

				DispatcherBehaviour.RunOnMainThread(() => OnFinishGettingAPI(result, generation));
			});
		}

		private static void OnFinishGettingAPI(PluginManifestPage result, int generation)
		{
			if (generation != _apiRequestGeneration) return;

			_isApiLoading = false;

			if (result == null || !result.Succeeded)
			{
				_apiLoadFailed = true;
				isCompleteApi = true;
				return;
			}

			var plugins = result.Items;

			if (plugins != null)
			{
				foreach (var manifest in plugins)
				{
					try
					{
						AddOrUpdateApiPlugin(manifest);
					}
					catch (Exception e)
					{
						Bridge.Logger.LogError($"[PluginInstaller] Failed to process mod {manifest?.Id ?? "Unknown"}: {e}");
					}
				}
			}

			isCompleteApi = true;
			UI.installer.ResortPlugins();
			UI.installer.UpdateCategoriesOnPluginsChange();
		}

		private static void AddOrUpdateApiPlugin(PluginManifest manifest)
		{
			//skip over already installed ones
			var installed = UI.installer.pluginsInstalled.FirstOrDefault(t => t.desc.Manifest != null && t.desc.Manifest.Id == manifest.Id);
			if (installed != null)
			{
				Version installedVersion = new(installed.desc.Version);

				if (manifest.AllVersions != null && manifest.AllVersions.Count != 0)
				{
					foreach (var version in manifest.AllVersions)
					{
						Version availableVersion = new(version);

						if (availableVersion > installedVersion)
						{
							installed.hasUpdate = true;
							break;
						}
					}
				}

				installed.desc.Manifest.IsFromAPI = true;
				installed.desc.Manifest.DownloadCount = manifest.DownloadCount;
				installed.desc.Manifest.IconBlob = manifest.IconBlob;
				installed.icon = BlobToTexture(manifest.IconBlob);
				installed.desc.Manifest.DisplayName = manifest.DisplayName;
				installed.desc.Manifest.Description = manifest.Description;
				installed.desc.Manifest.Tags = manifest.Tags;
				installed.desc.Manifest.Author = manifest.Author;
				installed.desc.Author = manifest.Author;
				PluginManifestHandler.StoreManifest(installed.desc.Manifest);
				return;
			}

			if (string.IsNullOrEmpty(manifest.Version) || manifest.Version == "-1") return;
			if (UI.installer.pluginsAvailable.Any(t => t.desc.Manifest != null && t.desc.Manifest.Id == manifest.Id)) return;

			var desc = new PluginDescriptor
			{
				Description = manifest.Description,
				Version = manifest.Version,
				Author = manifest.Author,
				EffectivePermissions = LunarisPermission.LunarisPlugin,
				DeclaredPermissions = LunarisPermission.LunarisPlugin,
				Manifest = manifest,
			};

			manifest.IsFromAPI = true;

			var item = new PluginListItem(manifest.DisplayName, desc, manifest)
			{
				Downloads = manifest.DownloadCount,
				icon = BlobToTexture(manifest.IconBlob),
			};

			UI.installer.pluginsAvailable.Add(item);
		}

		private void DrawHeader()
		{
			var style = ImGui.GetStyle();
			var windowSize = ImGui.GetWindowSize();

			var globalScale = 1.25f;
			var yPos = ImGui.GetCursorPosY() - (5 * globalScale) + 5;


			var searchInputWidth = 180 * globalScale;
			var searchClearButtonWidth = 25 * globalScale;

			var sortByText = "Sort By";
			var sortByTextWidth = ImGui.CalcTextSize(sortByText).X;
			var sortSelectables = new (string option, PluginSortKind kind)[]
			{
				("Search Score", PluginSortKind.SearchScore),
				("Alphabetical", PluginSortKind.Alphabetical),
				("Downloads", PluginSortKind.DownloadCount),
				("Last Update", PluginSortKind.LastUpdate),
				("New", PluginSortKind.NewOrNot),
				("Not Installed", PluginSortKind.NotInstalled),
				("Enabled", PluginSortKind.EnabledDisabled),
				("Profile", PluginSortKind.ProfileOrNot),
			};
			var longestSelectableWidth = sortSelectables.Select(t => ImGui.CalcTextSize(t.option).X).Max();
			var selectableWidth = longestSelectableWidth + (style.FramePadding.X * 2);
			var sortSelectWidth = selectableWidth + sortByTextWidth + style.ItemInnerSpacing.X;


			ImGui.SetCursorPosY(yPos + 5);
			ImGui.SetNextItemWidth(selectableWidth);
			ImGui.Text(sortByText);
			ImGui.SameLine();
			ImGui.SetCursorPosY(yPos);
			ImGui.SetNextItemWidth(selectableWidth);
			if (ImGui.BeginCombo("", filterText, ImGuiComboFlags.NoArrowButton))
			{
				foreach (var (option, kind) in sortSelectables)
				{
					if (kind == PluginSortKind.SearchScore && string.IsNullOrWhiteSpace(searchText))
						continue;

					if (ImGui.Selectable(option))
					{
						sortKind = kind;
						filterText = option;
						adaptiveSort = false;

						lock (listLock)
						{
							ResortPlugins();
						}
					}
				}

				ImGui.EndCombo();
			}

			ImGui.SameLine();


			ImGui.SetCursorPosY(yPos);
			var searchTextChanged = false;
			var prevSearchText = searchText;
			ImGui.SetNextItemWidth(searchInputWidth);
			searchTextChanged |= ImGui.InputTextWithHint("###LuPluginInstaller_Search", "Search", ref searchText, 100, ImGuiInputTextFlags.AutoSelectAll);

			ImGui.SameLine();

			ImGui.SetNextItemWidth(searchClearButtonWidth);
			ImGui.SetCursorPosY(yPos);
			if (UI.IconButton(UI.ToIconString(FontAwesomeIcon.TimesCircle)))
			{
				searchText = string.Empty;
				searchTextChanged = true;
			}

			if (searchTextChanged)
			{
				if (adaptiveSort)
				{
					if (string.IsNullOrWhiteSpace(searchText))
					{
						sortKind = PluginSortKind.Alphabetical;
						filterText = "Alphabetical";
					}
					else
					{
						sortKind = PluginSortKind.SearchScore;
						filterText = "Search Score";
					}

					ResortPlugins();
				}
				else if (sortKind == PluginSortKind.SearchScore)
				{
					ResortPlugins();
				}

				UpdateCategoriesOnSearchChange();
			}


			ImGui.SameLine();
			ImGui.SetCursorPosY(yPos - 10);
			var headerText = "This window allows you to install and remove plugins.\r\nThey are made by third-party developers.";
			var headerTextSize = ImGui.CalcTextSize(headerText);
			ImGui.Text(headerText);
		}

		private void UpdateCategoriesOnSearchChange()
		{
			if (string.IsNullOrEmpty(searchText))
			{
				SetCategoryHighlightsForPlugins(null);
			}
			else
			{
				var pluginsMatchingSearch = pluginsAvailable.Where(rm => !IsManifestFiltered(rm, false)).ToList();
				var pluginsMatchingSearch2 = pluginsInstalled.Where(rm => !IsManifestFiltered(rm, true)).ToList();

				pluginsMatchingSearch.AddRange(pluginsMatchingSearch2);

				SetCategoryHighlightsForPlugins(pluginsMatchingSearch.ToList());
			}
		}

		public static bool SimpleFuzzyMatch(string query, string subject)
		{
			if (string.IsNullOrEmpty(query)) return true;
			int lastIdx = -1;
			foreach (char c in query)
			{
				lastIdx = subject.IndexOf(c, lastIdx + 1);
				if (lastIdx == -1) return false;
			}
			return true;
		}


		private bool IsManifestFiltered(PluginListItem plugin, bool inst)
		{
			var searchString = searchText.ToLowerInvariant();
			var hasSearchString = !string.IsNullOrWhiteSpace(searchText);

			if (CurrentGroupKind == GroupKind.Installed && !inst)
				return true;
			if (CurrentGroupKind == GroupKind.Available && inst)
				return true;

			if (!hasSearchString)
				return true;

			return hasSearchString && !((!string.IsNullOrEmpty(plugin.pluginName) && SimpleFuzzyMatch(searchString, plugin.pluginName.ToLowerInvariant())));
		}

		private void UpdateCategoriesOnPluginsChange()
		{
			UpdateCategoriesOnSearchChange();
		}

		private static List<PluginListItem> visiblePlugins = null;
		public void SetCategoryHighlightsForPlugins(List<PluginListItem> plugins)
		{
			visiblePlugins = plugins;
		}


		private void DrawPluginCategories()
		{
			var useContentHeight = -40f;
			var useMenuWidth = 180f;

			var globalScale = 1;

			var useContentWidth = ImGui.GetContentRegionAvail().X;

			var installerMainChild = ImGui.BeginChild("InstallerCategories", new System.Numerics.Vector2(useContentWidth, useContentHeight * globalScale));
			if (installerMainChild)
			{
				ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new System.Numerics.Vector2(5, 0));

				try
				{
					var categoriesChild = ImGui.BeginChild("InstallerCategoriesSelector", new System.Numerics.Vector2(useMenuWidth * globalScale, -1));
					{
						if (categoriesChild)
						{
							DrawPluginCategorySelectors();
						}
					}
					ImGui.EndChild();

					ImGui.SameLine();

					var scrollingChild = ImGui.BeginChild("ScrollingPlugins", new System.Numerics.Vector2(-1, -1), false);
					if (scrollingChild)
					{
						try
						{
							if (resetPluginScroll)
							{
								ImGui.SetScrollY(0);
								resetPluginScroll = false;
							}

							DrawPluginCategoryContent();
							DrawApiLoadStatus();
						}
						catch (Exception ex)
						{
							Bridge.Logger.LogError($"Could not draw category content\n {ex}");
						}
						ImGui.EndChild();
					}

				}
				catch (Exception ex)
				{
					Bridge.Logger.LogError($"Could not draw plugin categories\n {ex}");
				}

				ImGui.PopStyleVar();
				ImGui.EndChild();
			}

		}


		private List<CategoryInfo> categories = [];
		private class CategoryInfo(string name, GroupKind groupKind)
		{
			public string Name = name;
			public GroupKind GroupKind = groupKind;
		}
		private enum GroupKind
		{
			DevTools,
			Installed,
			Available,
			All,
		}
		GroupKind CurrentGroupKind = GroupKind.Installed;
		PluginTags CurrentTagKind = PluginTags.All;

		[Flags]
		public enum PluginTags
		{
			None = 0,
			Audio = 1 << 0,
			Content = 1 << 1,
			Gameplay = 1 << 2,
			Graphics = 1 << 3,
			QoL = 1 << 4,
			Tools = 1 << 5,
			UI = 1 << 6,
			Utility = 1 << 7,
			All = ~0
		}

		private readonly (string option, PluginTags tag)[] _selectableTags =
		[
			("All", PluginTags.All),
			("Audio", PluginTags.Audio),
			("Content", PluginTags.Content),
			("Gameplay", PluginTags.Gameplay),
			("Graphics", PluginTags.Graphics),
			("QoL", PluginTags.QoL),
			("Tools", PluginTags.Tools),
			("UI", PluginTags.UI),
			("Utility", PluginTags.Utility),
		];

		private void DrawPluginCategorySelectors()
		{
			var categoryItemSize = new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X - (5), ImGui.GetTextLineHeight());
			foreach (var groupInfo in categories)
			{
				var canShowGroup = (groupInfo.GroupKind != GroupKind.DevTools);
				if (!canShowGroup)
				{
					continue;
				}

				var isCurrent = groupInfo.GroupKind == CurrentGroupKind;
				ImGui.SetNextItemOpen(isCurrent);
				if (groupInfo.GroupKind == GroupKind.Available)
				{
					if (ImGui.Selectable(groupInfo.Name, isCurrent, ImGuiSelectableFlags.None, categoryItemSize))
					{
						if (!isCurrent)
						{
							CurrentGroupKind = groupInfo.GroupKind;
							resetPluginScroll = true;
						}
					}

					if(isCurrent)
					{

						foreach ((string option, PluginTags tag) in _selectableTags)
						{
							var isCurrentTag = tag == CurrentTagKind;
							if (ImGui.Selectable("\t" + option, isCurrentTag, ImGuiSelectableFlags.None, categoryItemSize))
							{
								if (!isCurrentTag)
								{
									CurrentTagKind = tag;
									resetPluginScroll = true;
								}
							}
							//ImGui.Dummy(new System.Numerics.Vector2(5));
						}


						//ImGui.Dummy(new System.Numerics.Vector2(5));
					}
				}
				else
				{
					if (ImGui.Selectable(groupInfo.Name, isCurrent, ImGuiSelectableFlags.None, categoryItemSize))
					{
						if (!isCurrent)
						{
							CurrentGroupKind = groupInfo.GroupKind;
							resetPluginScroll = true;
						}


					}
					//ImGui.Dummy(new System.Numerics.Vector2(5));
				}
			}
		}

		private void DrawPluginCategoryContent()
		{
			if(visiblePlugins != null)
			{
				DrawFilteredPluginList();
				return;
			}
			if (CurrentGroupKind == GroupKind.Installed)
				DrawInstalledPluginList(InstalledPluginListFilter.None);
			else if (CurrentGroupKind == GroupKind.Available)
				DrawAvailablePluginList();
			else if (CurrentGroupKind == GroupKind.All)
			{
				DrawAllPluginList();
			}
			return;
		}

		private bool IsApiBackedGroup()
		{
			return CurrentGroupKind == GroupKind.Available || CurrentGroupKind == GroupKind.All;
		}

		private void DrawApiLoadStatus()
		{
			if (!IsApiBackedGroup()) return;

			if (_isApiLoading)
			{
				if (CurrentGroupKind == GroupKind.Available && pluginsAvailable.Count == 0) return;

				var availableWidth = ImGui.GetContentRegionAvail().X;
				ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max(0, (availableWidth - 24) * 0.5f));
				UI.DrawSpinner(24);
			}
			else if (_apiLoadFailed)
			{
				ImGui.TextDisabled("Could not load plugins.");
				ImGui.SameLine();
				if (ImGui.Button("Retry##PluginApiCatalog"))
				{
					FetchApprovedAsync(true);
				}
			}
		}

		private static void ResetApiLoading()
		{
			_apiRequestGeneration++;
			_isApiLoading = false;
			_apiLoadFailed = false;
			isCompleteApi = false;
			hasPulledAPI = false;
		}

		private void DrawList(List<PluginListItem> pList, bool isAvailable=false)
		{
			var i = 0;
			var colWidth = (ImGui.GetContentRegionAvail().X - ImGui.GetStyle().ItemSpacing.X) / 2;
			var startX = ImGui.GetCursorPosX();
			var rightX = startX + colWidth + ImGui.GetStyle().ItemSpacing.X + 1;
			var leftY = ImGui.GetCursorPosY();
			var rightY = leftY;
			var col = 0;

			foreach (var plugin in pList)
			{
				var isLeft = col % 2 == 0;
				ImGui.SetCursorPos(new System.Numerics.Vector2(isLeft ? startX : rightX, isLeft ? leftY : rightY));
				ImGui.PushID($"installed{i + 1}{plugin.pluginName}");
				var cursorBefore = ImGui.GetCursorPosY();
				UI.BeginCard(new System.Numerics.Vector2(colWidth, 0));

				/*if(!plugin.IsInstalled)
					DrawAvailablePlugin(plugin, i++, colWidth);
				else*/
					DrawInstalledPlugin(plugin, i++, colWidth);

				var cardHeight = ImGui.GetCursorPosY() - cursorBefore;
				UI.EndCard();
				ImGui.PopID();
				if (isLeft) leftY += cardHeight + 4;
				else rightY += cardHeight + 4;

				col++;
			}
			ImGui.SetCursorPosY(Math.Max(leftY, rightY));
		}

		private void DrawFilteredPluginList()
		{
			List<PluginListItem> _plList = [.. visiblePlugins];

			DrawList(_plList);
		}


		private void DrawAllPluginList()
		{
			List<PluginListItem> _plList = [.. pluginsInstalled];
			_plList.AddRange(pluginsAvailable);
			DrawList(_plList);
		}

		private void DrawInstalledPluginList(InstalledPluginListFilter filter)
		{
			List<PluginListItem> _plList = [.. pluginsInstalled];

			DrawList(_plList);
		}

		private void DrawAvailablePluginList()
		{
			List<PluginListItem> _plList = [.. pluginsAvailable];

			if (_plList.Count == 0)
			{
				if (!isCompleteApi)
				{
					var available = ImGui.GetContentRegionAvail();
					ImGui.SetCursorPos(new System.Numerics.Vector2((available.X - 32) * 0.5f, (available.Y - 32) * 0.5f));
					UI.DrawSpinner(32);
					//ImGui.Text("")
				}
				else
				{
					ImGui.Text("No plugins available. Check your connection.");
				}
			}
			else
			{

				if (CurrentTagKind != PluginTags.All)
				{
					_plList = [.. _plList.Where(x => x.desc.Manifest.Tags.HasFlag(CurrentTagKind))];
				}

				DrawList(_plList);
			}
		}

		public static void DrawFuzzyText(string text, string query, System.Numerics.Vector4 matchCol)
		{
			if (string.IsNullOrEmpty(query))
			{
				ImGui.TextUnformatted(text);
				return;
			}

			int lastIdx = 0;
			string textLower = text.ToLower();
			string queryLower = query.ToLower();
			uint uCol = ImGui.ColorConvertFloat4ToU32(matchCol);
			var drawList = ImGui.GetWindowDrawList();
			float thickness = 0.6f;

			for (int i = 0; i < queryLower.Length; i++)
			{
				int foundIdx = textLower.IndexOf(queryLower[i], lastIdx);
				if (foundIdx == -1) break;
				if (foundIdx > lastIdx)
				{
					ImGui.TextUnformatted(text.Substring(lastIdx, foundIdx - lastIdx));
					ImGui.SameLine(0, 0);
				}
				string charStr = text[foundIdx].ToString();
				var pos = ImGui.GetCursorScreenPos();
				float charWidth = ImGui.CalcTextSize(charStr).X;

				drawList.AddText(new System.Numerics.Vector2(pos.X - thickness, pos.Y), uCol, charStr);
				drawList.AddText(new System.Numerics.Vector2(pos.X + thickness, pos.Y), uCol, charStr);
				drawList.AddText(new System.Numerics.Vector2(pos.X, pos.Y - thickness), uCol, charStr);
				drawList.AddText(new System.Numerics.Vector2(pos.X, pos.Y + thickness), uCol, charStr);
				drawList.AddText(pos, uCol, charStr);

				var dp = new System.Numerics.Vector2(charWidth + thickness, ImGui.GetTextLineHeight());
				ImGui.Dummy(dp);
				ImGui.SameLine(0, 0);

				lastIdx = foundIdx + 1;
			}
			if (lastIdx < text.Length)
				ImGui.TextUnformatted(text.Substring(lastIdx));
		}

		private Dictionary<string, float> _expandProgress = [];
		private Dictionary<string, float> _expandedHeights = [];

		private void DrawInstalledPlugin(PluginListItem plugin, int index, float width, bool showInstalled = false)
		{
			var startPosX = ImGui.GetCursorPos().X + 10;
			var trouble = false;

			var label = plugin.pluginName;
			if (showInstalled)
			{
				label += "Installed";
			}

			var flags = PluginHeaderFlags.None;
			if (trouble)
				flags |= PluginHeaderFlags.HasError;
			if (plugin.hasUpdate)
				flags |= PluginHeaderFlags.UpdateAvailable;
			if (!PluginPermissions.ArePermissionsDeclared(plugin.desc.DeclaredPermissions, plugin.desc.EffectivePermissions))
				flags |= PluginHeaderFlags.HasInvalidPermissions;
			if (plugin.desc.EffectivePermissions.HasFlag(LunarisPermission.BepinPlugin))
				flags |= PluginHeaderFlags.IsLegacy;

			if (flags == PluginHeaderFlags.None || flags == PluginHeaderFlags.IsLegacy)
				flags |= PluginHeaderFlags.IsVerified;
			if (plugin.desc.Manifest != null && plugin.desc.Manifest.IsFromAPI)
				flags |= PluginHeaderFlags.FromAPI;

			if (!_expandProgress.ContainsKey(label)) _expandProgress[label] = 0f;
			if (!_expandedHeights.ContainsKey(label)) _expandedHeights[label] = 0f;

			var dt = ImGui.GetIO().DeltaTime;
			var target = plugin.IsOpen ? 1f : 0f;
			_expandProgress[label] = _expandProgress[label] + (target - _expandProgress[label]) * MathF.Min(1f, dt * 6f);
			var t = _expandProgress[label];

			var wdl = ImGui.GetWindowDrawList();
			var headerStartY = ImGui.GetCursorPosY();
			var startX = ImGui.GetCursorPosX() + 5;
			var bgStart = ImGui.GetCursorScreenPos();
			bgStart.X += 5;
			bgStart.Y += 10;

			wdl.ChannelsSplit(2);
			wdl.ChannelsSetCurrent(1);
			ImGui.PushTextWrapPos(startX + width - 20);
			if (DrawPluginCollapsingHeader(label, plugin, flags, index, width))
			{
				var headerHeight = ImGui.GetCursorPosY() - headerStartY;
				var clippedHeight = t * (_expandedHeights[label]);
				wdl.PushClipRect(bgStart, new System.Numerics.Vector2(bgStart.X + width, bgStart.Y + clippedHeight - 10), true);


				ImGui.SetCursorPosX(startPosX);
				ImGui.Indent();

				// Name
			/*	if(!string.IsNullOrEmpty(searchText))
					DrawFuzzyText(plugin.pluginName, searchText, UI.LunarisColors.LunarisOrange);
				else
					ImGui.TextUnformatted(plugin.pluginName);*/

				//ImGui.SameLine();
				//ImGui.TextColored(UI.LunarisColors.LunarisGrey3, "Author");

				ImGui.SetCursorPosX(startPosX);
				if (!string.IsNullOrWhiteSpace(plugin.desc.Description))
				{
					ImGui.TextWrapped(plugin.desc.Description);
				}else
				{
					ImGui.TextWrapped("No description available.");
				}

				if (plugin.IsLoaded)
				{
					var sanName = plugin.desc.SetPluginName.Replace(" ", "").ToLower();
					var commands = Commands.GetCommandsForPlugin(sanName);

					if (commands.Count != 0)
					{
						ImGui.Dummy(new System.Numerics.Vector2(0, 10));

						foreach (var command in commands)
						{
							ImGui.SetCursorPosX(startPosX);
							ImGui.TextUnformatted($"/{sanName} {command.Name} » {command.Description}");
						}

						ImGui.Dummy(new System.Numerics.Vector2(0, 3));
					}
				}


				ImGui.SetCursorPosX(startPosX);

				if (plugin.IsInstalled)
				{

					DrawPluginControlButton(plugin);

					if(flags.HasFlag(PluginHeaderFlags.UpdateAvailable))
						DrawPluginUpdateButton(plugin);
					if (plugin.desc.Manifest.IsFromAPI)
						DrawModUrlButton($"https://erenshorvault.app/mod/{plugin.desc.Id}", false, plugin.pluginName);
					DrawDeletePluginButton(plugin);
					if(!flags.HasFlag(PluginHeaderFlags.FromAPI))
						DrawPluginLinkButton(plugin);
				}
				else
				{
					DrawPluginDownloadButton(plugin);
				}


				//if (availablePluginUpdate != default && !plugin.IsDev)
				{
					//    DrawUpdateSinglePluginButton(availablePluginUpdate);
				}

				//ImGui.SameLine();
				//ImGui.TextColored(UI.LunarisColors.LunarisGrey3, "v"+plugin.SelectedVersion);

				ImGui.Dummy(new System.Numerics.Vector2(5));
				ImGui.Unindent();


				var contentEnd = ImGui.GetCursorScreenPos();
				var contEndY = ImGui.GetCursorPosY();
				_expandedHeights[label] = contentEnd.Y - bgStart.Y;

				wdl.PopClipRect();
				ImGui.SetCursorScreenPos(new System.Numerics.Vector2(bgStart.X, bgStart.Y + clippedHeight));

				if (t > 0.001f)
				{
					var bgEnd = new System.Numerics.Vector2(bgStart.X + width - 10, bgStart.Y + clippedHeight - 10);
					wdl.ChannelsSetCurrent(0);
					wdl.AddRectFilled(bgStart, bgEnd, ImGui.ColorConvertFloat4ToU32(ImGuiStyle.SidebarBg), 8.0f, ImDrawFlags.RoundCornersAll);
				}

				var exHeight = t * (headerHeight-5);
				ImGui.SetCursorPosY(contEndY + exHeight - 66);
			}
			ImGui.PopTextWrapPos();

			wdl.ChannelsMerge();

		}


		private void DrawFooter()
		{
			var windowSize = ImGui.GetWindowSize();
			var placeholderButtonSize = ImGui.CalcTextSize("placeholder") + (ImGui.GetStyle().FramePadding * 2);

			ImGui.Separator();

			ImGui.SetCursorPosY(windowSize.Y - placeholderButtonSize.Y-10);

			DrawUpdatePluginsButton();

			ImGui.SameLine();
			if (ImGui.Button("Settings"))
			{
				LunarisSettings.IsOpen = true;
			}

			if (CurrentGroupKind == GroupKind.Available)
			{
				ImGui.SameLine();
				if (ImGui.Button("Refresh"))
				{
					pluginsAvailable.Clear();
					ResetApiLoading();
					UpdateCategoriesOnPluginsChange();
				}
			}

			if (CurrentGroupKind == GroupKind.Installed && pluginsInstalled.Count(x=>!x.IsLoaded) > 0)
			{
				ImGui.SameLine();
				if (ImGui.Button("Enable All"))
				{
					pluginsInstalled.ForEach(x =>
					{
						if (!x.IsLoaded)
							LoadPlugin(x);
					});
				}
			}



			var closeText = "Close";
			var closeButtonSize = ImGui.CalcTextSize(closeText) + (ImGui.GetStyle().FramePadding * 2);

			ImGui.SameLine(windowSize.X - closeButtonSize.X - 20);
			if (ImGui.Button(closeText))
			{
				isWindowOpen = false;
			}


		}


		private void DrawUpdatePluginsButton()
		{
			if (ImGui.Button("Update Plugins"))
			{

			}
		}

		private void DrawPluginLinkButton(PluginListItem plugin)
		{
			ImGui.SameLine();
			if(UI.IconButton(UI.ToIconString(FontAwesomeIcon.Plug)))
			//if (ImGui.Button("Link Plugin"))
			{
				PluginLinker.Open(plugin);
			}

			VerifiedCheckmarkFadeTooltip($"##plglink{plugin.pluginName}", "Link Plugin");
		}


		private void DrawDeletePluginButton(PluginListItem plugin)
		{
			ImGui.SameLine();
			if (plugin.IsLoaded)
			{
				ImGui.PushFont(ImGuiWrap.iconFont.ImFont);
				UI.DisabledButton(FontAwesomeIcon.TrashAlt);
				ImGui.PopFont();
				VerifiedCheckmarkFadeTooltip($"##plgloaded01{plugin.pluginName}", "Plugin is loaded");
			}
			else
			{
				bool shiftHeld = ImGui.GetIO().KeyShift;
				if (!shiftHeld)
				{
					ImGui.PushFont(ImGuiWrap.iconFont.ImFont);
					UI.DisabledButton(FontAwesomeIcon.TrashAlt);
					ImGui.PopFont();
					VerifiedCheckmarkFadeTooltip($"##plgdel01{plugin.pluginName}", "Hold Shift to delete");
				}
				else
				{
					if (UI.IconButton(UI.ToIconString(FontAwesomeIcon.TrashAlt)))
					{
						try
						{
							plugin.ScheduleDeletion(!plugin.ScheduledForDeletion);
							Notifications.Add(new Notification(NotificationType.Warning, $"{plugin.pluginName} Deleted.", Notifications.DefaultDuration));
						}
						catch (Exception ex)
						{
							Debug.LogError($"Plugin installer threw an error during removal of {plugin.pluginName}\n{ex}");
							Notifications.Add(new Notification(NotificationType.Error, $"Could not delete {plugin.pluginName}", Notifications.DefaultDuration));
						}
					}
					VerifiedCheckmarkFadeTooltip($"##plgdel01{plugin.pluginName}", "Delete");
				}
			}
		}

		private void DrawPluginUpdateButton(PluginListItem plugin)
		{
			if (UI.IconButton(UI.ToIconString(FontAwesomeIcon.Download)))
			{

				try
				{
					plugin.Update(plugin.SelectedVersion);
				}
				catch (Exception ex)
				{
					Debug.LogError($"Plugin installer threw an error during update of {plugin.pluginName}\n{ex}");
					Notifications.Add(new Notification(NotificationType.Error, $"Could not Update {plugin.pluginName}", Notifications.DefaultDuration));
				}

			}
			VerifiedCheckmarkFadeTooltip($"##plgupd01{plugin.pluginName}", "Update");
		}

		private void DrawPluginDownloadButton(PluginListItem plugin)
		{
			//ImGui.SameLine();
			/*if (plugin.IsLoaded)
			{

				ImGui.PushFont(ImGuiWrap.iconFont.ImFont);
				UI.DisabledButton(FontAwesomeIcon.Download);
				ImGui.PopFont();

				VerifiedCheckmarkFadeTooltip($"##plgloaded01{plugin.pluginName}", "Plugin cannot be downloaded.");
			}
			else*/
			{
				if (UI.IconButton(UI.ToIconString(FontAwesomeIcon.Download)))
				{

					try
					{
						//plugin.ScheduleDeletion(!plugin.ScheduledForDeletion);
						plugin.Download();
						//
					}
					catch (Exception ex)
					{
						Debug.LogError($"Plugin installer threw an error during download of {plugin.pluginName}\n{ex}");
						Notifications.Add(new Notification(NotificationType.Error, $"Could not Install {plugin.pluginName}", Notifications.DefaultDuration));
					}

				}
				VerifiedCheckmarkFadeTooltip($"##plgins01{plugin.pluginName}", "Install");

			}
		}

		private bool DrawModUrlButton(string repoUrl, bool big, string pname)
		{
			if (!string.IsNullOrEmpty(repoUrl) && (repoUrl.StartsWith("https://") || repoUrl.StartsWith("http://")))
			{
				ImGui.SameLine();

				var clicked = UI.IconButton(UI.ToIconString(FontAwesomeIcon.Globe));
				if (clicked)
				{
					try
					{
						Application.OpenURL(repoUrl);
					}
					catch (Exception ex)
					{
						Debug.LogError($"Could not open mod url: {repoUrl}t\n {ex}");
					}
				}

				VerifiedCheckmarkFadeTooltip($"##modurl{pname}", "Visit Mod Page");

				return true;
			}

			return false;
		}

		private static bool _askPluginPerm = true;

		private void DrawPluginControlButton(PluginListItem plugin)
		{

			// Disable everything if the updater is running or another plugin is operating
			var disabled = updateStatus == OperationStatus.InProgress || installStatus == OperationStatus.InProgress;


			var toggleId = plugin.pluginName;
			var isLoadedAndUnloadable = plugin.IsLoaded;


			if (disabled)
			{
				UI.DisabledToggleButton(toggleId, isLoadedAndUnloadable);
			}
			else
			{
				if (UI.ToggleButton(toggleId, ref isLoadedAndUnloadable))
				{

					if (!isLoadedAndUnloadable)
					{
						try
						{
							plugin.Unload();
							if (plugin.IsLoaded)
								Notifications.Add(new Notification(NotificationType.Error, $"Could not unload \"{plugin.pluginName}\"", Notifications.DefaultDuration));
							else
								Notifications.Add(new Notification(NotificationType.Info, $"\"{plugin.pluginName}\" Disabled", Notifications.DefaultDuration));
						}
						catch
						{

							Notifications.Add(new Notification(NotificationType.Error, $"Could not unload \"{plugin.pluginName}\"", Notifications.DefaultDuration));
						}

					}
					else
					{
						if (UI.Settings.AskForPerms)
						{
							ImGui.OpenPopup($"Are you sure you want to enable {plugin.pluginName}?##AskPluginEnable");

						}
						else
							LoadPlugin(plugin);
					}
				}
			}

			if (ImGui.BeginPopupModal($"Are you sure you want to enable {plugin.pluginName}?##AskPluginEnable", ref _askPluginPerm, ImGuiWindowFlags.AlwaysAutoResize))
			{
				var titleSize = new System.Numerics.Vector2(ImGui.CalcTextSize($"Are you sure you want to enable {plugin.pluginName}?").X+40, 0);
				ImGui.Dummy(titleSize);
				string permStr = "This Plugin does the following:\n";
				foreach (LunarisPermission perm in Enum.GetValues(typeof(LunarisPermission)))
				{
					if (perm == LunarisPermission.None) continue;
					if (perm == LunarisPermission.All) continue;
					if (perm == LunarisPermission.BepinPlugin) continue;
					if (perm == LunarisPermission.LunarisPlugin) continue;

					if (plugin.desc.EffectivePermissions.HasFlag(perm))
					{
						switch (perm)
						{
							/*case LunarisPermission.Harmony:
								if(!permStr.Contains("Modifies the"))
									permStr += $"\t\tModifies the game.\n";
							break;*/
							case LunarisPermission.Reflection:
							permStr += $"\t\tAccesses the game in an unknown way.\n";
							break;
							case LunarisPermission.FileAccess:
							permStr += $"\t\tReads/Writes or Deletes files.\n";
							break;
							case LunarisPermission.Network:
							permStr += $"\t\tAccesses the internet.\n";
							break;
							case LunarisPermission.Unsafe:
							permStr += $"\t\tGenerates code at runtime and might be unsafe to use.\n";
							break;
						}
						//permStr += $"{perm}\n";
					}
				}
				ImGui.Text($"{permStr}\n\n");
				ImGui.Separator();
				ImGui.SetCursorPosX(ImGui.GetStyle().FramePadding.X+10);
				if (ImGui.Button("OK", new System.Numerics.Vector2(120, 0)))
				{
					LoadPlugin(plugin);
					ImGui.CloseCurrentPopup();
				}

				ImGui.SetItemDefaultFocus();
				ImGui.SameLine();

				var contentMaxX = ImGui.GetWindowContentRegionMax().X;
				var cursorPosX = contentMaxX - 120 - ImGui.GetStyle().FramePadding.X;

				ImGui.SetCursorPosX(cursorPosX);

				if (ImGui.Button("Cancel", new System.Numerics.Vector2(120, 0)))
				{
					ImGui.CloseCurrentPopup();
				}

				ImGui.EndPopup();
			}

			ImGui.SameLine();
			ImGui.Dummy(new System.Numerics.Vector2(15, 0));


			if (plugin.IsLoaded)
			{
				DrawOpenPluginSettingsButton(plugin);

				ImGui.SameLine();
				ImGui.Dummy(new System.Numerics.Vector2(5, 0));
			}

		}

		private void LoadPlugin(PluginListItem plugin)
		{
			try
			{
				plugin.Load();
				if (!plugin.IsLoaded)
					Notifications.Add(new Notification(NotificationType.Error, $"Could not enable {plugin.pluginName}", Notifications.DefaultDuration));
				else
					Notifications.Add(new Notification(NotificationType.Info, $"{plugin.pluginName} Enabled", Notifications.DefaultDuration));
			}
			catch
			{
				Notifications.Add(new Notification(NotificationType.Error, $"Could not enable {plugin.pluginName}", Notifications.DefaultDuration));
			}
		}

		private void DrawOpenPluginSettingsButton(PluginListItem plugin)
		{
			var toggleId = plugin.pluginName;
			var sanName = plugin.desc.SetPluginName.Replace(" ", "").ToLower();
			var disabled = !ConfigHandler.Has(sanName);
			var isLoadedAndUnloadable = plugin.IsOptionsOpen;

			ImGui.SameLine();
			if (disabled)
			{
				UI.DisabledButton($"Options##plgoptbut{toggleId}");
			}
			else
			{
				if (ImGui.Button($"Options##plgoptbut{toggleId}"))
				{
					PluginOptions.Add(plugin);
				}
			}
		}

		internal void DrawProgressOverlay(float progress, System.Numerics.Vector4 color)
		{
			var btnMin = ImGui.GetItemRectMin();
			var btnMax = ImGui.GetItemRectMax();
			float r = ImGui.GetStyle().FrameRounding;
			var dl = ImGui.GetWindowDrawList();
			var col = ImGui.ColorConvertFloat4ToU32(color);

			float fillWidth = (btnMax.X - btnMin.X) * MathF.Clamp(progress, 0f, 1f);
			var fillMax = new System.Numerics.Vector2(btnMin.X + fillWidth, btnMax.Y);

			dl.PushClipRect(btnMin, fillMax, true);
			dl.AddRectFilled(btnMin, btnMax, col, r);
			dl.PopClipRect();
		}

		private bool DrawPluginCollapsingHeader(string label, PluginListItem plugin, PluginHeaderFlags flags, int index, float width)
		{
			var isOpen = plugin.IsOpen;

			var sectionSize = 1 * 66;
			var tapeCursor = ImGui.GetCursorPos();
			ImGui.SetCursorPos(ImGui.GetCursorPos() + new System.Numerics.Vector2(1, ImGui.GetStyle().ItemSpacing.Y));

			var startCursor = ImGui.GetCursorPos();


			ImGui.SetCursorPos(tapeCursor);

			if (ImGui.Button($"###plugin{index}CollapsibleBtn", new System.Numerics.Vector2(width, sectionSize + ImGui.GetStyle().ItemSpacing.Y)))
			{
				if (isOpen) plugin.IsOpen = false;
				else plugin.IsOpen = true;

				isOpen = !isOpen;
			}

			ImGui.SetItemAllowOverlap();


			var dlProgress = -1f;
			if (plugin.manifest != null && !string.IsNullOrEmpty(plugin.manifest.Id))
				dlProgress = Bridge.PluginApi.GetDownloadProgress(plugin.manifest.Id);
			else if(plugin.IsInstalled && plugin.desc != null && !string.IsNullOrEmpty(plugin.desc.Id))
				dlProgress = Bridge.PluginApi.GetDownloadProgress(plugin.desc.Id);

			if (dlProgress >= 0 && dlProgress < 1f)
			{
				DrawProgressOverlay(dlProgress, UI.LunarisColors.HealerGreen);
			}
			else if (!flags.HasFlag(PluginHeaderFlags.FromAPI))
			{
				var btnMin = ImGui.GetItemRectMin();
				var btnMax = ImGui.GetItemRectMax();
				float r = ImGui.GetStyle().FrameRounding;
				const float stripeWidth = 40f;
				const float skewAmount = 20f;
				const int arcSegments = 8;

				var wdl = ImGui.GetWindowDrawList();
				var yellow = ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1.0f, 0.9f, 0.0f, 0.10f));

				var roundedRect = new List<System.Numerics.Vector2>();
				void AddArc(float cx, float cy, float startAngle, float endAngle)
				{
					for (var s = 0; s <= arcSegments; s++)
					{
						var a = startAngle + (endAngle - startAngle) * s / arcSegments;
						roundedRect.Add(new System.Numerics.Vector2(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r));
					}
				}
				AddArc(btnMin.X + r, btnMin.Y + r, (float)Math.PI, (float)Math.PI * 1.5f);
				AddArc(btnMax.X - r, btnMin.Y + r, (float)Math.PI * 1.5f, (float)Math.PI * 2.0f);
				AddArc(btnMax.X - r, btnMax.Y - r, 0f, (float)Math.PI * 0.5f);
				AddArc(btnMin.X + r, btnMax.Y - r, (float)Math.PI * 0.5f, (float)Math.PI);

				static List<System.Numerics.Vector2> ClipPolygon(List<System.Numerics.Vector2> poly, List<System.Numerics.Vector2> clip)
				{
					var output = new List<System.Numerics.Vector2>(poly);
					for (var i = 0; i < clip.Count && output.Count > 0; i++)
					{
						var input = output;
						output = [];
						var edgeA = clip[i];
						var edgeB = clip[(i + 1) % clip.Count];
						for (var j = 0; j < input.Count; j++)
						{
							var cur = input[j];
							var prev = input[(j + input.Count - 1) % input.Count];
							var curInside = IsInside(cur, edgeA, edgeB);
							var prevInside = IsInside(prev, edgeA, edgeB);
							if (curInside)
							{
								if (!prevInside)
									output.Add(Intersect(prev, cur, edgeA, edgeB));
								output.Add(cur);
							}
							else if (prevInside)
								output.Add(Intersect(prev, cur, edgeA, edgeB));
						}
					}
					return output;
				}

				static bool IsInside(System.Numerics.Vector2 p, System.Numerics.Vector2 a, System.Numerics.Vector2 b)
				{
					return (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X) >= 0;
				}

				static System.Numerics.Vector2 Intersect(System.Numerics.Vector2 a, System.Numerics.Vector2 b, System.Numerics.Vector2 c, System.Numerics.Vector2 d)
				{
					var ab = b - a;
					var cd = d - c;
					var t = ((c.X - a.X) * cd.Y - (c.Y - a.Y) * cd.X) / (ab.X * cd.Y - ab.Y * cd.X);
					return a + t * ab;
				}

				var size = btnMax - btnMin;
				var numStripes = (int)(size.X / stripeWidth) + (int)(size.Y / skewAmount) + 1;

				for (var i = 0; i < numStripes; i++)
				{
					if (i % 2 != 0) continue;

					var x0 = btnMin.X + i * stripeWidth;
					var x1 = x0 + stripeWidth;
					var quad = new List<System.Numerics.Vector2>
					{
						new(x0, btnMin.Y),
						new(x1, btnMin.Y),
						new(x1 - skewAmount, btnMax.Y),
						new(x0 - skewAmount, btnMax.Y),
					};

					bool nearLeft = x0 - skewAmount < btnMin.X + r;
					bool nearRight = x1 > btnMax.X - r;

					if (nearLeft || nearRight)
					{
						var clipped = ClipPolygon(quad, roundedRect);
						if (clipped.Count < 3) continue;
						var arr = clipped.ToArray();
						wdl.AddConvexPolyFilled(ref arr[0], arr.Length, yellow);
					}
					else
						wdl.AddQuadFilled(quad[0], quad[1], quad[2], quad[3], yellow);
				}
			}



			ImGui.SetCursorPos(startCursor);

			var pluginDisabled = false;

			var iconSize = new System.Numerics.Vector2(64, 64);
			var cursorBeforeImage = ImGui.GetCursorPos();
			var rectOffset = ImGui.GetWindowContentRegionMin() + ImGui.GetWindowPos();

			var overlayAlpha = 1.0f;



			ImGui.SetCursorPos(startCursor + new System.Numerics.Vector2(2, 0));
			if (ImGui.IsRectVisible(rectOffset + cursorBeforeImage, rectOffset + cursorBeforeImage + iconSize))
			{
				var iconTex = UI.DefaultIcon;

				if(plugin.icon != null)
					iconTex = ImGuiWrap.RegisterTexture(plugin.icon);

				var iconAlpha = 1f;

				var p = ImGui.GetCursorScreenPos();
				var drawList = ImGui.GetWindowDrawList();
				drawList.AddRectFilled(p, new System.Numerics.Vector2(p.X + iconSize.X, p.Y + iconSize.Y), ImGui.ColorConvertFloat4ToU32(ImGuiStyle.SidebarBg), 4f);

				ImGui.PushStyleVar(ImGuiStyleVar.Alpha, iconAlpha);

				drawList.AddImageRounded(iconTex, p, p + iconSize, new System.Numerics.Vector2(0, 0), new System.Numerics.Vector2(1, 1), ImGui.ColorConvertFloat4ToU32(new System.Numerics.Vector4(1, 1, 1, iconAlpha)), 4.0f);

				ImGui.PopStyleVar();

				ImGui.SameLine();
				ImGui.SetCursorPos(cursorBeforeImage);
			}

			ImGui.SetCursorPos(tapeCursor + new System.Numerics.Vector2(5, 0));
			{
				var verifiedOutlineColor = UI.LunarisColors.White with { W = 0.75f };
				var unverifiedOutlineColor = UI.LunarisColors.White with { W = 0.75f };
				var verifiedIconColor = UI.LunarisColors.TankBlue with { W = 0.75f };
				var unverifiedIconColor = UI.LunarisColors.LunarisOrange;
				var devIconOutlineColor = UI.LunarisColors.White;
				var devIconColor = UI.LunarisColors.ParsedBlue;

				if (flags.HasFlag(PluginHeaderFlags.IsVerified) && !flags.HasFlag(PluginHeaderFlags.IsLegacy) && !flags.HasFlag(PluginHeaderFlags.HasInvalidPermissions))
				{
					DrawFontawesomeIconOutlined(FontAwesomeIcon.CheckCircle, verifiedOutlineColor, verifiedIconColor);
					VerifiedCheckmarkFadeTooltip("##verf" + label, "Verified");
				}
				else
				{
					if (flags.HasFlag(PluginHeaderFlags.IsLegacy) && !flags.HasFlag(PluginHeaderFlags.HasInvalidPermissions))
					{
						DrawFontawesomeIconOutlined(FontAwesomeIcon.ExclamationCircle, unverifiedOutlineColor, unverifiedIconColor);
						VerifiedCheckmarkFadeTooltip("##unverf" + label, "Unverified\nThis is a legacy plugin and might not work correctly.");
					}
					else if (flags.HasFlag(PluginHeaderFlags.HasInvalidPermissions) && UI.Settings.NotifyPerms)
					{
						DrawFontawesomeIconOutlined(FontAwesomeIcon.ExclamationCircle, unverifiedOutlineColor, unverifiedIconColor);

						string permStr = "This Plugin does the following:\n";
						foreach (LunarisPermission perm in Enum.GetValues(typeof(LunarisPermission)))
						{
							if (perm == LunarisPermission.None) continue;
							if (perm == LunarisPermission.All) continue;
							if (perm == LunarisPermission.BepinPlugin) continue;
							if (perm == LunarisPermission.LunarisPlugin) continue;

							if (plugin.desc.EffectivePermissions.HasFlag(perm))
							{
								switch (perm)
								{
									/*case LunarisPermission.Harmony:
										if(!permStr.Contains("Modifies the"))
											permStr += $"\t\tModifies the game.\n";
									break;*/
									case LunarisPermission.Reflection:
									permStr += $"\t\tAccesses the game in an unknown way.\n";
									break;
									case LunarisPermission.FileAccess:
									permStr += $"\t\tReads/Writes or Deletes files.\n";
									break;
									case LunarisPermission.Network:
									permStr += $"\t\tAccesses the internet.\n";
									break;
									case LunarisPermission.Unsafe:
									permStr += $"\t\tGenerates code at runtime and might be unsafe to use.\n";
									break;
								}
								//permStr += $"{perm}\n";
							}
						}

						VerifiedCheckmarkFadeTooltip("##unverf" + label, permStr);
					}
					else
					{

						if (flags.HasFlag(PluginHeaderFlags.IsLegacy))
						{
							DrawFontawesomeIconOutlined(FontAwesomeIcon.ExclamationCircle, unverifiedOutlineColor, unverifiedIconColor);
							VerifiedCheckmarkFadeTooltip("##unverf" + label, "Unverified\nThis is a legacy plugin and might not work correctly.");
						}
						else
						{
							DrawFontawesomeIconOutlined(FontAwesomeIcon.CheckCircle, verifiedOutlineColor, verifiedIconColor);
							VerifiedCheckmarkFadeTooltip("##verf" + label, "Verified");
						}
					}
				}

				ImGui.SetCursorPos(cursorBeforeImage);
			}

			var isLoaded = plugin is { IsLoaded: true };

			ImGui.PushStyleVar(ImGuiStyleVar.Alpha, overlayAlpha);
			if (flags.HasFlag(PluginHeaderFlags.UpdateAvailable))
				ImGui.Image(UI.UpdateIcon, iconSize);
			else if ((flags.HasFlag(PluginHeaderFlags.HasError) && !pluginDisabled))
				ImGui.Image(UI.TroubleIcon, iconSize);
			else if (flags.HasFlag(PluginHeaderFlags.IsInstallableOutdated))
				ImGui.Image(UI.OutdatedInstallableIcon, iconSize);
			else if (pluginDisabled)
				ImGui.Image(UI.DisabledIcon, iconSize);
			else if (isLoaded)
				ImGui.Image(UI.InstalledIcon, iconSize);
			else
				ImGui.Dummy(iconSize);
			ImGui.PopStyleVar();

			ImGui.SameLine();

			ImGui.SetCursorPosX((cursorBeforeImage + iconSize).X);
			ImGui.Dummy(new System.Numerics.Vector2(5));
			ImGui.SameLine();

			var cursor = ImGui.GetCursorPos();



			// Name
			if (!string.IsNullOrEmpty(searchText))
				UI.DrawTruncatedFuzzyText(label, searchText, UI.LunarisColors.LunarisOrange, 290, 0.5f);
			else
				UI.DrawTruncatedText(label, 290, 0.5f);

			cursor.Y += ImGui.GetTextLineHeightWithSpacing()-10;

			ImGui.SetCursorPosX((cursorBeforeImage+iconSize).X);
			ImGui.SetCursorPosY(cursor.Y);
			ImGui.Dummy(new System.Numerics.Vector2(5));
			ImGui.SameLine();

			var authorText = "By Unknown";
			if (!string.IsNullOrEmpty(plugin.desc.Author))
				authorText =  $"By {plugin.desc.Author}";

			if(plugin.desc.Manifest.DownloadCount > 0)
			{
				ImGui.TextColored(UI.LunarisColors.LunarisGrey3, authorText);
				ImGui.SameLine();
				ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.LunarisGrey);
				ImGui.TextColored(UI.LunarisColors.LunarisGrey, $"{plugin.desc.Manifest.DownloadCount} Downloads");
			}
			else
			{
				ImGui.TextColored(UI.LunarisColors.LunarisGrey3, authorText);
			}


			//ImGui.TextColored(UI.LunarisColors.LunarisGrey3, downloadCountText);

			ImGui.SetCursorPosX((cursorBeforeImage + iconSize).X);
			cursor.Y += ImGui.GetTextLineHeightWithSpacing() - 10;
			ImGui.SetCursorPosY(cursor.Y+3);
			ImGui.Dummy(new System.Numerics.Vector2(5));
			ImGui.SameLine();

			ImGui.TextColored(UI.LunarisColors.LunarisGrey, "Version: ");
			ImGui.SameLine();
			if (!flags.HasFlag(PluginHeaderFlags.FromAPI))
			{
				ImGui.TextColored(UI.LunarisColors.LunarisGrey, plugin.desc.Version);
			}
			else
			{

				if(!plugin.IsInstalled && plugin.manifest != null && plugin.manifest.AllVersions != null && plugin.manifest.AllVersions.Count > 1)
				{
					float maxWidth = plugin.manifest.AllVersions.Max(v => ImGui.CalcTextSize(v).X) + ImGui.GetStyle().FramePadding.X * 2 -3;
					ImGui.SetNextItemWidth(maxWidth);
					cursor.Y += 6;
					ImGui.SetCursorPosY(cursor.Y);
					ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(ImGui.GetStyle().FramePadding.X, 0));
					if (ImGui.BeginCombo("", plugin.SelectedVersion, ImGuiComboFlags.NoArrowButton))
					{
						foreach (var v in plugin.manifest.AllVersions)
						{
							if (v == plugin.SelectedVersion)
								continue;

							if (ImGui.Selectable(v))
							{
								plugin.SelectedVersion = v;
							}
						}

						ImGui.EndCombo();
					}
					ImGui.PopStyleVar();
				}
				else
					ImGui.TextColored(UI.LunarisColors.LunarisGrey, plugin.desc.Version);
			}

			// I hate everything about this
			/*if (flags.HasFlag(PluginHeaderFlags.IsNew))
			{
				ImGui.TextColored(UI.LunarisColors.TankBlue, "New");
				ImGui.SameLine();
			}
			if (flags.HasFlag(PluginHeaderFlags.IsLegacy))
			{
				ImGui.TextColored(UI.LunarisColors.ParsedGold, flags.HasFlag(PluginHeaderFlags.IsNew)?", Legacy":"Legacy");
				ImGui.SameLine();
			}
			if (!flags.HasFlag(PluginHeaderFlags.FromAPI))
			{
				ImGui.TextColored(UI.LunarisColors.DPSRed, flags.HasFlag(PluginHeaderFlags.IsLegacy)?", Manual":"Manual");
			}*/

			cursor.Y += ImGui.GetTextLineHeightWithSpacing();
			ImGui.SetCursorPos(cursor);


			ImGui.SetCursorPosX(cursor.X);


			startCursor.Y += sectionSize;
			ImGui.SetCursorPos(startCursor);

			return isOpen;
		}


		internal static void VerifiedCheckmarkFadeTooltip(string source, string tooltip)
		{
			uint id = ImGui.GetID(source);
			float hoverStartTime = ImGui.GetStateStorage().GetFloat(id, -1.0f);

			const float fadeInStartDelay = 0.25f;
			const float fadeDuration = 0.3f;

			if (ImGui.IsItemHovered())
			{
				if (hoverStartTime < 0)
				{
					hoverStartTime = Time.realtimeSinceStartup;
					ImGui.GetStateStorage().SetFloat(id, hoverStartTime);
				}

				float elapsed = Time.realtimeSinceStartup - hoverStartTime;
				if (elapsed >= fadeInStartDelay)
				{
					float fadeProgress = Mathf.Clamp01((elapsed - fadeInStartDelay) / fadeDuration);
					ImGui.PushStyleVar(ImGuiStyleVar.Alpha, fadeProgress);

					var mousePos = ImGui.GetIO().MousePos;
					mousePos.X += 10;
					mousePos.Y -= 45;

					ImGui.SetNextWindowPos(mousePos, ImGuiCond.Always);
					ImGui.SetTooltip(tooltip);
					ImGui.PopStyleVar();

				}
			}
			else
				ImGui.GetStateStorage().SetFloat(id, -1);
		}


		private void DrawFontawesomeIconOutlined(FontAwesomeIcon icon, System.Numerics.Vector4 outline, System.Numerics.Vector4 iconColor)
		{
			var positionOffset = new System.Numerics.Vector2(0.0f, 5.0f);

			var screenStart = ImGui.GetCursorScreenPos() + positionOffset;
			var drawList = ImGui.GetWindowDrawList();
			var iconStr = UI.ToIconString(icon);

			var outCol = ImGui.ColorConvertFloat4ToU32(outline);
			var iconCol = ImGui.ColorConvertFloat4ToU32(iconColor);

			ImGui.PushFont(ImGuiWrap.iconFont.ImFont);

			for (int x = -1; x <= 1; x++)
			{
				for (int y = -1; y <= 1; y++)
				{
					if (x == 0 && y == 0) continue;
					drawList.AddText(ImGuiWrap.iconFont.ImFont, ImGui.GetFontSize(), screenStart + new System.Numerics.Vector2(x, y), outCol, iconStr);
				}
			}

			drawList.AddText(ImGuiWrap.iconFont.ImFont, ImGui.GetFontSize(), screenStart + new System.Numerics.Vector2(0, 0), iconCol, iconStr);
			ImGui.Dummy(new System.Numerics.Vector2(ImGui.CalcTextSize(iconStr).X, ImGui.GetFontSize()+5));
			ImGui.PopFont();





			/*	ImGui.PushStyleColor(ImGuiCol.Text, iconColor);
				ImGui.Text(iconStr);
				ImGui.PopStyleColor();*/

			//ImGui.PopFont();
		}

		private void ResortPlugins(bool sortInstalled = true, bool sortAvailable = true)
		{
			switch (sortKind)
			{
				case PluginSortKind.Alphabetical:
					if (sortAvailable) pluginsAvailable.Sort((p1, p2) => p1.pluginName.CompareTo(p2.pluginName));
					if (sortInstalled) pluginsInstalled.Sort((p1, p2) => p1.pluginName.CompareTo(p2.pluginName));
				break;
				case PluginSortKind.DownloadCount:
					if (sortAvailable) pluginsAvailable.Sort((p1, p2) => p2.desc.Manifest.DownloadCount.CompareTo(p1.desc.Manifest.DownloadCount));
					if (sortInstalled) pluginsInstalled.Sort((p1, p2) => p2.desc.Manifest.DownloadCount.CompareTo(p1.desc.Manifest.DownloadCount));
					break;
				/*case PluginSortKind.LastUpdate:
				pluginsAvailable.Sort((p1, p2) => p2.LastUpdate.CompareTo(p1.LastUpdate));
					pluginsInstalled.Sort((p1, p2) =>
					{
				// We need to get remote manifests here, as the local manifests will have the time when the current version is installed,
				// not the actual time of the last update, as the plugin may be pending an update
					IPluginManifest? p2Considered = pluginsAvailable.FirstOrDefault(x => x.InternalName == p2.InternalName);
					p2Considered ??= p2.Manifest;

					IPluginManifest? p1Considered = pluginListAvailable.FirstOrDefault(x => x.InternalName == p1.InternalName);
					p1Considered ??= p1.Manifest;

					return 0;//p2Considered.LastUpdate.CompareTo(p1Considered.LastUpdate);
					});
					break;*/
				//case PluginSortKind.NewOrNot:
				//pluginsAvailable.Sort((p1, p2) => WasPluginSeen(p1.InternalName)
				//	  .CompareTo(WasPluginSeen(p2.InternalName)));
				//pluginsInstalled.Sort((p1, p2) => WasPluginSeen(p1.Manifest.InternalName)
				//	  .CompareTo(WasPluginSeen(p2.Manifest.InternalName)));
				//	break;
				case PluginSortKind.NotInstalled:
					if (sortAvailable) pluginsAvailable.Sort((p1, p2) => pluginsInstalled.Any(x => x.pluginName == p1.pluginName).CompareTo(pluginsInstalled.Any(x => x.pluginName == p2.pluginName)));
					if (sortInstalled) pluginsInstalled.Sort((p1, p2) => p1.pluginName.CompareTo(p2.pluginName)); // Makes no sense for installed plugins
				break;
				case PluginSortKind.EnabledDisabled:
					if (sortAvailable) pluginsAvailable.Sort((p1, p2) =>
					{
						bool IsEnabled(PluginListItem manifest)
						{
							return pluginsInstalled.Any(x => x.desc.Id == manifest.desc.Id && x.IsLoaded);
						}

						return IsEnabled(p2).CompareTo(IsEnabled(p1));
					});
					if (sortInstalled) pluginsInstalled.Sort((p1, p2) => (p2.IsLoaded).CompareTo(p1.IsLoaded));
				break;
				case PluginSortKind.SearchScore:
					if (!string.IsNullOrEmpty(searchText))
					{
						int Score(string name)
						{
							if (name.Equals(searchText, StringComparison.OrdinalIgnoreCase)) return 0;
							if (name.StartsWith(searchText, StringComparison.OrdinalIgnoreCase)) return 1;
							if (name.Contains(searchText, StringComparison.OrdinalIgnoreCase)) return 2;
							return 3;
						}
						if (sortAvailable) pluginsAvailable = pluginsAvailable.OrderBy(m => Score(m.pluginName)).ToList();
						if (sortInstalled) pluginsInstalled = pluginsInstalled.OrderBy(m => Score(m.pluginName)).ToList();
					}
					else
					{
						if (sortAvailable) pluginsAvailable = pluginsAvailable.OrderBy(m => m.pluginName).ToList();
						if (sortInstalled) pluginsInstalled = pluginsInstalled.OrderBy(m => m.pluginName).ToList();
					}
				break;
				default:
				break;
			}
		}

		internal sealed class PluginListItem
		{
			public string pluginName;
			public int Downloads = 0;
			public bool IsInstalled = false;
			public bool IsLoaded = false;
			public bool IsOpen = false;
			public bool IsOptionsOpen = false;

			//public PluginDescriptor descriptor;
			public PluginDescriptor desc;

			public PluginManifest manifest;

			public string SelectedVersion = "";
			public bool hasUpdate = false;

			public Texture2D icon = null;

			public bool ScheduledForDeletion { get; internal set; }

			//public PluginManager.PluginLoadError loadError;

			public PluginListItem(string _pluginName, PluginDescriptor _desc, PluginManifest man = null)
			{
				pluginName = _pluginName;
				desc = _desc;
				manifest = man;

				if(man != null)
				{
					SelectedVersion = man.Version;
				}
				else
				{
					SelectedVersion = _desc.Manifest.Version;
				}
			}

			public void Unload()
			{
				var res = PluginLoader.UnloadPlugin(desc);
				//loadError = res;
				//IsInstalled = false;
				if(!res)
				{
					return;
				}
				IsLoaded = false;
				desc.Manifest.IsEnabled = false;
			}

			public void Load()
			{
				if (string.IsNullOrEmpty(desc.filePath))
					desc.filePath = PluginAssemblyUtils.CopyToCache(desc.OriginalFilePath, true);

				if(desc.filePath == null)
				{
					return;
				}

				var res = PluginLoader.LoadPluginFile(desc.filePath, pluginName);
				//loadError = res.Item1;
				if (!res.Item1)
				{
					return;
				}

				//IsInstalled = true;
				IsLoaded = true;
				desc.Manifest.IsEnabled = true;
			}

			public void Update(string ver)
			{
				Task.Run(async () =>
				{
					var res = await Bridge.PluginApi.DownloadVersion(desc.Id, ver, (pluginZipBytes) =>
					{
						//Grab plugin bytes
						if (pluginZipBytes == null)
						{
							Notifications.Add(new Notification(NotificationType.Error, $"Could not Update {desc.Manifest.DisplayName}", Notifications.DefaultDuration));
							return;
						}

						(string mainDll, byte[] pluginBytes, Dictionary<string, byte[]> depend) = APIPluginManifestHandler.LoadZip(pluginZipBytes);

						//inject our manifest guid
						pluginBytes = PluginAssemblyUtils.InjectGuid(pluginBytes, desc.Manifest.Id);


						DispatcherBehaviour.RunOnMainThread(() =>
						{
							//filepath
							var p = Path.Combine(PluginLoader.pluginPath, mainDll);
							desc.OriginalFilePath = p;

							//update version
							desc.Version = ver;
							desc.Manifest.Version = ver;
							hasUpdate = false;
							SelectedVersion = ver;


							List<string> plDepends = null;
							if (depend != null)
							{
								plDepends = [];
								foreach (var depKV in depend)
								{
									var dep = depKV.Key;
									var dllB = depKV.Value;
									plDepends.Add(dep);

									if (!PluginAssemblyUtils.IsManagedAssembly(dllB))
									{
										File.WriteAllBytes(Path.Combine(PluginLoader.pluginPath, dep), dllB);
										continue;
									}

									string assemblyName;
									using (var ms = new MemoryStream(dllB))
										assemblyName = AssemblyDefinition.ReadAssembly(ms).Name.Name;

									if (!LibraryLoader.IsAssemblyLoaded(assemblyName))
										File.WriteAllBytes(Path.Combine(PluginLoader.pluginPath, dep), dllB);
								}
							}

							desc.Manifest.Dependencies = plDepends;

							//Save the manifest?
							PluginManifestHandler.StoreManifest(desc.Manifest);

							IsLoaded = desc.Manifest.IsEnabled;

							//Unload if loaded
							var wasLoaded = IsLoaded;
							if(IsLoaded)
							{
								Unload();
								Bridge.Logger.Log($"unloaded {pluginName}");
							}

							//we can now save the plugin
							File.WriteAllBytes(p, pluginBytes);
							Notifications.Add(new Notification(NotificationType.Warning, $"{desc.Manifest.DisplayName} Updated!", Notifications.DefaultDuration));
							IsOpen = false;

							desc.filePath = PluginAssemblyUtils.CopyToCache(p, true);

							//Auto enable plugin thingy
							if (wasLoaded)
							{
								//Bridge.Logger.Log($"tryload {pluginName}");
								UI.installer.LoadPlugin(this);
							}
						});
					});
				});

			}


			public void Download()
			{
				Task.Run(async () =>
				{
					var res = await Bridge.PluginApi.DownloadVersion(manifest.Id, SelectedVersion, (pluginZipBytes) =>
					{
						//Grab plugin bytes
						if (pluginZipBytes == null)
						{
							Notifications.Add(new Notification(NotificationType.Error, $"Could not Install {manifest.DisplayName}", Notifications.DefaultDuration));
							return;
						}

						(string mainDll, byte[] pluginBytes, Dictionary<string, byte[]> depend) = APIPluginManifestHandler.LoadZip(pluginZipBytes);

						//inject our manifest guid
						pluginBytes = PluginAssemblyUtils.InjectGuid(pluginBytes, manifest.Id);


						DispatcherBehaviour.RunOnMainThread(() =>
						{
							//Register with pluginloader, so we get the correct manifest
							var (succ, pdesc) = PluginLoader.LoadPluginFile(pluginBytes, "");
							if(!succ)
							{
								Notifications.Add(new Notification(NotificationType.Error, $"Could not Install {manifest.DisplayName}", Notifications.DefaultDuration));
								return;
							}

							//Move from available to installed
							IsInstalled = true;
							manifest.DownloadCount++;

							//overwrite pdesc with some stuff from our real manifest
							pdesc.Manifest.Author = manifest.Author;
							pdesc.Manifest.DownloadCount = manifest.DownloadCount;
							pdesc.Manifest.Description = manifest.Description;
							pdesc.Manifest.Id = manifest.Id;
							pdesc.Manifest.Version = SelectedVersion;
							pdesc.Manifest.IsEnabled = false;
							pdesc.Manifest.IconBlob = manifest.IconBlob;
							//pdesc.Manifest.Name = manifest.Name;

							pdesc.Version = SelectedVersion;
							pdesc.Description = manifest.Description;
							//pdesc.Name = manifest.Name;
							pdesc.Author = manifest.Author;
							pdesc.IsLoaded = false;
							pdesc.Manifest.IsFromAPI = true;

							//filepath
							var p = Path.Combine(PluginLoader.pluginPath, mainDll);
							pdesc.Manifest.FilePath = p;
							pdesc.OriginalFilePath = p;

							//overwrite manifest with the new one
							manifest = pdesc.Manifest;

							desc = pdesc;

							UI.installer.pluginsAvailable.Remove(this);
							UI.installer.pluginsInstalled.Add(this);

							List<string> plDepends = null;
							if (depend != null)
							{
								plDepends = [];
								foreach (var depKV in depend)
								{
									var dep = depKV.Key;
									var dllB = depKV.Value;
									plDepends.Add(dep);

									if (!PluginAssemblyUtils.IsManagedAssembly(dllB))
									{
										File.WriteAllBytes(Path.Combine(PluginLoader.pluginPath, dep), dllB);
										continue;
									}

									string assemblyName;
									using (var ms = new MemoryStream(dllB))
										assemblyName = AssemblyDefinition.ReadAssembly(ms).Name.Name;

									if (!LibraryLoader.IsAssemblyLoaded(assemblyName))
										File.WriteAllBytes(Path.Combine(PluginLoader.pluginPath, dep), dllB);
								}
							}

							desc.Manifest.Dependencies = plDepends;

							var incomingName = AssemblyDefinition.ReadAssembly(new MemoryStream(pluginBytes)).Name.Name;
							var duplicate = PluginLoader.GetPluginByAssemblyName(incomingName);
							if (duplicate != null && !duplicate.Manifest.IsFromAPI)
							{
								if (duplicate.IsLoaded)
									PluginLoader.UnloadPlugin(duplicate);
								PluginLoader.RemovePlugin(duplicate);
							}

							//Save the manifest?
							PluginManifestHandler.StoreManifest(manifest);

							//we can now save the plugin
							File.WriteAllBytes(p, pluginBytes);
							Notifications.Add(new Notification(NotificationType.Warning, $"{manifest.DisplayName} Installed!", Notifications.DefaultDuration));
							IsOpen = false;

							desc.filePath = PluginAssemblyUtils.CopyToCache(p, true);

							//Auto enable plugin thingy
							if (UI.Settings.AutoEnablePlugin)
							{
								if (UI.Settings.AskForPerms)
									_modalPlugin = this;
								else
									UI.installer.LoadPlugin(this);
							}
						});
					});
				});

			}

			public void ScheduleDeletion(bool scheduled)
			{
				if (ScheduledForDeletion) return;
				ScheduledForDeletion = scheduled;

				PluginLoader.RemovePlugin(desc);
			}
		}
		private enum OperationStatus
		{
			Idle,
			InProgress,
			Complete,
		}

		private enum PluginSortKind
		{
			Alphabetical,
			DownloadCount,
			LastUpdate,
			NewOrNot,
			NotInstalled,
			EnabledDisabled,
			ProfileOrNot,
			SearchScore,
		}

		[Flags]
		private enum PluginHeaderFlags
		{
			None = 0,
			HasInvalidPermissions = 1 << 0,
			HasError = 1 << 1,
			UpdateAvailable = 1 << 2,
			IsNew = 1 << 3,
			IsInstallableOutdated = 1 << 4,
			IsLegacy = 1 << 5,
			IsVerified = 1 << 6,
			FromAPI = 1 << 7,
		}

		private enum InstalledPluginListFilter
		{
			None,
			Testing,
			Updateable,
			Dev,
		}
	}


	/// <exclude/>
	internal static class MathF
	{
		public static float Abs(float value) => Math.Abs(value);

		public static float Acos(float value) => (float)Math.Acos(value);
		public static float Asin(float value) => (float)Math.Asin(value);
		public static float Atan(float value) => (float)Math.Atan(value);
		public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);

		public static float Ceiling(float value) => (float)Math.Ceiling(value);
		public static float Cos(float value) => (float)Math.Cos(value);
		public static float Cosh(float value) => (float)Math.Cosh(value);

		public static float Exp(float value) => (float)Math.Exp(value);
		public static float Floor(float value) => (float)Math.Floor(value);

		public static float Log(float value) => (float)Math.Log(value);
		public static float Log(float value, float baseValue) => (float)Math.Log(value, baseValue);

		public static float Max(float val1, float val2) => Math.Max(val1, val2);
		public static float Min(float val1, float val2) => Math.Min(val1, val2);

		public static float Pow(float x, float y) => (float)Math.Pow(x, y);
		public static float Round(float value) => (float)Math.Round(value);
		public static float Round(float value, int digits) => (float)Math.Round(value, digits);

		public static float Sign(float value) => Math.Sign(value);
		public static float Sin(float value) => (float)Math.Sin(value);
		public static float Sinh(float value) => (float)Math.Sinh(value);
		public static float Sqrt(float value) => (float)Math.Sqrt(value);
		public static float Tan(float value) => (float)Math.Tan(value);
		public static float Tanh(float value) => (float)Math.Tanh(value);

		public static float Truncate(float value) => (float)Math.Truncate(value);
		public static float Clamp(float value, float min, float max) => Math.Min(Math.Max(value, min), max);
		public static float Lerp(float a, float b, float t) => a + (b - a) * t;
		public static float SmoothStep(float a, float b, float t)
		{
			t = Clamp(t, 0f, 1f);
			t = t * t * (3f - 2f * t);
			return a + (b - a) * t;
		}
	}

}
