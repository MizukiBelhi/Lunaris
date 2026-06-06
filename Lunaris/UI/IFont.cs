using System;

namespace Lunaris.IGUI
{
	public interface IFont
	{
		/// <summary>
		/// This is the pointer required for ImGui.
		/// </summary>
		IntPtr ImFont { get; set; }
	}
}