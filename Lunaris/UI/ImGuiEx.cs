using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Lunaris.IGUI
{
	public static class ImGuiEx
	{
		/// <summary> Registers a texture. </summary>
		public static IntPtr RegisterTexture(Texture tex) => ImGuiWrap.RegisterTexture(tex);

		/// <summary> Registers a texture. </summary>
		public static IntPtr RegisterTexture(IntPtr ptr, Texture tex) => ImGuiWrap.RegisterTexture(ptr, tex);

		/// <summary> Unregisters a previously registered texture. </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > Clear up all your used textures during OnDestroy() to prevent memory leaks.
		///  </remarks>
		public static void UnregisterTexture(IntPtr ptr) => ImGuiWrap.UnregisterTexture(ptr);

		/// <summary>
		/// Registers a font from raw bytes for use in ImGui.
		/// </summary>
		///  <remarks>
		///  > [!WARNING]
		///  > Please only use once for each font during Awake()
		///  </remarks>
		/// <param name="fontData"> Raw TTF/OTF font bytes. </param>
		/// <param name="fontSize"> Font size in pixels. </param>
		public static IFont RegisterFont(byte[] fontData, float fontSize)
		{
			ImGuiWrap.LunarisFont font = new(fontData, fontSize, Assembly.GetCallingAssembly());
			ImGuiWrap.fontsToLoad.Add(font);
			ImGuiWrap.needsFontRebuild = true;
			return font;
		}

		/// <summary>
		/// Registers a font from a file path for use in ImGui.
		/// </summary>
		/// <remarks>
		/// > [!WARNING]
		/// > Please only use once for each font during Awake()
		/// </remarks>
		/// <param name="path"> Absolute path to a TTF or OTF file. </param>
		/// <param name="fontSize"> Font size in pixels. </param>
		/// <param name="isEmbedded"> Set to true if the font is embedded. </param>
		public static IFont RegisterFont(string path, float fontSize, bool isEmbedded = false)
		{
			ImGuiWrap.LunarisFont font = new(path, fontSize, Assembly.GetCallingAssembly(), false, isEmbedded);
			ImGuiWrap.fontsToLoad.Add(font);
			ImGuiWrap.needsFontRebuild = true;
			return font;
		}

		/// <summary>
		/// Unregisters a font.
		/// </summary>
		///  <remarks> 
		///  > [!WARNING]
		///  > Clear up all your loaded fonts during OnDestroy() to prevent memory leaks.
		///  </remarks>
		public static void UnregisterFont(IFont font)
		{
			if (ImGuiWrap.fontsToLoad.Contains(font))
				ImGuiWrap.fontsToLoad.Remove(font);

			ImGuiWrap.needsFontRebuild = true;
		}

		/// <summary> Converts a <see cref="IGUI.FontAwesomeIcon"/> string. </summary>
		public static string ToIconString(FontAwesomeIcon i) => ((char)i).ToString();

		/// <summary>
		/// Draws a styled icon button using a <see cref="IGUI.FontAwesomeIcon"/>.
		/// </summary>
		/// <param name="icon"> The FontAwesome icon to display. </param>
		/// <param name="defaultColor"> Button background color. Defaults to theme color if null. </param>
		/// <param name="activeColor"> Button color when held. Defaults to theme color if null. </param>
		/// <param name="hoveredColor"> Button color on hover. Defaults to theme color if null. </param>
		public static bool IconButton(FontAwesomeIcon icon, System.Numerics.Vector4? defaultColor = null, System.Numerics.Vector4? activeColor = null, System.Numerics.Vector4? hoveredColor = null) => UI.IconButton(ToIconString(icon), defaultColor, activeColor, hoveredColor);

		/// <summary>
		/// Draws a styled icon button using a raw icon string.
		/// </summary>
		/// <param name="iconText"> Icon string, typically from <see cref="ToIconString"/>. </param>
		/// <param name="defaultColor"> Button background color. Defaults to theme color if null. </param>
		/// <param name="activeColor"> Button color when held. Defaults to theme color if null. </param>
		/// <param name="hoveredColor"> Button color on hover. Defaults to theme color if null. </param>
		public static bool IconButton(string iconText, System.Numerics.Vector4? defaultColor = null, System.Numerics.Vector4? activeColor = null, System.Numerics.Vector4? hoveredColor = null) => UI.IconButton(iconText, defaultColor, activeColor, hoveredColor);

		/// <summary>
		/// Draws a greyed-out icon button that cannot be clicked.
		/// </summary>
		/// <param name="icon"> The FontAwesome icon to display. </param>
		/// <param name="id"> Optional ID. </param>
		/// <param name="defaultColor"> Button background color. Defaults to theme color if null. </param>
		/// <param name="activeColor"> Button color when held. Defaults to theme color if null. </param>
		/// <param name="hoveredColor"> Button color on hover. Defaults to theme color if null. </param>
		/// <param name="alphaMult"> Opacity. </param>
		public static bool DisabledButton(FontAwesomeIcon icon, int? id = null, System.Numerics.Vector4? defaultColor = null, System.Numerics.Vector4? activeColor = null, System.Numerics.Vector4? hoveredColor = null, float alphaMult = 0.5f) => UI.DisabledButton(icon, id, defaultColor, activeColor, hoveredColor, alphaMult);
	}
}