using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	internal class ILoggingService : ILog
	{
		public enum MessageType
		{
			Info,
			Warn,
			Error,
			Debug,
			Verbose,
			None,
		}
		private static class Color
		{
			public const uint Red = 0x0004;
			public const uint Blue = 0x0001;
			public const uint Green = 0x0002;
			public const uint Yellow = Red | Green;
			public const uint White = Red | Green | Blue;
		}
		private bool EnableConsoleLogging { get; set; } = true;

		private bool initConsole = false;

		[DllImport("winhttp.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
		private static extern void DebugLogSL(string msg);

		public ILoggingService()
		{
			LoadConsole();
		}

		private void LoadConsole()
		{
			if (initConsole) return;
			initConsole = true;
		}


		private void SetColor(uint color)
		{
			string ansi = "\x1b[37m";
			if (color == Color.Red) ansi = "\x1b[31m";
			if (color == Color.Green) ansi = "\x1b[32m";
			if (color == Color.Yellow) ansi = "\x1b[33m";
			if (color == Color.Blue) ansi = "\x1b[34m";

			DebugLogSL(ansi);
		}

		private void ResetColor() => DebugLogSL("\x1b[0m");

		private void WriteFile(string msg)
		{
			/*string docPath = AppContext.BaseDirectory;

			using (StreamWriter outputFile = new StreamWriter(Path.Combine(docPath, "LunarisLog.txt"), true))
			{
				outputFile.WriteLine(msg);
			}*/
		}

		private void Write(MessageType msgType, string message, bool writeFile = true)
		{
			LoadConsole();
			var mt = "[Unknown]";
			var color = Color.White;
			switch (msgType)
			{
				case MessageType.Info:
				color = Color.Green;
				mt = "[Info]";
				break;
				case MessageType.Warn:
				color = Color.Yellow;
				mt = "[Warning]";
				break;
				case MessageType.Error:
				color = Color.Red;
				mt = "[Error]";
				break;
				case MessageType.Debug:
				color = Color.Blue;
				mt = "[Debug]";
				break;
				case MessageType.None:
				color = Color.White;
				mt = "";
				break;
				case MessageType.Verbose:
				color = Color.White;
				mt = "[Verbose]";
				break;
				default:
				break;
			}

			if (writeFile)
				WriteFile($"{mt} {message}");

			if (EnableConsoleLogging)
			{
				if (UI.consoleLog.Count > 1024)
					UI.consoleLog.RemoveAt(0);
				UI.consoleLog.Add(new UI.ConsoleMessage { message = $"{message}", messageType = msgType });
			}

			SetColor(color);
			DebugLogSL(mt);
			ResetColor();
			DebugLogSL(" "+message+"\n");
		}

		public void LogVerbose(string msg) => Write(MessageType.Verbose, msg);
		public void LogDebug(string msg) => Write(MessageType.Debug, msg);
		public void Log(string msg) => Write(MessageType.Debug, msg);
		public void LogInfo(string msg) => Write(MessageType.Info, msg);
		public void LogWarning(string msg) => Write(MessageType.Warn, msg);
		public void LogError(string msg) => Write(MessageType.Error, msg);
		public void WriteType(MessageType type, string msg) => Write(type, msg);
		public void LogSocial(string msg, string color="")
		{
			if (GameData.ChatLog != null)
			{
				UpdateSocialLog.LogAdd(new ChatLogLine(msg, ChatLogLine.LogType.SystemMessages, color));
			}
		}

		public void LogSocial(ChatLogLine clg)
		{
			if(GameData.ChatLog != null)
			{
				UpdateSocialLog.LogAdd(clg);
			}
		}

		public void LogCombat(string msg, string color = "")
		{
			if(GameData.ChatLog != null)
			{
				if (string.IsNullOrEmpty(color))
					UpdateSocialLog.CombatLogAdd(msg);
				else
					UpdateSocialLog.CombatLogAdd(msg, color);
			}
		}

		public void LogLocal(string msg, string color = "")
		{
			if (GameData.ChatLog != null)
			{
				if (string.IsNullOrEmpty(color))
					UpdateSocialLog.LocalLogAdd(msg);
				else
					UpdateSocialLog.LocalLogAdd(msg, color);
			}
		}

	}
}
