using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris.IPC.Internal
{
	internal static class AuraRegistry
	{
		private static readonly ConcurrentDictionary<string, Delegate> _handlers = new();
		private static readonly ConcurrentDictionary<string, Delegate> _events = new();
		private static readonly ConcurrentDictionary<string, LunarisPlugin> _owners = new();

		internal static void RegisterHandler(string label, Delegate del, LunarisPlugin caller)
		{

			if (_owners.TryGetValue(label, out var owner) && owner != caller)
			{
				throw new UnauthorizedAccessException($"Aura: {label} is owned by {owner.name}. {caller.name} cannot overwrite it.");
			}

			_owners[label] = caller;
			_handlers[label] = del;
		}

		internal static void UnregisterHandler(string label, LunarisPlugin caller)
		{
			if (_owners.TryGetValue(label, out var owner) && owner == caller)
			{
				_handlers.TryRemove(label, out _);
				_owners.TryRemove(label, out _);
			}
		}

		internal static Delegate GetHandler(string label) => _handlers.TryGetValue(label, out var d) ? d : null;
		internal static void AddEvent(string label, Delegate del) => _events.AddOrUpdate(label, del, (key, existing) => Delegate.Combine(existing, del));
		internal static void RemoveEvent(string label, Delegate del) => _events.AddOrUpdate(label, (Delegate)null, (key, existing) => Delegate.Remove(existing, del));
		internal static Delegate GetEvent(string label) => _events.TryGetValue(label, out var d) ? d : null;
	}
}
