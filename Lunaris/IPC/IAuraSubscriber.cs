using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris.IPC
{
	public interface IAuraSubscriber
	{
		public bool HasAction { get; }
		public bool HasFunction { get; }
	}

	public interface IAuraSubscriber<TRet> : IAuraSubscriber
	{
		public void Subscribe(Action action);
		public void Unsubscribe(Action action);
		public void InvokeAction();
		public TRet InvokeFunc();
	}

	public interface IAuraSubscriber<T1, TRet> : IAuraSubscriber
	{
		public void Subscribe(Action<T1> action);
		public void Unsubscribe(Action<T1> action);
		public void InvokeAction(T1 arg1);
		public TRet InvokeFunc(T1 arg1);
	}

	public interface IAuraSubscriber<T1, T2, TRet> : IAuraSubscriber
	{
		public void Subscribe(Action<T1, T2> action);
		public void Unsubscribe(Action<T1, T2> action);
		public void InvokeAction(T1 arg1, T2 arg2);
		public TRet InvokeFunc(T1 arg1, T2 arg2);
	}

	public interface IAuraSubscriber<T1, T2, T3, TRet> : IAuraSubscriber
	{
		public void Subscribe(Action<T1, T2, T3> action);
		public void Unsubscribe(Action<T1, T2, T3> action);
		public void InvokeAction(T1 arg1, T2 arg2, T3 arg3);
		public TRet InvokeFunc(T1 arg1, T2 arg2, T3 arg3);
	}

	public interface IAuraSubscriber<T1, T2, T3, T4, TRet> : IAuraSubscriber
	{
		public void Subscribe(Action<T1, T2, T3, T4> action);
		public void Unsubscribe(Action<T1, T2, T3, T4> action);
		public void InvokeAction(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
		public TRet InvokeFunc(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
	}

	public interface IAuraSubscriber<T1, T2, T3, T4, T5, TRet> : IAuraSubscriber
	{
		public void Subscribe(Action<T1, T2, T3, T4, T5> action);
		public void Unsubscribe(Action<T1, T2, T3, T4, T5> action);
		public void InvokeAction(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
		public TRet InvokeFunc(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
	}

	public interface IAuraSubscriber<T1, T2, T3, T4, T5, T6, TRet> : IAuraSubscriber
	{
		public void Subscribe(Action<T1, T2, T3, T4, T5, T6> action);
		public void Unsubscribe(Action<T1, T2, T3, T4, T5, T6> action);
		public void InvokeAction(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
		public TRet InvokeFunc(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
	}

	public interface IAuraSubscriber<T1, T2, T3, T4, T5, T6, T7, TRet> : IAuraSubscriber
	{
		public void Subscribe(Action<T1, T2, T3, T4, T5, T6, T7> action);
		public void Unsubscribe(Action<T1, T2, T3, T4, T5, T6, T7> action);
		public void InvokeAction(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
		public TRet InvokeFunc(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
	}

	public interface IAuraSubscriber<T1, T2, T3, T4, T5, T6, T7, T8, TRet> : IAuraSubscriber
	{
		public void Subscribe(Action<T1, T2, T3, T4, T5, T6, T7, T8> action);
		public void Unsubscribe(Action<T1, T2, T3, T4, T5, T6, T7, T8> action);
		public void InvokeAction(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
		public TRet InvokeFunc(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
	}
}
