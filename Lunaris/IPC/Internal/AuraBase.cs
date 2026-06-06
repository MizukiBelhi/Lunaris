using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris.IPC.Internal
{

	/// <exclude/>
	internal abstract class AuraBase(string label, LunarisPlugin caller)
	{
		public string Label => label;
		public LunarisPlugin Caller = caller;
		public bool IsAvailable => AuraRegistry.GetHandler(label) != null;
		public bool HasFunction => IsAvailable && AuraRegistry.GetHandler(label).Method.ReturnType != typeof(void);
		public bool HasAction => IsAvailable && AuraRegistry.GetHandler(label).Method.ReturnType == typeof(void);

		protected void SetHandler(Delegate d) => AuraRegistry.RegisterHandler(label, d, Caller);
		protected void ClearHandler() => AuraRegistry.UnregisterHandler(label, Caller);


		protected void Broadcast(params object[] args)
		{
			var ev = AuraRegistry.GetEvent(label);
			ev?.DynamicInvoke(args);
		}

		protected void AddListener(Delegate d) => AuraRegistry.AddEvent(label, d);
		protected void RemoveListener(Delegate d) => AuraRegistry.RemoveEvent(label, d);
		protected T InvokeRemote<T>(params object[] args)
		{
			var handler = AuraRegistry.GetHandler(label);
			if (handler == null) return default;

			try
			{
				return (T)handler.DynamicInvoke(args);
			}
			catch
			{
				return default;
			}
		}

		protected void InvokeRemote(params object[] args)
		{
			var handler = AuraRegistry.GetHandler(label);
			handler?.DynamicInvoke(args);
		}
		protected void InvokeRemoteAction(params object[] args)
		{
			var handler = AuraRegistry.GetHandler(label);
			if (HasAction)
			{
				handler.DynamicInvoke(args);
			}
		}

	}
}
