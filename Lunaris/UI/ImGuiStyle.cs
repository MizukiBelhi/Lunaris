using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using ImGuiNET;
using System.Globalization;

namespace Lunaris
{

	/// <summary>
	/// Style, all colors taken from ErenshorVault. Curtesy @ET508
	/// </summary>
	internal class ImGuiStyle
	{

		public static readonly Vector4 WindowBg = FromHex("#0F1014");
		public static readonly Vector4 CardBg = FromHex("#1A1D23");
		public static readonly Vector4 CardBgHover = FromHex("#20242C");
		public static readonly Vector4 SidebarBg = FromHex("#151820");
		public static readonly Vector4 AccentPrimary = FromHex("#0D2640");
		public static readonly Vector4 AccentHover = FromHex("#008DFD");
		public static readonly Vector4 TextPrimary = FromHex("#F1F5F9");
		public static readonly Vector4 TextSecondary = FromHex("#94A3B8");
		public static readonly Vector4 TextMuted = FromHex("#64748B");
		public static readonly Vector4 BorderColor = FromHex("#2D3139");
		public static readonly Vector4 Success = FromHex("#10B981");
		public static readonly Vector4 Danger = FromHex("#EF4444");
		public static readonly Vector4 ButtonHover = FromHex("008DFD");
		public static readonly Vector4 Button = FromHex("151820");

		public static void ApplyStyle()
		{
			var style = ImGui.GetStyle();

			style.WindowRounding = 4f;
			style.ChildRounding = 4f;
			style.FrameRounding = 4f;
			style.PopupRounding = 4f;
			style.ScrollbarRounding = 4f;
			style.GrabRounding = 4f;

			style.FrameBorderSize = 1f;
			style.ChildBorderSize = 1f;
			style.WindowBorderSize = 0f;
			style.PopupBorderSize = 1f;
			style.TabBorderSize = 0f;

			style.ItemSpacing = new Vector2(4, 4);
			style.FramePadding = new Vector2(8, 4);

			style.SelectableTextAlign = new Vector2(0.05f, 0);
			style.WindowMenuButtonPosition = ImGuiDir.Right;
			style.ColorButtonPosition = ImGuiDir.Left;
			style.IndentSpacing = 20f;

			var colors = style.Colors;


			colors[(int)ImGuiCol.WindowBg] = WindowBg;
			colors[(int)ImGuiCol.ChildBg] = new Vector4(0.05f, 0.07f, 0.10f, 0f);

			colors[(int)ImGuiCol.TitleBg] = FromHex("0F1014", 0.9f);
			colors[(int)ImGuiCol.TitleBgActive] = FromHex("1A1D23", 1f);

			colors[(int)ImGuiCol.Border] = BorderColor;

			colors[(int)ImGuiCol.Text] = TextPrimary;
			colors[(int)ImGuiCol.TextDisabled] = TextMuted;


			colors[(int)ImGuiCol.Button] = FromHex("151820", 1f);
			colors[(int)ImGuiCol.ButtonHovered] = FromHex("008DFD", 1f);
			//colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.12f, 0.25f, 0.50f, 1f);

			colors[(int)ImGuiCol.FrameBg] = FromHex("151820", 1f);
			colors[(int)ImGuiCol.FrameBgHovered] = FromHex("1E222D", 1f);
			colors[(int)ImGuiCol.FrameBgActive] = FromHex("272C3A", 1f);
			
			colors[(int)ImGuiCol.PopupBg] = new Vector4(0.08f, 0.10f, 0.14f, 0.95f);
		}

		public static Vector4 FromHex(string hex, float alpha = 1.0f)
		{
			if (hex.StartsWith("#"))
				hex = hex.Substring(1);

			if (hex.Length == 6)
			{
				int r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
				int g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
				int b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);

				return new Vector4(r / 255f, g / 255f, b / 255f, alpha);
			}
			return new Vector4(1.0f, 1.0f, 1.0f, alpha);
		}
	}
}
