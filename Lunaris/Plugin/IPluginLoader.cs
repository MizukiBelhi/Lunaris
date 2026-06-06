using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Lunaris.PluginLoader;

namespace Lunaris
{
	internal interface IPluginLoader
	{
		bool LoadPlugin(PluginDescriptor descriptor, bool full);
		bool LoadPluginFromBytes(PluginDescriptor descriptor);
	}
}
