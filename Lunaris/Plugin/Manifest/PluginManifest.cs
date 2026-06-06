using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	internal class PluginManifest
	{
		public string Id { get; internal set; }
		public bool IsEnabled { get; internal set; }
		public string FilePath { get; internal set; }
		public byte[] IconBlob { get; internal set; }
		public string Description { get; internal set; }
		public string DisplayName { get; internal set; }
		public string Version { get; internal set; }
		public string Author { get; internal set; }
		public PluginInstaller.PluginTags Tags { get; internal set; }
		public int DownloadCount { get; internal set; }
		public DateTime PullDate { get; internal set; }
		public string DownloadLocation { get; internal set; }
		public bool IsFromAPI { get; internal set; }
		public List<string> AllVersions { get; internal set; }
		public List<string> Dependencies { get; internal set; }
	}
}
