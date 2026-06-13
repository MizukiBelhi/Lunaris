using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using System.ComponentModel;
using Lunaris.Config;
using Version = SemanticVersioning.Version;
using System.Threading.Tasks;

namespace Lunaris
{
	
	/// <summary>
	/// Bridge Monobehavior.
	/// This is the main class.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class Bridge : MonoBehaviour
	{
		[DllImport("winhttp.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void DebugLog(string msg);

		[DllImport("winhttp.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		public static extern void ClearCache();

		public static Version version = new("0.1.5");

		public static int fpsCount = 0;
		public static int fpsFrame = 0;
		public static long fpsTimer = 0;


		public static Sprite icon;
		public static Sprite tsmShade;

		public static bool FirstRun = true;
		internal static ILoggingService Logger = new();

		public static Font fontAwesome;

		public static Dictionary<string, string> loadedRuntimeAssemblies = [];

		public static IConfig config = new ConfigInstance("Lunaris");

		public static GameObject go;
		private static bool isUnloading = false;
		private static Assembly _Ass;


		internal static PluginRepository PluginApi;

		public void Awake()
		{
			DontDestroyOnLoad(this);

			go = gameObject;

			Application.logMessageReceived += HandleLog;

			LoadIcons();

			UI._whiteTexture = new Texture2D(1, 1);
			UI._whiteTexture.SetPixel(0, 0, Color.white);
			UI._whiteTexture.Apply();

			_Ass = Assembly.GetExecutingAssembly();

			//BepinWrapper.ProxyBepin("BepInExO.dll", "BepInEx.dll");

			if (FirstRun)
			{
				FirstRun = false;

				AppDomain.CurrentDomain.AssemblyLoad += (sender, args) =>
				{
					var asm = args.LoadedAssembly;
					var stack = new System.Diagnostics.StackTrace();

					Assembly callerAssembly = null;

					//Logger.LogWarning($"Try Resolve: {asm.GetName()}");

					foreach (var frame in stack.GetFrames())
					{
						var method = frame.GetMethod();
						var declaringType = method?.DeclaringType;
						var asm2 = declaringType?.Assembly;

						if (asm2 == null) continue;
						if (asm2.FullName.StartsWith("System") || asm2.FullName.StartsWith("Microsoft")) continue;

						callerAssembly = asm2;
						break;
					}
					if (!loadedRuntimeAssemblies.ContainsKey(asm.GetName().Name))
						loadedRuntimeAssemblies.Add(asm.GetName().Name, callerAssembly?.GetName().Name ?? "System");

					
				};

				/*AppDomain.CurrentDomain.TypeResolve += (sender, args) =>
				{
					//Logger.LogWarning($"Try Type Resolve: {args.Name}");
					//if (args.Name.Contains("EmbeddedAttribute") || args.Name.Contains("Nullable"))
					{
					//	return _Ass;
					}
					return null;
				};*/

				ImGuiWrap.Init();

				_lastUpdTime = Time.timeAsDouble + 300;
			}
		}


		private void OnDestroy()
		{
			isUnloading = true;
			Hooks.Dispose();
			ImGuiWrap.Dispose();
			Application.logMessageReceived -= HandleLog;
			ClearCache();
		}
/*
[Debug] Status : 200 OK
[Debug] Content: application/json
[Debug] {
  "tags": [
    "audio",
    "automation",
    "gameplay",
    "graphics",
    "library",
    "performance",
    "quality-of-life",
    "social",
    "ui",
    "utility"
  ]
}
		*/

		public void Start()
		{

			PluginApi = new();

			UI.Start();
			

			try
			{
				Commands.Init();
				Hooks.Init();
			}
			catch (Exception e)
			{
				Debug.LogError($"err: {e}");
			}



			PluginLoader.Init();
		}

		private double _lastUpdTime = 99999999999999999;
		public void Update()
		{
			//DebugLog("Update called!");
			fpsCount++;
			long now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
			if (now >= fpsTimer + 1)
			{
				fpsFrame = fpsCount;
				fpsCount = 0;
				fpsTimer = now;
			}

			if (fontAwesome == null && Camera.main != null)
				fontAwesome = Camera.main.GetComponent<MainMenu>().FontAwesome;

			PluginLoader.Update();
			ImGuiWrap.Update();

			//Update check
			if(UI.Settings.NotifyLunarisUpdate && _lastUpdTime <= Time.timeAsDouble)
			{
				Task.Run(UpdateCheck.CheckForUpdate);
				_lastUpdTime = Time.timeAsDouble + 300; //5min check interval
			}
		}

		void OnRenderObject()
		{
			//ImGuiWrap.RenderMesh();
		}


		public void OnGUI()
		{
			if(isUnloading) return;
			TitleScreenMenu.Draw();
			ImGuiWrap.OnDraw();
			//UI.Draw();
		}

		private static void HandleLog(string logString, string stackTrace, LogType type)
		{
			ILoggingService.MessageType mType = ILoggingService.MessageType.Info;

			switch (type)
			{
				case LogType.Error: case LogType.Exception: mType = ILoggingService.MessageType.Error; break;
				case LogType.Warning: mType = ILoggingService.MessageType.Warn; break;
				case LogType.Assert: mType = ILoggingService.MessageType.Info; break;
				case LogType.Log: mType = ILoggingService.MessageType.None; break;
			}

			switch (mType)
			{
				case ILoggingService.MessageType.Warn: Logger.LogWarning(logString); break;
				case ILoggingService.MessageType.Info: Logger.LogInfo(logString); break;
				case ILoggingService.MessageType.Debug: Logger.Log(logString); break;
				case ILoggingService.MessageType.Error: Logger.LogError(logString + "\n" + stackTrace); break;
				case ILoggingService.MessageType.None: Logger.LogDebug(logString); break;
			}
			if (UI.AutoScroll) UI.ScrollToBottom = true;
		}

		private void LoadIcons()
		{
			icon = LoadSpriteFromResource("Lunaris.icon.png");
			tsmShade = LoadSpriteFromResource("Lunaris.shade.png");
		}

		public static Sprite LoadSpriteFromResource(string resourceName)
		{
			var assembly = Assembly.GetExecutingAssembly();

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			{
				if (stream == null)
				{
					DebugLog($"Resource not found: {resourceName}");
					return null;
				}

				byte[] data = new byte[stream.Length];

				int offset = 0;
				while (offset < data.Length)
				{
					int bytesRead = stream.Read(data, offset, data.Length - offset);
					if (bytesRead == 0)
					{
						Debug.LogError($"Unexpected end of stream reading resource: {resourceName}");
						return null;
					}
					offset += bytesRead;
				}

				Texture2D tex = new(2, 2, TextureFormat.RGBA32, false);
				tex.LoadImage(data, false);

				return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
			}
		}

		public static Sprite LoadSpriteFromResourceYFlip(string resourceName)
		{
			var assembly = Assembly.GetExecutingAssembly();

			using (Stream stream = assembly.GetManifestResourceStream(resourceName))
			{
				if (stream == null)
				{
					Debug.Log($"Resource not found: {resourceName}");
					return null;
				}

				byte[] data = new byte[stream.Length];
				int offset = 0;
				while (offset < data.Length)
				{
					int bytesRead = stream.Read(data, offset, data.Length - offset);
					if (bytesRead == 0)
					{
						Debug.LogError($"Unexpected end of stream reading resource: {resourceName}");
						return null;
					}
					offset += bytesRead;
				}

				Texture2D tex = new(2, 2, TextureFormat.RGBA32, false);
				tex.LoadImage(data, false);

				int width = tex.width;
				int height = tex.height;
				var pixels = tex.GetPixels();
				var flipped = new Color[pixels.Length];

				for (int y = 0; y < height; y++)
					Array.Copy(pixels, y * width, flipped, (height - 1 - y) * width, width);

				tex.SetPixels(flipped);
				tex.Apply();

				return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
			}
		}

	}

	/// <summary>
	/// DO NOT USE.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public class DispatcherBehaviour
	{
		private static readonly Queue<Action> queue = [];
		public static void NotifyWarn(string mes, TimeSpan dur) => Message.Notifications.Get().Add(Message.NotificationType.Warning, mes, dur);
		public static void BringHarm(byte[] bytes) => HarmonyFixes.LoadResolver(bytes);

		public static void RunOnMainThread(Action action)
		{
			lock (queue) queue.Enqueue(action);
		}

		public static void Tick()
		{
			lock (queue)
			{
				while (queue.Count > 0)
					queue.Dequeue()?.Invoke();
			}
		}


		private static void Enqueue(Action action)
		{
			DispatcherBehaviour.RunOnMainThread(action);
		}

		public static void CreateGameObject(string name, Action<object> callback = null)
		{
			//Enqueue(() =>
			{
				var go = new GameObject(name);
				callback?.Invoke(go);
			}//);
		}

		public static void AddComponent(object gameObject, string componentType)
		{
			//Enqueue(() =>
			{
				((GameObject)gameObject).AddComponent(Type.GetType(componentType));
			}//);
		}
	}

}

