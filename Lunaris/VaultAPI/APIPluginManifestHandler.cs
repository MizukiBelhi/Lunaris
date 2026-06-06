using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris
{
	internal class ZipManifest
	{
		[JsonProperty("main_file")] public string MainDll { get; set; }
		[JsonProperty("asset_files")] public List<string> Files { get; set; }
	}

	internal class APIPluginManifestHandler
	{
		static string SanitizeFileName(string fileName)
		{
			var name = Path.GetFileName(fileName);
			if (string.IsNullOrWhiteSpace(name)) return null;
			var fullPath = Path.GetFullPath(Path.Combine(PluginLoader.pluginPath, name));
			if (!fullPath.StartsWith(Path.GetFullPath(PluginLoader.pluginPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
				return null;
			return fullPath;
		}

		public static (string mainDll, byte[] pluginBytes, Dictionary<string, byte[]> dependencies) LoadZip(byte[] pluginZipBytes)
		{
			var mainDll = "";
			byte[] pluginBytes = null;
			Dictionary<string, byte[]> dependencies = null;
			using (var zip = new ZipArchive(new MemoryStream(pluginZipBytes)))
			{
				var manifestEntry = zip.Entries.FirstOrDefault(e => e.Name == "manifest.json");
				if (manifestEntry == null) { Bridge.Logger.LogError("No manifest.json in zip"); return (null, null, null); }

				using var ms = new MemoryStream();
				manifestEntry.Open().CopyTo(ms);
				var zipManifest = JsonConvert.DeserializeObject<ZipManifest>(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
				mainDll = SanitizeFileName(zipManifest?.MainDll);

				if (mainDll == null) { Bridge.Logger.LogError("No mainDll in manifest"); return (null, null, null); }

				var dllEntry = zip.Entries.FirstOrDefault(e => e.Name == zipManifest?.MainDll);
				if (dllEntry == null) { Bridge.Logger.LogError($"{zipManifest?.MainDll} not found in zip"); return (null, null, null); }

				if (zipManifest.Files.Count > 1)
				{
					dependencies = [];
					foreach (var fileName in zipManifest.Files)
					{
						if (fileName == mainDll) continue;
						var safePath = SanitizeFileName(fileName);
						if (safePath == null) { Bridge.Logger.LogWarning($"Skipping unsafe path in manifest: {fileName}"); continue; }

						var entry = zip.Entries.FirstOrDefault(e => e.Name == fileName);
						if (entry == null) continue;

						using var dllStr = new MemoryStream();
						entry.Open().CopyTo(dllStr);
						dependencies.Add(fileName, dllStr.ToArray());
					}
				}

				using var dllMs = new MemoryStream();
				dllEntry.Open().CopyTo(dllMs);
				pluginBytes = dllMs.ToArray();
			}

			return (mainDll, pluginBytes, dependencies);
		}
	}
}
