using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lunaris.Message;

namespace Lunaris
{
	internal static class PluginUpdateChecker
	{
		private static readonly List<PluginInstaller.PluginListItem> _checkQueue = [];
		private static readonly Dictionary<string, string> _pendingUpdates = [];
		private static int _completedChecks = 0;

		internal static void Enqueue(PluginDescriptor desc)
		{
			var item = UI.installer.pluginsInstalled.FirstOrDefault(t => t.desc == desc);
			if (item == null) return;
			if (desc.Manifest == null || string.IsNullOrEmpty(desc.Manifest.Id)) return;
			_checkQueue.Add(item);
		}

		internal static void CheckAll()
		{
			if (_checkQueue.Count == 0)
			{
				PluginLoader.RunLoadQueue();
				return;
			}

			Task.Run(async () =>
			{
				var req = _checkQueue.Select(v => v.desc).ToList();
				var allUpdates = await Bridge.PluginApi.FetchAllUpdates([.. req.Select(t => t.Manifest)]);

				var nonUpdates = req.Where(t => !allUpdates.ContainsKey(t.Manifest.Id)).ToList();

				DispatcherBehaviour.RunOnMainThread(() =>
				{
					//_completedChecks++;

					foreach (var upd in allUpdates)
					{
						var item = _checkQueue.FirstOrDefault(t => t.desc.Manifest.Id == upd.Key);
						if(item == null) continue;

						var desc = item.desc;

						item.hasUpdate = true;
						_pendingUpdates[desc.Manifest.DisplayName] = upd.Value;
					}

					foreach(var desc in nonUpdates)
					{
						PluginLoader.EnqueueLoad(desc);
					}

					//if (_completedChecks == _checkQueue.Count)
					OnAllChecksComplete();
				});
			});
		}

		private static void OnAllChecksComplete()
		{
			if (UI.Settings.NotifyPluginUpdate)
			{
				if (_pendingUpdates.Count > 1)
					Notifications.Add(new Notification(NotificationType.Info, $"{_pendingUpdates.Count} plugins have updates available.", Notifications.DefaultDuration));
				else if (_pendingUpdates.Count == 1)
					Notifications.Add(new Notification(NotificationType.Info, $"{_pendingUpdates.First().Key} has an update available.", Notifications.DefaultDuration));
			}

			if (UI.Settings.AutoUpdatePlugin)
			{
				foreach (var kv in _pendingUpdates)
				{
					var name = kv.Key;
					var ver = kv.Value;
					var item = UI.installer.pluginsInstalled.FirstOrDefault(t => t.desc.Manifest.DisplayName == name);
					item?.Update(ver);
				}
			}
			else
			{
				foreach (var kv in _pendingUpdates)
				{
					var name = kv.Key;
					var ver = kv.Value;
					var item = UI.installer.pluginsInstalled.FirstOrDefault(t => t.desc.Manifest.DisplayName == name);
					if(item != null)
						item.SelectedVersion = ver;
				}
			}

			_pendingUpdates.Clear();
			PluginLoader.RunLoadQueue();
		}
	}
}