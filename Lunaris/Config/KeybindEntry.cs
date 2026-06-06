using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lunaris.Config
{
	internal class KeybindEntry(params KeyCode[] keys) : IKeybind
	{
		public KeyCode[] Keys { get; private set; } = keys ?? [];
		private readonly HashSet<int> _heldVKs = [];

		private bool _wasHeld;

		public bool IsHeld { get; private set; }
		public bool IsPressed { get; private set; }
		public bool IsReleased { get; private set; }

		public event Action OnPressed;
		public event Action OnHeld;
		public event Action OnReleased;

		public string DisplayString => BuildDisplayString();

		internal void NotifyVKey(int vk, bool down)
		{
			if (down) _heldVKs.Add(vk);
			else _heldVKs.Remove(vk);

			bool held = Keys.Length > 0 && Keys.All(k => _heldVKs.Contains(KeyToVK(k)));

			IsHeld = held;
			IsPressed = held && !_wasHeld;
			IsReleased = !held && _wasHeld;

			if (IsPressed) OnPressed?.Invoke();
			if (IsHeld) OnHeld?.Invoke();
			if (IsReleased) OnReleased?.Invoke();

			_wasHeld = held;
		}


		internal void SetKeys(KeyCode[] keys)
		{
			Keys = keys ?? [];
			_wasHeld = false;
		}

		public int[] ToVirtualKeys() => [.. Keys.Select(KeyToVK)];

		public string BuildDisplayString()
		{
			if (Keys.Length == 0) return "Unbound";
			return string.Join(" + ", Keys.Select(KeycodeDisplayName));
		}

		public static string KeycodeDisplayName(KeyCode k) => k switch
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

		public static int KeyToVK(KeyCode k)
		{
			if (k >= KeyCode.A && k <= KeyCode.Z)
				return (int)k - (int)KeyCode.A + 0x41;
			if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9)
				return (int)k - (int)KeyCode.Alpha0 + 0x30;
			if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9)
				return (int)k - (int)KeyCode.Keypad0 + 0x60;
			if (k >= KeyCode.F1 && k <= KeyCode.F15)
				return (int)k - (int)KeyCode.F1 + 0x70;

			return k switch
			{
				KeyCode.Backspace => 0x08,
				KeyCode.Tab => 0x09,
				KeyCode.Return => 0x0D,
				KeyCode.Escape => 0x1B,
				KeyCode.Space => 0x20,
				KeyCode.PageUp => 0x21,
				KeyCode.PageDown => 0x22,
				KeyCode.End => 0x23,
				KeyCode.Home => 0x24,
				KeyCode.LeftArrow => 0x25,
				KeyCode.UpArrow => 0x26,
				KeyCode.RightArrow => 0x27,
				KeyCode.DownArrow => 0x28,
				KeyCode.Insert => 0x2D,
				KeyCode.Delete => 0x2E,
				KeyCode.LeftShift => 0xA0,
				KeyCode.RightShift => 0xA1,
				KeyCode.LeftControl => 0xA2,
				KeyCode.RightControl => 0xA3,
				KeyCode.LeftAlt => 0xA4,
				KeyCode.RightAlt => 0xA5,
				KeyCode.LeftWindows => 0x5B,
				KeyCode.RightWindows => 0x5C,
				KeyCode.Numlock => 0x90,
				KeyCode.ScrollLock => 0x91,
				KeyCode.CapsLock => 0x14,
				KeyCode.Print => 0x2C,
				KeyCode.Pause => 0x13,
				KeyCode.KeypadDivide => 0x6F,
				KeyCode.KeypadMultiply => 0x6A,
				KeyCode.KeypadMinus => 0x6D,
				KeyCode.KeypadPlus => 0x6B,
				KeyCode.KeypadEnter => 0x0D,
				KeyCode.KeypadPeriod => 0x6E,
				KeyCode.Minus => 0xBD,
				KeyCode.Equals => 0xBB,
				KeyCode.LeftBracket => 0xDB,
				KeyCode.RightBracket => 0xDD,
				KeyCode.Backslash => 0xDC,
				KeyCode.Semicolon => 0xBA,
				KeyCode.Quote => 0xDE,
				KeyCode.BackQuote => 0xC0,
				KeyCode.Comma => 0xBC,
				KeyCode.Period => 0xBE,
				KeyCode.Slash => 0xBF,
				_ => 0x00
			};
		}

		public static KeyCode VKToKeyCode(int vk)
		{
			if (vk >= 0x41 && vk <= 0x5A)
				return KeyCode.A + (vk - 0x41);
			if (vk >= 0x30 && vk <= 0x39)
				return KeyCode.Alpha0 + (vk - 0x30);
			if (vk >= 0x60 && vk <= 0x69)
				return KeyCode.Keypad0 + (vk - 0x60);
			if (vk >= 0x70 && vk <= 0x7E)
				return KeyCode.F1 + (vk - 0x70);

			return vk switch
			{
				0x08 => KeyCode.Backspace,
				0x09 => KeyCode.Tab,
				0x0D => KeyCode.Return,
				0x1B => KeyCode.Escape,
				0x20 => KeyCode.Space,
				0x21 => KeyCode.PageUp,
				0x22 => KeyCode.PageDown,
				0x23 => KeyCode.End,
				0x24 => KeyCode.Home,
				0x25 => KeyCode.LeftArrow,
				0x26 => KeyCode.UpArrow,
				0x27 => KeyCode.RightArrow,
				0x28 => KeyCode.DownArrow,
				0x2D => KeyCode.Insert,
				0x2E => KeyCode.Delete,
				0xA0 => KeyCode.LeftShift,
				0xA1 => KeyCode.RightShift,
				0xA2 => KeyCode.LeftControl,
				0xA3 => KeyCode.RightControl,
				0xA4 => KeyCode.LeftAlt,
				0xA5 => KeyCode.RightAlt,
				0x5B => KeyCode.LeftWindows,
				0x5C => KeyCode.RightWindows,
				0x90 => KeyCode.Numlock,
				0x91 => KeyCode.ScrollLock,
				0x14 => KeyCode.CapsLock,
				0x13 => KeyCode.Pause,
				0x6F => KeyCode.KeypadDivide,
				0x6A => KeyCode.KeypadMultiply,
				0x6D => KeyCode.KeypadMinus,
				0x6B => KeyCode.KeypadPlus,
				0x6E => KeyCode.KeypadPeriod,
				0xBD => KeyCode.Minus,
				0xBB => KeyCode.Equals,
				0xDB => KeyCode.LeftBracket,
				0xDD => KeyCode.RightBracket,
				0xDC => KeyCode.Backslash,
				0xBA => KeyCode.Semicolon,
				0xDE => KeyCode.Quote,
				0xC0 => KeyCode.BackQuote,
				0xBC => KeyCode.Comma,
				0xBE => KeyCode.Period,
				0xBF => KeyCode.Slash,
				_ => KeyCode.None
			};
		}
	}
}