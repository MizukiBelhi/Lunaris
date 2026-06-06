using System;
using UnityEngine;

namespace Lunaris.Config
{
	/// <summary>
	/// Config keybind entry.
	/// </summary>
	public interface IKeybind
	{
		KeyCode[] Keys { get; }

		bool IsHeld { get; }
		bool IsPressed { get; }
		bool IsReleased { get; }
		event Action OnPressed;
		event Action OnHeld;
		event Action OnReleased;

		string DisplayString { get; }
	}
}