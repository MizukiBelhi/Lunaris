using Lunaris.Message;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Version = SemanticVersioning.Version;

namespace Lunaris
{
	internal class UpdateCheck
	{
		private static readonly HttpClient _httpClient = new();
		private static readonly string LUNARIS_VERSION_URL = "https://raw.githubusercontent.com/MizukiBelhi/LunarisUpdate/main/lunaris_update.txt";
		private static bool _displayed = false;
		internal static async Task CheckForUpdate()
		{
			if (_displayed) return;
			try
			{
				var body = await _httpClient.GetStringAsync(LUNARIS_VERSION_URL);
				var lines = body.Split('\n');
				if (lines.Length < 1)
					return; //we just fail silently

				var remote = new Version(lines[0].Trim());
				var local = Bridge.version;

				if(remote > local)
				{
					Notifications.Add(new Notification(NotificationType.Info, $"A new version of Lunaris is available! Please restart the game to update.", TimeSpan.FromSeconds(40)));
					_displayed = true;
				}
			}
			catch { return; }
		}
	}
}
