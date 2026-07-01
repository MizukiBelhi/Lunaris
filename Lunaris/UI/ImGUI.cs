using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using ImGuiNET;
using Lunaris.Config;
using Lunaris.IGUI;
using UnityEngine;

using Debug = UnityEngine.Debug;


namespace Lunaris
{

	internal class ImGuiWrap
	{
		private static IntPtr _context;
		//private static ImGuiIO* _io;

		private static Texture2D fontTex;



		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SaveAllStacks();
		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void PopAllStacks();

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr ImFontAtlas_Get();
		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetDisplaySize(float w, float h);

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetDeltaTime(float dt);

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetMousePos(float x, float y);

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetMouseButton(int button, bool down);

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetMouseWheel(float vertical, float horizontal);

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		private static unsafe extern IntPtr LoadFontFromMemory(IntPtr data, int data_size, float size_pixels, IntPtr font_cfg, ushort* glyph_ranges);
		public unsafe static IntPtr LoadFontFromMemy(IntPtr data, int data_size, float size)
		{
			return LoadFontFromMemory(data, data_size, size, IntPtr.Zero, null);
		}

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public unsafe static extern IntPtr LoadFont(string fileName, float size_pixels, IntPtr font_cfg, ushort* glyph_ranges);
		public unsafe static IntPtr LoadFont(string fileName, float size)
		{
			return LoadFont(fileName, size, IntPtr.Zero, null);
		}

		public unsafe static IntPtr LoadFontGlyph(string fileName, float size, ushort[] glyphs)
		{
			fixed (ushort* dataPtr = &glyphs[0])
			{
				return LoadFont(fileName, size, IntPtr.Zero, dataPtr);
			}
		}

		public unsafe static IntPtr LoadFontFromMemyGlyph(IntPtr data, int data_size, float size, ushort[] glyphs)
		{
			fixed (ushort* glyph_ranges = &glyphs[0])
			{
				return LoadFontFromMemory(data, data_size, size, IntPtr.Zero, glyph_ranges);
			}
		}

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetKeyDown(int key, bool down);

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void AddInputChar(uint c);

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public static extern void SetKeyModifiers(bool ctrl, bool shift, bool alt, bool super);

		[DllImport("kernel32.dll", SetLastError = true)]
		static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool FreeLibrary(IntPtr hModule);

		public delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll", SetLastError = true)]
		public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr newProc);

		[DllImport("user32.dll")]
		public static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

		[DllImport("user32.dll")]
		public static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

		[DllImport("user32.dll")]
		private static extern short GetKeyState(int nVirtKey);

		public const int GWL_WNDPROC = -4;

		static IntPtr originalWndProc;
		static IntPtr hookedHwnd;
		static WndProcDelegate newWndProcDelegate;

		public static bool TryHookWndProc()
		{
			if (hookedHwnd != IntPtr.Zero)
				return true;

			var process = Process.GetCurrentProcess();
			process.Refresh();

			IntPtr hwnd = process.MainWindowHandle;
			if (hwnd == IntPtr.Zero)
				return false;

			HookWndProc(hwnd);
			return hookedHwnd != IntPtr.Zero;
		}

		public static void HookWndProc(IntPtr hwnd)
		{
			try
			{
				if (hwnd == IntPtr.Zero)
				{
					Debug.LogError("Lunaris ImGui failed to hook input: MainWindowHandle was zero.");
					return;
				}

				newWndProcDelegate = HookedWndProc;
				originalWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(newWndProcDelegate));
				if (originalWndProc == IntPtr.Zero)
				{
					//Debug.LogError($"Lunaris ImGui failed to hook input for HWND {hwnd}: Win32 error {Marshal.GetLastWin32Error()}.");
					newWndProcDelegate = null;
					return;
				}

				hookedHwnd = hwnd;
			}
			catch{ }
		}

		private static readonly Dictionary<IntPtr, Texture> _textures = [];

		private static IntPtr _imgDll;

		public static Action OnRender = null;

		public static LunarisFont defaultFont = new("Lunaris.NotoSansKR-Regular.otf", 16*1.25f, null, false, true);
		public static LunarisFont iconFont = new("Lunaris.FontAwesomeFreeSolid.otf", 16 * 1.25f, null, true, true);

		public static List<IFont> fontsToLoad = [];

		private static bool isCapturingInput = false;

		private static readonly bool[] _heldMouseButtons = new bool[3];
		private static readonly HashSet<IntPtr> _heldKeys = [];
		private static bool wasCapture = false;

		public static bool needsFontRebuild = false;

		static void ReleaseAllToUnity(IntPtr hWnd)
		{
			if (_heldMouseButtons[0]) CallWindowProc(originalWndProc, hWnd, 0x0202, IntPtr.Zero, IntPtr.Zero);
			if (_heldMouseButtons[1]) CallWindowProc(originalWndProc, hWnd, 0x0205, IntPtr.Zero, IntPtr.Zero);
			if (_heldMouseButtons[2]) CallWindowProc(originalWndProc, hWnd, 0x0208, IntPtr.Zero, IntPtr.Zero);

			var _kk = new HashSet<IntPtr>(_heldKeys);
			foreach (var key in _kk)
			{
				_heldKeys.Remove(key);
				IntPtr lp = (IntPtr)(1 | (1 << 31) | (1 << 30));
				CallWindowProc(originalWndProc, hWnd, 0x0101, key, lp);
				CallWindowProc(originalWndProc, hWnd, 0x0105, key, lp);
			}
		}


		static IntPtr HookedWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
		{
			bool handledHotkey = false;
			switch (msg)
			{
				case 0x0200:
				int mx = (short)(lParam.ToInt32() & 0xFFFF);
				int my = (short)((lParam.ToInt32() >> 16) & 0xFFFF);
				SetMousePos(mx, my);
				break;
				case 0x0201: SetMouseButton(0, true); _heldMouseButtons[0] = true; break;
				case 0x0202: SetMouseButton(0, false); _heldMouseButtons[0] = false; break;
				case 0x0204: SetMouseButton(1, true); _heldMouseButtons[1] = true; break;
				case 0x0205: SetMouseButton(1, false); _heldMouseButtons[1] = false; break;
				case 0x0207: SetMouseButton(2, true); _heldMouseButtons[2] = true; break;
				case 0x0208: SetMouseButton(2, false); _heldMouseButtons[2] = false; break;
				case 0x020A:
				{
					short delta = (short)((wParam.ToInt32() >> 16));
					SetMouseWheel(0.0f, delta / 120.0f);
					break;
				}
				case 0x020E:
				{
					short delta = (short)((wParam.ToInt32() >> 16));
					SetMouseWheel(delta / 120.0f, 0.0f);
					break;
				}
			}

			switch (msg)
			{
				case 0x0100:
				case 0x0104:
				{
					if (!IsModifierVK((int)wParam))
						handledHotkey = ConfigHandler.NotifyVKey((int)wParam, down: true);
					_heldKeys.Add(wParam);
					ImGuiKey key = MapKey(wParam);
					if (key != ImGuiKey.None) SendKey(key, true);
					break;
				}
				case 0x0101:
				case 0x0105:
				{
					if (!IsModifierVK((int)wParam))
						handledHotkey = ConfigHandler.NotifyVKey((int)wParam, down: false);
					_heldKeys.Remove(wParam);
					ImGuiKey key = MapKey(wParam);
					if (key != ImGuiKey.None) SendKey(key, false);
					break;
				}
				case 0x0102:
					AddInputChar((char)wParam.ToInt32());
				break;
			}

			switch (msg)
			{
				case 0x0100:
				case 0x0101:
				case 0x0104:
				case 0x0105:
					handledHotkey |= UpdateModifiers();
				break;
			}

			if (isCapturingInput && !wasCapture)
				ReleaseAllToUnity(hWnd);
			wasCapture = isCapturingInput;


			if (isCapturingInput)
			{
				
				switch (msg)
				{
					case 0x0200:
					case 0x0201:
					case 0x0202:
					case 0x0203:
					case 0x0204:
					case 0x0205:
					case 0x0207:
					case 0x0208:
					case 0x020A:
					case 0x020E:
					case 0x0100:
					case 0x0101:
					case 0x0102:
					case 0x0104:
					case 0x0105:
					return IntPtr.Zero;
				}
			}

			if (handledHotkey)
				return IntPtr.Zero;

			return CallWindowProc(originalWndProc, hWnd, msg, wParam, lParam);
		}

		private static bool IsModifierVK(int vk) => vk is 0x10 or 0x11 or 0x12 or 0xA0 or 0xA1 or 0xA2 or 0xA3 or 0xA4 or 0xA5 or 0x5B or 0x5C;
		private static bool _lshift, _rshift, _lctrl, _rctrl, _lalt, _ralt, _lwin, _rwin;
		internal static bool LShift => _lshift;
		internal static bool RShift => _rshift;
		internal static bool LCtrl => _lctrl;
		internal static bool RCtrl => _rctrl;
		internal static bool LAlt => _lalt;
		internal static bool RAlt => _ralt;

		private static bool UpdateModifiers()
		{
			bool handled = false;
			bool lshift = (GetKeyState(0xA0) & 0x8000) != 0;
			bool rshift = (GetKeyState(0xA1) & 0x8000) != 0;
			bool lctrl = (GetKeyState(0xA2) & 0x8000) != 0;
			bool rctrl = (GetKeyState(0xA3) & 0x8000) != 0;
			bool lalt = (GetKeyState(0xA4) & 0x8000) != 0;
			bool ralt = (GetKeyState(0xA5) & 0x8000) != 0;
			bool lwin = (GetKeyState(0x5B) & 0x8000) != 0;
			bool rwin = (GetKeyState(0x5C) & 0x8000) != 0;

			if (lshift != _lshift) { handled |= ConfigHandler.NotifyVKey(0xA0, lshift); _lshift = lshift; }
			if (rshift != _rshift) { handled |= ConfigHandler.NotifyVKey(0xA1, rshift); _rshift = rshift; }
			if (lctrl != _lctrl) { handled |= ConfigHandler.NotifyVKey(0xA2, lctrl); _lctrl = lctrl; }
			if (rctrl != _rctrl) { handled |= ConfigHandler.NotifyVKey(0xA3, rctrl); _rctrl = rctrl; }
			if (lalt != _lalt) { handled |= ConfigHandler.NotifyVKey(0xA4, lalt); _lalt = lalt; }
			if (ralt != _ralt) { handled |= ConfigHandler.NotifyVKey(0xA5, ralt); _ralt = ralt; }
			if (lwin != _lwin) { handled |= ConfigHandler.NotifyVKey(0x5B, lwin); _lwin = lwin; }
			if (rwin != _rwin) { handled |= ConfigHandler.NotifyVKey(0x5C, rwin); _rwin = rwin; }

			SetKeyModifiers(lctrl || rctrl, lshift || rshift, lalt || ralt, lwin || rwin);
			return handled;
		}

		private static ImGuiKey MapKey(IntPtr wParam)
		{
			int vk = wParam.ToInt32();
			switch (vk)
			{
				case 0x09: return ImGuiKey.Tab;
				case 0x25: return ImGuiKey.LeftArrow;
				case 0x27: return ImGuiKey.RightArrow;
				case 0x26: return ImGuiKey.UpArrow;
				case 0x28: return ImGuiKey.DownArrow;
				case 0x21: return ImGuiKey.PageUp;
				case 0x22: return ImGuiKey.PageDown;
				case 0x24: return ImGuiKey.Home;
				case 0x23: return ImGuiKey.End;
				case 0x2D: return ImGuiKey.Insert;
				case 0x2E: return ImGuiKey.Delete;
				case 0x08: return ImGuiKey.Backspace;
				case 0x20: return ImGuiKey.Space;
				case 0x0D: return ImGuiKey.Enter;
				case 0x1B: return ImGuiKey.Escape;
				case 0xDE: return ImGuiKey.Apostrophe;
				case 0xBC: return ImGuiKey.Comma;
				case 0xBD: return ImGuiKey.Minus;
				case 0xBE: return ImGuiKey.Period;
				case 0xBF: return ImGuiKey.Slash;
				case 0xBA: return ImGuiKey.Semicolon;
				case 0xBB: return ImGuiKey.Equal;
				case 0xDB: return ImGuiKey.LeftBracket;
				case 0xDC: return ImGuiKey.Backslash;
				case 0xDD: return ImGuiKey.RightBracket;
				case 0xC0: return ImGuiKey.GraveAccent;
				case 0x14: return ImGuiKey.CapsLock;
				case 0x91: return ImGuiKey.ScrollLock;
				case 0x90: return ImGuiKey.NumLock;
				case 0x2C: return ImGuiKey.PrintScreen;
				case 0x13: return ImGuiKey.Pause;
				case 0x60: return ImGuiKey.Keypad0;
				case 0x61: return ImGuiKey.Keypad1;
				case 0x62: return ImGuiKey.Keypad2;
				case 0x63: return ImGuiKey.Keypad3;
				case 0x64: return ImGuiKey.Keypad4;
				case 0x65: return ImGuiKey.Keypad5;
				case 0x66: return ImGuiKey.Keypad6;
				case 0x67: return ImGuiKey.Keypad7;
				case 0x68: return ImGuiKey.Keypad8;
				case 0x69: return ImGuiKey.Keypad9;
				case 0x6E: return ImGuiKey.KeypadDecimal;
				case 0x6F: return ImGuiKey.KeypadDivide;
				case 0x6A: return ImGuiKey.KeypadMultiply;
				case 0x6D: return ImGuiKey.KeypadSubtract;
				case 0x6B: return ImGuiKey.KeypadAdd;
				case 0x0D + 256: return ImGuiKey.KeypadEnter;
				case 0xA0: return ImGuiKey.LeftShift;
				case 0xA2: return ImGuiKey.LeftCtrl;
				case 0xA4: return ImGuiKey.LeftAlt;
				case 0x5B: return ImGuiKey.LeftSuper;
				case 0xA1: return ImGuiKey.RightShift;
				case 0xA3: return ImGuiKey.RightCtrl;
				case 0xA5: return ImGuiKey.RightAlt;
				case 0x5C: return ImGuiKey.RightSuper;
				case 0x5D: return ImGuiKey.Menu;

				case 0x30: return ImGuiKey._0;
				case 0x31: return ImGuiKey._1;
				case 0x32: return ImGuiKey._2;
				case 0x33: return ImGuiKey._3;
				case 0x34: return ImGuiKey._4;
				case 0x35: return ImGuiKey._5;
				case 0x36: return ImGuiKey._6;
				case 0x37: return ImGuiKey._7;
				case 0x38: return ImGuiKey._8;
				case 0x39: return ImGuiKey._9;

				case 0x41: return ImGuiKey.A;
				case 0x42: return ImGuiKey.B;
				case 0x43: return ImGuiKey.C;
				case 0x44: return ImGuiKey.D;
				case 0x45: return ImGuiKey.E;
				case 0x46: return ImGuiKey.F;
				case 0x47: return ImGuiKey.G;
				case 0x48: return ImGuiKey.H;
				case 0x49: return ImGuiKey.I;
				case 0x4A: return ImGuiKey.J;
				case 0x4B: return ImGuiKey.K;
				case 0x4C: return ImGuiKey.L;
				case 0x4D: return ImGuiKey.M;
				case 0x4E: return ImGuiKey.N;
				case 0x4F: return ImGuiKey.O;
				case 0x50: return ImGuiKey.P;
				case 0x51: return ImGuiKey.Q;
				case 0x52: return ImGuiKey.R;
				case 0x53: return ImGuiKey.S;
				case 0x54: return ImGuiKey.T;
				case 0x55: return ImGuiKey.U;
				case 0x56: return ImGuiKey.V;
				case 0x57: return ImGuiKey.W;
				case 0x58: return ImGuiKey.X;
				case 0x59: return ImGuiKey.Y;
				case 0x5A: return ImGuiKey.Z;

				case 0x70: return ImGuiKey.F1;
				case 0x71: return ImGuiKey.F2;
				case 0x72: return ImGuiKey.F3;
				case 0x73: return ImGuiKey.F4;
				case 0x74: return ImGuiKey.F5;
				case 0x75: return ImGuiKey.F6;
				case 0x76: return ImGuiKey.F7;
				case 0x77: return ImGuiKey.F8;
				case 0x78: return ImGuiKey.F9;
				case 0x79: return ImGuiKey.F10;
				case 0x7A: return ImGuiKey.F11;
				case 0x7B: return ImGuiKey.F12;

				default: return ImGuiKey.None;
			}
		}


		internal class LunarisFont(string filePath, float size, Assembly ex = null, bool IsFontAwesome = false, bool IsEmbedded=false) : IFont
		{
			public IntPtr ImFont { get; set; }

			public string Path { get; set; } = filePath;
			public float Size { get; set; } = size;
			public bool IsLoaded { get; set; } = false;
			private readonly bool isfontAwesome = IsFontAwesome;
			private readonly bool isEmbedded = IsEmbedded;
			private readonly bool isData = false;

			private byte[] _fontData = null;

			private Assembly execAss = ex;

			private static readonly ushort[] fontAwesomeGlyphs =
									[
										0x0020, 0x00FF,
										0xe005, 0xf8ff,
										0x0000
									];

			public LunarisFont(byte[] data, float size, Assembly ass=null) : this("", size, ass, false, false)
			{
				_fontData = data;
				isData = true;
			}


			public unsafe void Init()
			{
				if (IsLoaded) return;

				if (!isEmbedded && !isData)
				{
					if (!isfontAwesome)
						ImFont = LoadFont(Path, Size);
					else
						ImFont = LoadFontGlyph(Path, Size, fontAwesomeGlyphs);

					IsLoaded = true;
				}
				else if(isData)
				{
					LoadData();
				}
				else
				{
					if (execAss == null)
						execAss = Assembly.GetExecutingAssembly();

					using (Stream stream = execAss.GetManifestResourceStream(Path))
					{
						if (stream == null)
						{
							Debug.LogError($"Resource not found: {Path}");
							return;
						}

						_fontData = new byte[stream.Length];
						int offset = 0;
						while (offset < _fontData.Length)
						{
							int bytesRead = stream.Read(_fontData, offset, _fontData.Length - offset);
							if (bytesRead == 0)
							{
								Debug.LogError($"Unexpected end of stream reading resource: {Path}");
								return;
							}
							offset += bytesRead;
						}

					}

					LoadData();
				}
			}

			private void LoadData()
			{
				if(_fontData == null || _fontData.Length == 0)
				{
					Bridge.Logger.LogError($"Error loading font. Data == null || 0!");
					return;
				}

				IntPtr ptr = Marshal.AllocHGlobal(_fontData.Length);
				Marshal.Copy(_fontData, 0, ptr, _fontData.Length);

				if (!isfontAwesome)
					ImFont = LoadFontFromMemy(ptr, _fontData.Length, Size);
				else
					ImFont = LoadFontFromMemyGlyph(ptr, _fontData.Length, Size, fontAwesomeGlyphs);

				IsLoaded = true;
			}
		}


		public static IntPtr RegisterTexture(Texture tex)
		{
			IntPtr ptr = tex.GetNativeTexturePtr();
			if (!_textures.ContainsKey(ptr)) _textures.Add(ptr, tex);
			return ptr;
		}

		public static IntPtr RegisterTexture(IntPtr ptr, Texture tex)
		{
			if (!_textures.ContainsKey(ptr)) _textures.Add(ptr, tex);
			return ptr;
		}

		public static void UnregisterTexture(IntPtr ptr)
		{
			if (_textures.ContainsKey(ptr)) _textures.Remove(ptr);
		}

		public static void Init()
		{
			var assembly = Assembly.GetExecutingAssembly();


			//write cimgui to cache
			Stream stream = assembly.GetManifestResourceStream("Lunaris.cimgui.dll");
			byte[] ddata = new byte[stream.Length];
			int offset = 0;
			while (offset < ddata.Length)
			{
				int bytesRead = stream.Read(ddata, offset, ddata.Length - offset);
				if (bytesRead == 0)
				{
					Debug.LogError($"CRITICAL ERROR: Unexpected end of stream reading cimgui - REPORT THIS!!!");
					return;
				}
				offset += bytesRead;
			}

			var fp = Path.Combine(PluginLoader.cacheRoot, "cimgui.dll");
			File.WriteAllBytes(fp, ddata);

			//get handle
			_imgDll = LoadLibrary(fp);


			_context = ImGui.CreateContext();
			ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;


			//we change the location of imgui.ini
			unsafe
			{
				byte[] pathBuffer = Encoding.UTF8.GetBytes("./plugins/config/imgui.ini\0");
				GCHandle pathHandle = GCHandle.Alloc(pathBuffer, GCHandleType.Pinned);
				ImGui.GetIO().NativePtr->IniFilename = (byte*)pathHandle.AddrOfPinnedObject();
			}


			ImGui.StyleColorsDark();
			ImGuiStyle.ApplyStyle();

			defaultFont.Init();
			iconFont.Init();

			ImGui.GetIO().Fonts.AddFontDefault();
			ImGui.GetIO().Fonts.Build();

			ImGui.GetStyle().ScaleAllSizes(1.25f);

			unsafe
			{
				var ftatlas = ImFontAtlas_Get();


				IntPtr pixels;
				int width;
				int height;
				int bpp;


				ImGuiNative.ImFontAtlas_GetTexDataAsRGBA32((ImFontAtlas*)ftatlas, &pixels, &width, &height, &bpp);

				fontTex = new(width, height, TextureFormat.RGBA32, false);

				byte[] data = new byte[width * height * 4];
				Marshal.Copy(pixels, data, 0, data.Length);

				fontTex.LoadRawTextureData(data);
				fontTex.Apply();

				ImGuiNative.ImFontAtlas_SetTexID((ImFontAtlas*)ftatlas, fontTex.GetNativeTexturePtr());
				//RegisterTexture(fontTex);
			}

			TryHookWndProc();
		}


		public static bool openDemo = true;

		[StructLayout(LayoutKind.Sequential)]
		public unsafe struct ImDrawDataNative
		{
			public byte Valid;
			public int CmdListsCount;
			public int TotalIdxCount;
			public int TotalVtxCount;
			public IntPtr CmdLists;
			public Vector2 DisplayPos;
			public Vector2 DisplaySize;
			public Vector2 FramebufferScale;
			public IntPtr ownerViewport;

			public unsafe ImGuiViewportPtr OwnerViewport => new ImGuiViewportPtr(ownerViewport);
		}

		[DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
		public unsafe static extern IntPtr igGetDrawData();

		public static unsafe void Update()
		{
			TryHookWndProc();
			//UpdateInput();
		}

		private static void SendKey(ImGuiKey imguiKey, bool active)
		{
			unsafe
			{
				ImGuiNative.ImGuiIO_AddKeyEvent(ImGuiNative.igGetIO(), imguiKey, active ? (byte)1 : (byte)0);
			}
		}

		public static void Dispose()
		{
			if (hookedHwnd != IntPtr.Zero)
			{
				SetWindowLongPtr(hookedHwnd, GWL_WNDPROC, originalWndProc);
				hookedHwnd = IntPtr.Zero;
				originalWndProc = IntPtr.Zero;
				newWndProcDelegate = null;
			}

			ImGuiNative.igDestroyContext(_context);
			if (_imgDll != IntPtr.Zero)
			{
				//Yes it is required to freelib twice here
				//once for our own LoadLib (to get the handle) and once for [DllImport]
				//otherwise the file will still be in use and we can't delete it.
				FreeLibrary(_imgDll);
				FreeLibrary(_imgDll);
			}
		}

		static Material lineMaterial;
		static void CreateLineMaterial()
		{
			if (!lineMaterial)
			{
				Shader shader = Shader.Find("UI/Default");
				lineMaterial = new Material(shader)
				{
					hideFlags = HideFlags.HideAndDontSave
				};
				lineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
				lineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
				lineMaterial.SetInt("_ZWrite", 0);
				lineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
				lineMaterial.mainTexture = fontTex;
			}
		}

		
		internal delegate void UserDrawCallback(ImDrawListPtr parent_list, ImDrawCmdPtr cmd);

		private static UnityEngine.Rendering.CommandBuffer _cb;
		private static readonly List<Mesh> _meshes = [];
		private static readonly List<Vector3> _verts = [];
		private static readonly List<Vector2> _uvs = [];
		private static readonly List<Color32> _colors = [];
		private static readonly List<int> _indices =[];
		private static readonly MaterialPropertyBlock _mpb = new();

		internal static void OnDraw()
		{
			if (Event.current?.type != EventType.Repaint) return;

			CreateLineMaterial();
			SetDisplaySize(Screen.width, Screen.height);
			SetDeltaTime(Time.deltaTime);

			if(needsFontRebuild)
			{
				ImGui.GetIO().Fonts.ClearFonts();
				defaultFont.IsLoaded = false;
				defaultFont.Init();
				iconFont.IsLoaded = false;
				iconFont.Init();

				ImGui.GetIO().Fonts.AddFontDefault();


				foreach(var f in fontsToLoad)
				{
					((LunarisFont)f).IsLoaded = false;
					((LunarisFont)f).Init();
				}

				ImGui.GetIO().Fonts.Build();

				unsafe
				{
					var ftatlas = ImFontAtlas_Get();

					IntPtr pixels;
					int width;
					int height;
					int bpp;


					ImGuiNative.ImFontAtlas_GetTexDataAsRGBA32((ImFontAtlas*)ftatlas, &pixels, &width, &height, &bpp);

					fontTex = new(width, height, TextureFormat.RGBA32, false);

					byte[] data = new byte[width * height * 4];
					Marshal.Copy(pixels, data, 0, data.Length);

					fontTex.LoadRawTextureData(data);
					fontTex.Apply();

					ImGuiNative.ImFontAtlas_SetTexID((ImFontAtlas*)ftatlas, fontTex.GetNativeTexturePtr());
				}

				needsFontRebuild = false;
			}


			ImGui.NewFrame();
			//SaveAllStacks(); //these two custom cimgui functions
			OnRender?.Invoke();
			//PopAllStacks(); //prevent crashing if anything is still open
			ImGui.EndFrame();

			isCapturingInput = ImGui.IsAnyItemHovered() || ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow) || ImGui.IsAnyItemActive() || ImGui.GetIO().WantCaptureMouse || ImGui.GetIO().WantCaptureKeyboard;

			ImGui.Render();

			RenderDrawData();
		}


		//This is the "dont look at me" function
		private static void RenderDrawData()
		{
			_cb ??= new UnityEngine.Rendering.CommandBuffer { name = "ImGui" };

			float screenW = Screen.width;
			float screenH = Screen.height;

			var proj = Matrix4x4.Ortho(0, screenW, screenH, 0, -1, 1);

			_cb.Clear();
			_cb.SetProjectionMatrix(proj);
			_cb.SetViewMatrix(Matrix4x4.identity);

			unsafe
			{
				ImDrawDataNative* drawData = (ImDrawDataNative*)igGetDrawData();
				if (drawData->CmdListsCount == 0) return;

				int vtxSize = sizeof(ImDrawVert);
				int cmdSize = sizeof(ImDrawCmd);
				float dispX = drawData->DisplayPos.x;
				float dispY = drawData->DisplayPos.y;

				while (_meshes.Count < drawData->CmdListsCount)
				{
					var m = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
					m.MarkDynamic();
					_meshes.Add(m);
				}

				for (int n = 0; n < drawData->CmdListsCount; n++)
				{
					ImDrawList* cmdList = ((ImDrawList**)drawData->CmdLists)[n];
					byte* vtxBase = (byte*)cmdList->VtxBuffer.Data;
					ushort* idxBase = (ushort*)cmdList->IdxBuffer.Data;
					byte* cmdBase = (byte*)cmdList->CmdBuffer.Data;

					int vtxCount = cmdList->VtxBuffer.Size;
					_verts.Clear();
					_uvs.Clear();
					_colors.Clear();

					for (int v = 0; v < vtxCount; v++)
					{
						ImDrawVert* vert = (ImDrawVert*)(vtxBase + v * vtxSize);
						_verts.Add(new Vector3(vert->pos.X - dispX, vert->pos.Y - dispY, 0));
						_uvs.Add(new Vector2(vert->uv.X, vert->uv.Y));
						uint c = vert->col;
						_colors.Add(new Color32((byte)(c & 0xFF), (byte)((c >> 8) & 0xFF), (byte)((c >> 16) & 0xFF), (byte)((c >> 24) & 0xFF)));
					}

					int cmdCount = cmdList->CmdBuffer.Size;

					var _mesh = _meshes[n];
					_mesh.Clear();
					_mesh.SetVertices(_verts);
					_mesh.SetUVs(0, _uvs);
					_mesh.SetColors(_colors);
					_mesh.subMeshCount = cmdCount;

					for (int i = 0; i < cmdCount; i++)
					{
						ImDrawCmd* cmd = (ImDrawCmd*)(cmdBase + i * cmdSize);
						_indices.Clear();
						for (int j = 0; j < (int)cmd->ElemCount; j++)
							_indices.Add(idxBase[cmd->IdxOffset + j] + (int)cmd->VtxOffset);
						_mesh.SetTriangles(_indices, i);
					}

					_mesh.UploadMeshData(false);

					for (int i = 0; i < cmdCount; i++)
					{
						ImDrawCmd* cmd = (ImDrawCmd*)(cmdBase + i * cmdSize);
						if (cmd->ElemCount == 0) continue;
						if (cmd->UserCallback != IntPtr.Zero)
						{
							var cb = Marshal.GetDelegateForFunctionPointer<UserDrawCallback>(cmd->UserCallback);
							cb(cmdList, cmd);
							continue;
						}

						int clipX = (int)(cmd->ClipRect.X - dispX);
						int clipY = (int)(cmd->ClipRect.Y - dispY);
						int clipW = (int)(cmd->ClipRect.Z - cmd->ClipRect.X);
						int clipH = (int)(cmd->ClipRect.W - cmd->ClipRect.Y);
						_cb.EnableScissorRect(new Rect(clipX, screenH - clipY - clipH, clipW, clipH));

						_mpb.Clear();
						if (_textures.TryGetValue(cmd->TextureId, out Texture tex))
						{
							_mpb.SetTexture("_MainTex", tex);
							//We need to y-flip textures
							_mpb.SetVector("_MainTex_ST", new Vector4(1, -1, 0, 1));
						}
						else
						{
							_mpb.SetTexture("_MainTex", fontTex);
							//The font texture is from imgui, and that's already in the correct y-orientation
							_mpb.SetVector("_MainTex_ST", new Vector4(1, 1, 0, 0));
						}

						_cb.DrawMesh(_mesh, Matrix4x4.identity, lineMaterial, i, 0, _mpb);
					}

					_cb.DisableScissorRect();
				}
			}

			Graphics.ExecuteCommandBuffer(_cb);
		}

	}
}
