using System;
using System.Collections.Generic;


namespace Lunaris.IPC
{
	using Internal;


	internal class Aura(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider, IAuraSubscriber
	{
		public void RegisterAction(Action a) => SetHandler(a);
		public void RegisterFunc(Action f) => SetHandler(f);
		public void SendMessage() => Broadcast();
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action a) => AddListener(a);
		public void Unsubscribe(Action a) => RemoveListener(a);
		public void InvokeAction() => InvokeRemoteAction();
		public void InvokeFunc() => InvokeRemote();
	}



	internal class Aura<TRet>(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider<TRet>, IAuraSubscriber<TRet>
	{
		public void RegisterAction(Action a) => SetHandler(a);
		public void RegisterFunc(Func<TRet> f) => SetHandler(f);
		public void SendMessage() => Broadcast();
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action a) => AddListener(a);
		public void Unsubscribe(Action a) => RemoveListener(a);
		public void InvokeAction() => InvokeRemoteAction();
		public TRet InvokeFunc() => InvokeRemote<TRet>();
	}


	internal class Aura<T1, TRet>(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider<T1, TRet>, IAuraSubscriber<T1, TRet>
	{
		public void RegisterAction(Action<T1> a) => SetHandler(a);
		public void RegisterFunc(Func<T1, TRet> f) => SetHandler(f);
		public void SendMessage(T1 p1) => Broadcast(p1);
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action<T1> a) => AddListener(a);
		public void Unsubscribe(Action<T1> a) => RemoveListener(a);
		public void InvokeAction(T1 p1) => InvokeRemoteAction(p1);
		public TRet InvokeFunc(T1 p1) => InvokeRemote<TRet>(p1);
	}


	internal class Aura<T1, T2, TRet>(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider<T1, T2, TRet>, IAuraSubscriber<T1, T2, TRet>
	{
		public void RegisterAction(Action<T1, T2> a) => SetHandler(a);
		public void RegisterFunc(Func<T1, T2, TRet> f) => SetHandler(f);
		public void SendMessage(T1 p1, T2 p2) => Broadcast(p1, p2);
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action<T1, T2> a) => AddListener(a);
		public void Unsubscribe(Action<T1, T2> a) => RemoveListener(a);
		public void InvokeAction(T1 p1, T2 p2) => InvokeRemoteAction(p1, p2);
		public TRet InvokeFunc(T1 p1, T2 p2) => InvokeRemote<TRet>(p1, p2);
	}

	internal class Aura<T1, T2, T3, TRet>(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider<T1, T2, T3, TRet>, IAuraSubscriber<T1, T2, T3, TRet>
	{
		public void RegisterAction(Action<T1, T2, T3> a) => SetHandler(a);
		public void RegisterFunc(Func<T1, T2, T3, TRet> f) => SetHandler(f);
		public void SendMessage(T1 p1, T2 p2, T3 p3) => Broadcast(p1, p2, p3);
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action<T1, T2, T3> a) => AddListener(a);
		public void Unsubscribe(Action<T1, T2, T3> a) => RemoveListener(a);
		public void InvokeAction(T1 p1, T2 p2, T3 p3) => InvokeRemoteAction(p1, p2, p3);
		public TRet InvokeFunc(T1 p1, T2 p2, T3 p3) => InvokeRemote<TRet>(p1, p2, p3);
	}

	internal class Aura<T1, T2, T3, T4, TRet>(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider<T1, T2, T3, T4, TRet>, IAuraSubscriber<T1, T2, T3, T4, TRet>
	{
		public void RegisterAction(Action<T1, T2, T3, T4> a) => SetHandler(a);
		public void RegisterFunc(Func<T1, T2, T3, T4, TRet> f) => SetHandler(f);
		public void SendMessage(T1 p1, T2 p2, T3 p3, T4 p4) => Broadcast(p1, p2, p3, p4);
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action<T1, T2, T3, T4> a) => AddListener(a);
		public void Unsubscribe(Action<T1, T2, T3, T4> a) => RemoveListener(a);
		public void InvokeAction(T1 p1, T2 p2, T3 p3, T4 p4) => InvokeRemoteAction(p1, p2, p3, p4);
		public TRet InvokeFunc(T1 p1, T2 p2, T3 p3, T4 p4) => InvokeRemote<TRet>(p1, p2, p3, p4);
	}

	internal class Aura<T1, T2, T3, T4, T5, TRet>(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider<T1, T2, T3, T4, T5, TRet>, IAuraSubscriber<T1, T2, T3, T4, T5, TRet>
	{
		public void RegisterAction(Action<T1, T2, T3, T4, T5> a) => SetHandler(a);
		public void RegisterFunc(Func<T1, T2, T3, T4, T5, TRet> f) => SetHandler(f);
		public void SendMessage(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5) => Broadcast(p1, p2, p3, p4, p5);
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action<T1, T2, T3, T4, T5> a) => AddListener(a);
		public void Unsubscribe(Action<T1, T2, T3, T4, T5> a) => RemoveListener(a);
		public void InvokeAction(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5) => InvokeRemoteAction(p1, p2, p3, p4, p5);
		public TRet InvokeFunc(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5) => InvokeRemote<TRet>(p1, p2, p3, p4, p5);
	}

	internal class Aura<T1, T2, T3, T4, T5, T6, TRet>(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider<T1, T2, T3, T4, T5, T6, TRet>, IAuraSubscriber<T1, T2, T3, T4, T5, T6, TRet>
	{
		public void RegisterAction(Action<T1, T2, T3, T4, T5, T6> a) => SetHandler(a);
		public void RegisterFunc(Func<T1, T2, T3, T4, T5, T6, TRet> f) => SetHandler(f);
		public void SendMessage(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6) => Broadcast(p1, p2, p3, p4, p5, p6);
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action<T1, T2, T3, T4, T5, T6> a) => AddListener(a);
		public void Unsubscribe(Action<T1, T2, T3, T4, T5, T6> a) => RemoveListener(a);
		public void InvokeAction(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6) => InvokeRemoteAction(p1, p2, p3, p4, p5, p6);
		public TRet InvokeFunc(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6) => InvokeRemote<TRet>(p1, p2, p3, p4, p5, p6);
	}

	internal class Aura<T1, T2, T3, T4, T5, T6, T7, TRet>(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider<T1, T2, T3, T4, T5, T6, T7, TRet>, IAuraSubscriber<T1, T2, T3, T4, T5, T6, T7, TRet>
	{
		public void RegisterAction(Action<T1, T2, T3, T4, T5, T6, T7> a) => SetHandler(a);
		public void RegisterFunc(Func<T1, T2, T3, T4, T5, T6, T7, TRet> f) => SetHandler(f);
		public void SendMessage(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7) => Broadcast(p1, p2, p3, p4, p5, p6, p7);
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action<T1, T2, T3, T4, T5, T6, T7> a) => AddListener(a);
		public void Unsubscribe(Action<T1, T2, T3, T4, T5, T6, T7> a) => RemoveListener(a);
		public void InvokeAction(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7) => InvokeRemoteAction(p1, p2, p3, p4, p5, p6, p7);
		public TRet InvokeFunc(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7) => InvokeRemote<TRet>(p1, p2, p3, p4, p5, p6, p7);
	}

	internal class Aura<T1, T2, T3, T4, T5, T6, T7, T8, TRet>(string label, LunarisPlugin t) : AuraBase(label, t), IAuraProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet>, IAuraSubscriber<T1, T2, T3, T4, T5, T6, T7, T8, TRet>
	{
		public void RegisterAction(Action<T1, T2, T3, T4, T5, T6, T7, T8> a) => SetHandler(a);
		public void RegisterFunc(Func<T1, T2, T3, T4, T5, T6, T7, T8, TRet> f) => SetHandler(f);
		public void SendMessage(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8) => Broadcast(p1, p2, p3, p4, p5, p6, p7, p8);
		public void UnregisterAction() => ClearHandler();
		public void UnregisterFunc() => ClearHandler();
		public void Subscribe(Action<T1, T2, T3, T4, T5, T6, T7, T8> a) => AddListener(a);
		public void Unsubscribe(Action<T1, T2, T3, T4, T5, T6, T7, T8> a) => RemoveListener(a);
		public void InvokeAction(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8) => InvokeRemoteAction(p1, p2, p3, p4, p5, p6, p7, p8);
		public TRet InvokeFunc(T1 p1, T2 p2, T3 p3, T4 p4, T5 p5, T6 p6, T7 p7, T8 p8) => InvokeRemote<TRet>(p1, p2, p3, p4, p5, p6, p7, p8);
	}


	/*internal static class IPC
	{
		private static readonly Dictionary<string, Delegate> _registry = new();
		internal static void Register(string label, Delegate del) => _registry[label] = del;
		internal static void Unregister(string label) => _registry.Remove(label);
		internal static Delegate Get(string label) => _registry.TryGetValue(label, out var del) ? del : null;
	}*/

}

