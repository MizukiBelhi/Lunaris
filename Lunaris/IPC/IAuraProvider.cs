using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris.IPC
{
	public interface IAuraProvider
	{
		void UnregisterAction();
		void UnregisterFunc();
	}

	public interface IAuraProvider<TRet> : IAuraProvider
	{
		void RegisterAction(Action action);
		void RegisterFunc(Func<TRet> func);
		void SendMessage();
	}


	public interface IAuraProvider<T1, TRet> : IAuraProvider
	{
		void RegisterAction(Action<T1> action);
		void RegisterFunc(Func<T1, TRet> func);
		void SendMessage(T1 arg1);
	}

	public interface IAuraProvider<T1, T2, TRet> : IAuraProvider
	{
		void RegisterAction(Action<T1, T2> action);
		void RegisterFunc(Func<T1, T2, TRet> func);
		void SendMessage(T1 arg1, T2 arg2);
	}

	public interface IAuraProvider<T1, T2, T3, TRet> : IAuraProvider
	{
		void RegisterAction(Action<T1, T2, T3> action);
		void RegisterFunc(Func<T1, T2, T3, TRet> func);
		void SendMessage(T1 arg1, T2 arg2, T3 arg3);
	}

	public interface IAuraProvider<T1, T2, T3, T4, TRet> : IAuraProvider
	{
		void RegisterAction(Action<T1, T2, T3, T4> action);
		void RegisterFunc(Func<T1, T2, T3, T4, TRet> func);
		void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4);
	}

	public interface IAuraProvider<T1, T2, T3, T4, T5, TRet> : IAuraProvider
	{
		void RegisterAction(Action<T1, T2, T3, T4, T5> action);
		void RegisterFunc(Func<T1, T2, T3, T4, T5, TRet> func);
		void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5);
	}

	public interface IAuraProvider<T1, T2, T3, T4, T5, T6, TRet> : IAuraProvider
	{
		void RegisterAction(Action<T1, T2, T3, T4, T5, T6> action);
		void RegisterFunc(Func<T1, T2, T3, T4, T5, T6, TRet> func);
		void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
	}

	public interface IAuraProvider<T1, T2, T3, T4, T5, T6, T7, TRet> : IAuraProvider
	{
		void RegisterAction(Action<T1, T2, T3, T4, T5, T6, T7> action);
		void RegisterFunc(Func<T1, T2, T3, T4, T5, T6, T7, TRet> func);
		void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);
	}

	public interface IAuraProvider<T1, T2, T3, T4, T5, T6, T7, T8, TRet> : IAuraProvider
	{
		void RegisterAction(Action<T1, T2, T3, T4, T5, T6, T7, T8> action);
		void RegisterFunc(Func<T1, T2, T3, T4, T5, T6, T7, T8, TRet> func);
		void SendMessage(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7, T8 arg8);
	}
}
