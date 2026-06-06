using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	internal class LibraryDescriptor
	{
		public string OriginalFilePath { get; set; } = string.Empty;
		public string FilePath { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;

		public Assembly assembly { get; set; } = null!;

		public List<string> ReferencedBy { get; } = [];
	}
}
