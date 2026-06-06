using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	public interface ILog
	{
		void Log(string msg);
		void LogVerbose(string msg);
		void LogDebug(string msg);
		void LogInfo(string msg);
		void LogWarning(string msg);
		void LogError(string msg);
		void LogSocial(ChatLogLine clg);
		void LogSocial(string msg, string color = "");
		void LogCombat(string msg, string color = "");
		void LogLocal(string msg, string color = "");
	}
}
