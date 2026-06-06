using ImGuiNET;
using Lunaris.Config;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	internal static class LunarisSettings
	{
		internal static bool IsOpen = false;
		internal static void Draw()
		{
			if (!IsOpen) return;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(765, 360), ImGuiCond.FirstUseEver);
			ImGui.Begin("Settings##lunarissettings", ref IsOpen, ImGuiWindowFlags.NoCollapse);

			var useContentWidth = ImGui.GetContentRegionAvail().X;
			var useContentHeight = -40f;
			if (ImGui.BeginChild("Settings##ch1", new System.Numerics.Vector2(useContentWidth, useContentHeight)))
			{
				if (ImGui.BeginTabBar("SettingsTabs##lunarisstabs"))
				{
					if (ImGui.BeginTabItem($"General"))
					{
						if (ImGui.BeginChild("Settings##ch2", new System.Numerics.Vector2(useContentWidth, useContentHeight)))
						{
							DrawGeneralTab();
							ImGui.EndChild();
						}
						ImGui.EndTabItem();
					}

					if (ImGui.BeginTabItem($"Updates"))
					{
						if (ImGui.BeginChild("Settings##ch3", new System.Numerics.Vector2(useContentWidth, useContentHeight)))
						{
							DrawUpdatesTab();
							ImGui.EndChild();
						}
						ImGui.EndTabItem();
					}
					ImGui.EndTabBar();
				}
				ImGui.EndChild();
			}

			DrawFooter();

			ImGui.End();
		}

		internal static void DrawGeneralTab()
		{
			//ImGui.BeginChild($"SettingsGeneral", new System.Numerics.Vector2(0, 0));

			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
			
			ImGui.Text($"Here you can change the general settings of Lunaris.");

			ImGui.Separator();

			ImGui.SetCursorPosY(ImGui.GetCursorPosY()+5);
			ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
			ImGui.TextWrapped("Displays a warning notification if Lunaris detects tampering with it's internals, which could be a security risk.");
			ImGui.PopStyleColor();
			ImGui.Text($"Tamper Warning");
			//PluginInstaller.VerifiedCheckmarkFadeTooltip("#plgtmpGen", "Displays a warning notification if Lunaris detects tampering\nwith it's internals, which could be a security risk.");

			ImGui.SameLine();

			var ControlPos = ImGui.GetCursorPosX() + 50;

			ImGui.SetCursorPosX(ControlPos);

			PluginOptions.DrawField("Tamper Warning", RegGetValue("TamperWarning", true), null, (updatedValue) => {
				var b = (bool)updatedValue ? 1 : 0;
				RegSetValue("TamperWarning", b, RegistryValueKind.DWord);
			});

			ImGui.Separator();
			ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
			ImGui.TextWrapped("Opens a console when the game starts (requires restart).");
			ImGui.PopStyleColor();
			ImGui.Text($"Console");
			//PluginInstaller.VerifiedCheckmarkFadeTooltip("#plgtmpCon", "Opens a console when the game starts (requires restart).");

			ImGui.SameLine();

			ImGui.SetCursorPosX(ControlPos);
			PluginOptions.DrawField("Console", RegGetValue("EnableConsole", false), null, (updatedValue) => {
				var b = (bool)updatedValue ? 1 : 0;
				RegSetValue("EnableConsole", b, RegistryValueKind.DWord);
			});

			ImGui.Separator();
			ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
			ImGui.TextWrapped("Keybind to open the plugin installer.");
			ImGui.PopStyleColor();
			ImGui.Text($"Open Plugin Installer");
			//PluginInstaller.VerifiedCheckmarkFadeTooltip("#plgtmpCon", "Opens a console when the game starts (requires restart).");

			ImGui.SameLine();

			var conflicts = ConfigHandler.GetKeybindConflicts();
			bool hasConflict = conflicts.Values.Any(list => list.Any(e => e.Plugin == "Lunaris" && e.Property == "OpenPluginInstaller"));
			ImGui.SetCursorPosX(ControlPos);
			PluginOptions.DrawKeybindField("Open Plugin Installer", "lun", "OpenPluginInstaller", UI.Settings.OpenPluginInstaller, hasConflict, (updatedValue) => {
				UI.Settings.OpenPluginInstaller.SetKeys(updatedValue);
				ConfigHandler.InvalidateConflicts();
				UI.SaveSettings();
			});


			ImGui.Separator();
			ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
			ImGui.TextWrapped("Automatically enables plugins after installing them.");
			ImGui.PopStyleColor();
			ImGui.Text($"Auto Enable");
			//PluginInstaller.VerifiedCheckmarkFadeTooltip("#plgtmpCon", "Opens a console when the game starts (requires restart).");

			ImGui.SameLine();

			ImGui.SetCursorPosX(ControlPos);
			PluginOptions.DrawField("Auto Enable", UI.Settings.AutoEnablePlugin, null, (updatedValue) => {
				UI.Settings.AutoEnablePlugin = (bool)updatedValue;
				UI.SaveSettings();
			});



			ImGui.Separator();
			ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
			ImGui.TextWrapped("Ask you for permissions before enabling a plugin.");
			ImGui.PopStyleColor();
			ImGui.Text($"Ask Permission");
			//PluginInstaller.VerifiedCheckmarkFadeTooltip("#plgtmpCon", "Opens a console when the game starts (requires restart).");

			ImGui.SameLine();

			ImGui.SetCursorPosX(ControlPos);
			PluginOptions.DrawField("Ask Permission", UI.Settings.AskForPerms, null, (updatedValue) => {
				UI.Settings.AskForPerms = (bool)updatedValue;
				UI.SaveSettings();
			});


			ImGui.Separator();
			ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
			ImGui.TextWrapped("Shows a warning in the installer if a plugin needs permissions.");
			ImGui.PopStyleColor();
			ImGui.Text($"Permission Warning");
			//PluginInstaller.VerifiedCheckmarkFadeTooltip("#plgtmpCon", "Opens a console when the game starts (requires restart).");

			ImGui.SameLine();

			ImGui.SetCursorPosX(ControlPos);
			PluginOptions.DrawField("Permission Warning", UI.Settings.NotifyPerms, null, (updatedValue) => {
				UI.Settings.NotifyPerms = (bool)updatedValue;
				UI.SaveSettings();
			});

		}

		internal static void DrawUpdatesTab()
		{
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
			ImGui.Text($"Here you can change the update settings.");

			ImGui.Separator();


			ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
			ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 5);
			ImGui.TextWrapped("Shows a notification if a plugin or more has an available update.");
			ImGui.PopStyleColor();
			ImGui.Text($"Plugin Update Notification");

			ImGui.SameLine();
			var ControlPos = ImGui.GetCursorPosX() + 50;

			ImGui.SetCursorPosX(ControlPos);
			PluginOptions.DrawField("Plugin Update Notification", UI.Settings.NotifyPluginUpdate, null, (updatedValue) => {
				UI.Settings.NotifyPluginUpdate = (bool)updatedValue;
				UI.SaveSettings();
			});


			ImGui.Separator();
			ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
			ImGui.TextWrapped("Automatically updates plugins.");
			ImGui.PopStyleColor();
			ImGui.Text($"Auto Update Plugins");

			ImGui.SameLine();

			ImGui.SetCursorPosX(ControlPos);
			PluginOptions.DrawField("Auto Update Plugins", UI.Settings.AutoUpdatePlugin, null, (updatedValue) => {
				UI.Settings.AutoUpdatePlugin = (bool)updatedValue;
				UI.SaveSettings();
			});


			ImGui.Separator();
			ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.ParsedOrange);
			ImGui.TextWrapped("Shows a notification if Lunaris has an update.");
			ImGui.PopStyleColor();
			ImGui.Text($"Lunaris Update Notification");

			ImGui.SameLine();

			ImGui.SetCursorPosX(ControlPos);
			PluginOptions.DrawField("Lunaris Update Notifications", UI.Settings.NotifyLunarisUpdate, null, (updatedValue) => {
				UI.Settings.NotifyLunarisUpdate = (bool)updatedValue;
				UI.SaveSettings();
			});
		}



		internal static void DrawFooter()
		{
			var windowSize = ImGui.GetWindowSize();
			var placeholderButtonSize = ImGui.CalcTextSize("placeholder") + (ImGui.GetStyle().FramePadding * 2);

			ImGui.Separator();

			ImGui.SetCursorPosY(windowSize.Y - placeholderButtonSize.Y - 10);

			ImGui.Dummy(placeholderButtonSize);

			var closeText = "Close";
			var closeButtonSize = ImGui.CalcTextSize(closeText) + (ImGui.GetStyle().FramePadding * 2);

			ImGui.SameLine(windowSize.X - closeButtonSize.X - 20);
			if (ImGui.Button(closeText))
			{
				IsOpen = false;
			}
		}



		internal static void RegSetValue(string name, object value, RegistryValueKind kind)
		{
			using var key = Registry.CurrentUser.CreateSubKey(@"Software\Lunaris");
			key?.SetValue(name, value, kind);
		}

		internal static T RegGetValue<T>(string name, T defaultValue = default!)
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
}
