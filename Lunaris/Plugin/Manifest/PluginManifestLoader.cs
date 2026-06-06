using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;


namespace Lunaris
{
	internal static class PluginManifestHandler
	{
		private static readonly object FileLock = new();
		private static readonly string ManifestLocation = Path.Combine(PluginLoader.configPath, "pluginManifests.lpm");
		private static readonly string ManifestCacheLocation = Path.Combine(PluginLoader.configPath, "pluginManifestCache.lpm");
		private const int FileVersion = 1;

		public static void StoreManifest(PluginManifest manifest)
		{
			lock (FileLock)
			{
				var all = LoadAll(ManifestLocation).Manifests ?? [];
				all[manifest.Id] = manifest;
				WriteFile(ManifestLocation, [.. all.Values]);
			}
		}

		public static PluginManifest GetManifest(string id)
		{
			lock (FileLock)
			{
				var (manifests, _) = LoadAll(ManifestLocation);
				return manifests?.TryGetValue(id, out var m) == true ? m : null;
			}
		}

		public static void RemoveManifest(string id)
		{
			lock (FileLock)
			{
				var all = LoadAll(ManifestLocation).Manifests ?? [];
				if (all.Remove(id))
					WriteFile(ManifestLocation, [.. all.Values]);
			}
		}

		public static void UpdateCache(List<PluginManifest> manifests)
		{
			WriteFile(ManifestCacheLocation, manifests);
		}

		public static (List<PluginManifest> Manifests, DateTime WrittenAt) GetCache()
		{
			var (manifests, date) = LoadAll(ManifestCacheLocation);
			return (manifests != null ? [.. manifests.Values] : [], date);
		}

		private static void WriteFile(string path, List<PluginManifest> manifests)
		{
			if(!Directory.Exists(Path.GetDirectoryName(path)))
				Directory.CreateDirectory(Path.GetDirectoryName(path));

			using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
			using var gs = new GZipStream(fs, CompressionLevel.Optimal);
			using var bw = new BinaryWriter(gs);

			bw.Write(FileVersion);
			bw.Write(DateTime.Now.ToBinary());
			bw.Write(manifests.Count);

			foreach (var m in manifests)
			{
				bw.Write(m.Id ?? "");
				bw.Write(m.DisplayName ?? "");
				bw.Write(m.Author ?? "");
				bw.Write(m.Version ?? "");
				bw.Write(m.Description ?? "");
				bw.Write(m.FilePath ?? "");
				bw.Write(m.IsEnabled);
				bw.Write((int)m.Tags);
				bw.Write(m.DownloadCount);
				bw.Write(m.IsFromAPI);
				bw.Write(m.Dependencies?.Count ?? 0);
				if(m.Dependencies != null)
				{
					foreach(var dep in m.Dependencies)
					{
						bw.Write(dep);
					}
				}
				bw.Write(m.IconBlob?.Length ?? 0);
				if (m.IconBlob != null) bw.Write(m.IconBlob);
			}
		}

		private static (Dictionary<string, PluginManifest> Manifests, DateTime WrittenAt) LoadAll(string path)
		{
			if (!File.Exists(path)) return (null, DateTime.MinValue);
			var dict = new Dictionary<string, PluginManifest>();

			try
			{
				using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
				using var gs = new GZipStream(fs, CompressionMode.Decompress);
				using var br = new BinaryReader(gs);

				if (br.ReadInt32() != FileVersion) return (null, DateTime.MinValue);

				var writtenAt = DateTime.FromBinary(br.ReadInt64());
				int count = br.ReadInt32();

				for (int i = 0; i < count; i++)
				{
					var m = new PluginManifest
					{
						Id = br.ReadString(),
						DisplayName = br.ReadString(),
						Author = br.ReadString(),
						Version = br.ReadString(),
						Description = br.ReadString(),
						FilePath = br.ReadString(),
						IsEnabled = br.ReadBoolean(),
						Tags = (PluginInstaller.PluginTags)br.ReadInt32(),
						DownloadCount = br.ReadInt32(),
						IsFromAPI = br.ReadBoolean(),
					};
					int depLen = br.ReadInt32();
					if (depLen > 0)
					{
						m.Dependencies = [];
						for (int x = 0; x < depLen; x++)
						{
							m.Dependencies.Add(br.ReadString());
						}
					}
					int blobLen = br.ReadInt32();
					if (blobLen > 0) m.IconBlob = br.ReadBytes(blobLen);
					dict[m.Id] = m;
				}
				return (dict, writtenAt);
			}
			catch { return (null, DateTime.MinValue); }
		}
	}
}
