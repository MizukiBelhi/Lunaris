using System;
using System.Collections.Generic;
using UnityEngine;
using ImGuiNET;
using Lunaris.IGUI;

namespace Lunaris.Message
{
	public enum NotificationType
	{
		None,
		Info,
		Warning,
		Error,
	}

	public interface INotifications
	{
		void Add(NotificationType notificationType, string header, string content = "", TimeSpan? duration = null);
		void Add(NotificationType notificationType, string header, TimeSpan? duration = null);
		void Add(NotificationType notificationType, string header);
		void Add(string header, string content = "", TimeSpan? duration = null);
		void Add(string header, TimeSpan? duration = null);
		void Add(string header);
	}

	internal class Notifications : INotifications
	{
		public const string WidthString = "This is a long string idk what im doing here\nbut we set the width with it.";

		public static readonly TimeSpan DefaultDuration = TimeSpan.FromSeconds(7);

		private static readonly List<Notification> _notifications = [];

		private static readonly Notifications _instance = new();

		internal static Notifications Get()
		{
			return _instance;
		}

		internal static void Add(Notification notification)
		{
			lock (_notifications)
			{
				_notifications.Add(notification);
			}
		}

		public void Add(NotificationType notificationType, string header, string content = "", TimeSpan? duration = null)
		{
			var notification = new Notification(notificationType, header, content, duration);
			lock (_notifications)
			{
				_notifications.Add(notification);
			}
		}

		public void Add(NotificationType notificationType, string header, TimeSpan? duration = null)
		{
			var notification = new Notification(notificationType, header, duration);
			lock (_notifications)
			{
				_notifications.Add(notification);
			}
		}

		public void Add(NotificationType notificationType, string header)
		{
			var notification = new Notification(notificationType, header, null);
			lock (_notifications)
			{
				_notifications.Add(notification);
			}
		}

		public void Add(string header, string content = "", TimeSpan? duration = null)
		{
			var notification = new Notification(header, content, duration);
			lock (_notifications)
			{
				_notifications.Add(notification);
			}
		}

		public void Add(string header, TimeSpan? duration = null)
		{
			var notification = new Notification(header, duration);
			lock (_notifications)
			{
				_notifications.Add(notification);
			}
		}

		public void Add(string header)
		{
			var notification = new Notification(header, null);
			lock (_notifications)
			{
				_notifications.Add(notification);
			}
		}

		internal static void Draw()
		{
			int offset = _notifications.Count;
			int idx = 0;

			var width = ImGui.CalcTextSize(WidthString).X;

			List<Notification> deletionList = [];
			lock (_notifications)
			{
				var _not = _notifications;
				for(int i=0; i< _not.Count; i++)
				{
					var notification = _not[i];
					notification.Draw(width, offset - idx);
					idx++;
					if (notification.QueuedForDeletion)
						deletionList.Add(notification);
				}

			
				foreach (var notification in deletionList)
					_notifications.Remove(notification);
			}
		}
	}


	internal class Notification
	{
		private string header = "";
		private string content = "";
		private DateTime endTime;
		private TimeSpan duration;

		private System.Numerics.Vector2 curPos = System.Numerics.Vector2.Zero;
		private float endYPos = 0;
		private Guid guid = Guid.NewGuid();
		private bool once = false;
		public bool QueuedForDeletion = false;

		private NotificationType notificationType = NotificationType.None;

		public Notification(NotificationType notificationType, string header, string content = "", TimeSpan? duration = null)
		{
			this.header = header;
			this.content = content;
			this.notificationType = notificationType;
			this.duration = duration ?? Notifications.DefaultDuration;
			endTime = DateTime.Now.Add(this.duration);
		}
		public Notification(NotificationType notificationType, string header, TimeSpan? duration = null)
		{
			this.header = header;
			this.notificationType = notificationType;
			this.duration = duration ?? Notifications.DefaultDuration;
			endTime = DateTime.Now.Add(this.duration);
		}

		public Notification(string header, string content = "", TimeSpan? duration = null)
		{

			this.header = header;
			this.content = content;
			this.duration = duration ?? Notifications.DefaultDuration;
			endTime = DateTime.Now.Add(this.duration);
		}

		public Notification(string header, TimeSpan? duration = null)
		{

			this.header = header;
			this.duration = duration ?? Notifications.DefaultDuration;
			endTime = DateTime.Now.Add(this.duration);
		}

		public void Draw(float width, int offset)
		{
			float screenWidth = ImGui.GetIO().DisplaySize.X;
			float screenHeight = ImGui.GetIO().DisplaySize.Y;

			if (!once)
			{
				curPos.X = screenWidth - (width) - 5;
				once = true;
			}

			float notificationHeight = 40;
			curPos.Y = screenHeight + notificationHeight - endYPos;

			endYPos = endYPos.MoveTowards((offset * (notificationHeight + 5)) + notificationHeight, 2);

			if (endTime <= DateTime.Now)
			{
				curPos.X += 3;
				if (curPos.X - 5 > screenWidth)
				{
					QueuedForDeletion = true;
				}
			}
			if (QueuedForDeletion)
				return;


			ImGui.SetNextWindowPos(curPos, ImGuiCond.Always, System.Numerics.Vector2.Zero);
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(width, notificationHeight), ImGuiCond.FirstUseEver);
			var col = UI.LunarisColors.Black with { W = 0.8f };
			ImGui.PushStyleColor(ImGuiCol.WindowBg, col);

			var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoSavedSettings;
			if (!ImGui.Begin($"##NotificationWindow{guid}", flags))
			{
				ImGui.End();
				ImGui.PopStyleColor();
				return;
			}

			var isHover = ImGui.IsWindowHovered();

			if (isHover) //Reset duration
			{
				endTime = DateTime.Now.Add(duration);
			}

			col = UI.LunarisColors.White;
			switch (notificationType)
			{
				case NotificationType.Info:
				col = UI.LunarisColors.HealerGreen;
				break;
				case NotificationType.Warning:
				col = UI.LunarisColors.LunarisOrange;
				break;
				case NotificationType.Error:
				col = UI.LunarisColors.LunarisRed;
				break;
				default:
				break;
			}

			ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(8, 2));
			float textHeight = 12;
			if(!string.IsNullOrEmpty(header))
				textHeight = ImGui.CalcTextSize(header).Y;
			float centerY = (notificationHeight - textHeight) * 0.5f;

			ImGui.SetCursorPosY(centerY);

			ImGui.PushStyleColor(ImGuiCol.Text, col);

			if (notificationType != NotificationType.None)
				DrawTypeIcon();

			ImGui.PopStyleColor();

			float wrapWidth = width - 10;
			if (notificationType != NotificationType.None) wrapWidth -= 25;

			float wrappedTextHeight = ImGui.CalcTextSize(header, wrapWidth).Y;

			centerY = (notificationHeight - wrappedTextHeight) * 0.5f;
			ImGui.SetCursorPosY(centerY);

			ImGui.PushFont(ImGuiWrap.defaultFont.ImFont);

			string[] words = header.Split(' ');
			string currentLine = "";
			float currentY = centerY;
			float startX = ImGui.GetCursorPosX();

			//wrapping ourselves here otherwise the text is too far apart
			foreach (var word in words)
			{
				string testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";
				if (ImGui.CalcTextSize(testLine).X > wrapWidth && !string.IsNullOrEmpty(currentLine))
				{
					ImGui.SetCursorPos(new System.Numerics.Vector2(startX, currentY));
					ImGui.Text(currentLine);
					currentY += textHeight-5;
					currentLine = word;
				}
				else
					currentLine = testLine;
			}

			if (!string.IsNullOrEmpty(currentLine))
			{
				ImGui.SetCursorPos(new System.Numerics.Vector2(startX, currentY));
				ImGui.Text(currentLine);
			}
			ImGui.PopFont();

			if (!isHover)
			{
				TimeSpan elapsed = DateTime.Now - endTime.Add(-duration);
				double percentage = (elapsed.TotalMilliseconds / duration.TotalMilliseconds);
				percentage = Math.Max(0, Math.Min(1, 1 - percentage));

				//float barHeight = 2.0f;
				ImGui.PushStyleColor(ImGuiCol.PlotHistogram, col);
				ImGui.SetCursorPosY(notificationHeight - 2);
				ImGui.ProgressBar((float)percentage, new System.Numerics.Vector2(ImGui.GetContentRegionAvail().X, 1));
				ImGui.PopStyleColor();
			}

			ImGui.End();
			ImGui.PopStyleColor();
			ImGui.PopStyleVar();
		}

		private void DrawTypeIcon()
		{
			ImGui.PushFont(ImGuiWrap.iconFont.ImFont);

			var icon = FontAwesomeIcon.Cow;
			switch (notificationType)
			{
				case NotificationType.Info:
				icon = FontAwesomeIcon.InfoCircle;
				break;
				case NotificationType.Warning:
				icon = FontAwesomeIcon.ExclamationCircle;
				break;
				case NotificationType.Error:
				icon = FontAwesomeIcon.TimesCircle;
				break;
				default:
				break;
			}
			ImGui.Text(UI.ToIconString(icon));
			ImGui.SameLine();

			ImGui.PopFont();
		}

	}


	/// <exclude/>
	internal static class FloatExtension
	{
		public static float MoveTowards(this float _t, float val, float speed)
		{
			if (_t < val)
				_t = Math.Min(_t + speed, val);
			else if (_t > val)
				_t = Math.Max(_t - speed, val);
			return _t;
		}
	}
}
