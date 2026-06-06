using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Lunaris.VaultAPI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Lunaris
{
	internal class PluginRepository
	{
		private const string BaseUrl = "https://erenshorvault.app/api/v1/luna/";

		[DllImport("winhttp.dll", CallingConvention = CallingConvention.StdCall)]
		[return: MarshalAs(UnmanagedType.LPStr)]
		private static extern string GetAPIKey();

		[DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		public static extern IntPtr GetModuleHandle(string lpModuleName);

		private readonly ConcurrentDictionary<string, object> _endpoints = [];

		private readonly ConcurrentDictionary<string, float> _downloadProgress = [];

		private readonly HttpClient _http = new(new HttpClientHandler());

		private static string GetFriendlyPlatformName()
		{
			if (IsRunningUnderWine())
				return $"Proton/Wine; {Environment.OSVersion}";

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "Windows";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "Linux";
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "macOS";

			return "Unknown";
		}

		private static bool IsRunningUnderWine()
		{
			return GetProcAddress(GetModuleHandle("ntdll.dll"), "wine_get_version") != IntPtr.Zero;
		}


		public PluginRepository()
		{
			_http.DefaultRequestHeaders.Add("X-API-Key", $"{GetAPIKey()}");
			_http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Lunaris", Bridge.version.ToString()));
			_http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue($"({GetFriendlyPlatformName()}; Lunaris/v{Bridge.version})"));
			_http.BaseAddress = new Uri(BaseUrl);


			_endpoints["mods"] = new APIEndpoint<ApiResponseMods>(_http, "mods");
			_endpoints["modVersions"] = new APIEndpoint<ApiResponseModVersions>(_http, "mods/{slug}/versions");
			_endpoints["modsVersions"] = new APIEndpoint<ApiResponseModsUpdates>(_http, "mods/updates");
			_endpoints["getIcon"] = new APIEndpoint<ApiResponseBase>(_http, "mods/{slug}/logo");
			_endpoints["download"] = new APIEndpoint<ApiResponseBase>(_http, "mods/{slug}/download/{version}");


			//Explore("food-buff-duration", "1.2.3");
		}

		internal APIEndpoint<T> Endpoint<T>(string key) where T : class, new()
		{
			if (_endpoints.TryGetValue(key, out var ep))
				return (APIEndpoint<T>)ep;
			return null;
		}

		public void Explore(string testSlug, string testVersion)
		{
			/*

			GET  /api/v1/luna/mods
			GET  /api/v1/luna/mods/:slug
			GET  /api/v1/luna/mods/:slug/versions
			GET  /api/v1/luna/mods/:slug/versions/:version
			GET  /api/v1/luna/mods/:slug/download/latest
			GET  /api/v1/luna/mods/:slug/download/:version
			GET  /api/v1/luna/mods/:slug/logo
			GET  /api/v1/luna/mods/:slug/updates?current_version=X
			POST /api/v1/luna/mods/updates
			GET  /api/v1/luna/tags

			*/


			var endpoints = new[]
			{
				"mods",
				$"mods/{testSlug}",
				$"mods/{testSlug}/versions",
				$"mods/{testSlug}/versions/{testVersion}",
				$"mods/{testSlug}/download/latest",
				$"mods/{testSlug}/download/{testVersion}",
				$"mods/{testSlug}/logo",
				$"mods/{testSlug}/updates?current_version={testVersion}",
				$"mods/updates",
				"tags",
			};

			foreach (var path in endpoints)
			{
				Bridge.Logger.Log($"\n{"=".PadRight(60, '=')}\nGET {path}\n{"=".PadRight(60, '=')}");
				try
				{
					var res = _http.GetAsync(path).Result;
					var ct = res.Content.Headers.ContentType?.MediaType ?? "unknown";
					Bridge.Logger.Log($"Status : {(int)res.StatusCode} {res.StatusCode}");
					Bridge.Logger.Log($"Content: {ct}");

					if (ct.Contains("json"))
					{
						var raw = res.Content.ReadAsStringAsync().Result;
						var pretty = JToken.Parse(raw).ToString(Formatting.Indented);
						Bridge.Logger.Log(pretty);
					}
					else
					{
						var bytes = res.Content.ReadAsByteArrayAsync().Result;
						Bridge.Logger.Log($"Binary : {bytes.Length} bytes");
					}
				}
				catch (Exception ex) { Bridge.Logger.Log($"ERROR: {ex.Message}"); }
			}
		}


		internal float GetDownloadProgress(string modId)
		{
			if(_downloadProgress.ContainsKey(modId))
				return _downloadProgress[modId];
			return -1;
		}

		internal void ClearDownloadProgress(string modId)
		{
			if (_downloadProgress.ContainsKey(modId))
				_downloadProgress.TryRemove(modId, out var _);
		}

		

		internal async Task<List<PluginManifest>> FetchApprovedAsync(bool forceRefresh)
		{
			try
			{
				var ep = Endpoint<ApiResponseMods>("mods");
				var response = await ep.FetchAsync(forceRefresh: forceRefresh);

				if (response?.Mods == null) return [];

				var tasks = response.Mods.Select(MapToManifestAsync);


				return [.. (await Task.WhenAll(tasks))];
			}
			catch (Exception e)
			{
				Bridge.Logger.LogError($"[PluginRepository] Failed to fetch mods: {e}");
				return [];
			}
		}

		//Need to sanitize these for imgui
		private string Sanitize(string str)
		{
			if (string.IsNullOrEmpty(str)) return str;
			return str.Replace("%", "%%");
		}

		private async Task<PluginManifest> MapToManifestAsync(ApiResponseModSummary mod)
		{
			var vers = await FetchLatestVersions(mod.Slug);
			var av = new List<string>();
			foreach(var version in vers)
			{
				av.Add(version.Version);
			}


			var plm = new PluginManifest
			{
				Id = Sanitize(mod.Slug),
				DisplayName = Sanitize(mod.Name),
				Description = Sanitize(mod.ShortDescription),
				Author = Sanitize(mod.Author?.Username ?? "Unknown"),
				DownloadCount = mod.DownloadCount,
				Tags = MapTags(mod.Tags),
				PullDate = DateTime.UtcNow,
				IsEnabled = false,
				Version = vers.FirstOrDefault()?.Version,
				FilePath = "",
				DownloadLocation = "",
				AllVersions = av
			};

			if (!string.IsNullOrEmpty(mod.LogoUrl))
				plm.IconBlob = await FetchIconAsync(mod.Slug);

			return plm;
		}

		private static PluginInstaller.PluginTags MapTags(List<string> tags)
		{
			if (tags == null || tags.Count == 0) return PluginInstaller.PluginTags.All;

			var result = (PluginInstaller.PluginTags)0;

			foreach (var tag in tags)
			{
				var mapped = tag.ToLower() switch
				{
					"utility" => PluginInstaller.PluginTags.Utility,
					"gameplay" => PluginInstaller.PluginTags.Gameplay,
					"quality-of-life" or "qol" => PluginInstaller.PluginTags.QoL,
					"audio" => PluginInstaller.PluginTags.Audio,
					"content" => PluginInstaller.PluginTags.Content,
					"graphics" => PluginInstaller.PluginTags.Graphics,
					"tools" => PluginInstaller.PluginTags.Tools,
					"ui" => PluginInstaller.PluginTags.UI,
					_ => (PluginInstaller.PluginTags?)null
				};

				if (mapped.HasValue) result |= mapped.Value;
			}

			return result == 0 ? PluginInstaller.PluginTags.All : result;
		}


		private async Task<byte[]> FetchIconAsync(string modId)
		{
			if (string.IsNullOrEmpty(modId)) return null;

			try
			{
				var ep = Endpoint<ApiResponseBase>("getIcon");
				var bytes = await ep.FetchBytesAsync(slug: modId);
				if(bytes == null || bytes.Length == 0) return null;
				return bytes;
			}
			catch (Exception e)
			{
				Bridge.Logger.LogWarning($"[PluginRepository] Failed to fetch icon: {e}");
				return null;
			}
		}

		internal async Task<Dictionary<string, string>> FetchAllUpdates(List<PluginManifest> plugins)
		{
			try
			{
				var ep = Endpoint<ApiResponseModsUpdates>("modsVersions");
				var response = await ep.PostAsync<ApiRequestModsUpdates, ApiResponseModsUpdates>(new ApiRequestModsUpdates
				{
					Mods = [.. plugins.Select(pl => new ApiRequestModsUpdatesEntry { Slug = pl.Id, CurrentVersion = pl.Version })]
				});

				var updates = new Dictionary<string, string>();
				foreach (var mod in response?.Results)
				{
					if (mod.UpdateAvailable)
						updates.Add(mod.Slug, mod.LatestVersion);
				}

				return updates;
			}
			catch (Exception e)
			{
				Bridge.Logger.LogError($"[PluginRepository] Failed to fetch updates: {e}");
				return new();
			}
		}

		internal async Task<List<ApiModVersion>> FetchLatestVersions(string modId)
		{
			try
			{
				var ep = Endpoint<ApiResponseModVersions>("modVersions");
				var response = await ep.FetchAsync(slug: modId);
				return response?.Versions;
			}
			catch (Exception e)
			{
				Bridge.Logger.LogError($"[PluginRepository] Failed to fetch versions for {modId}: {e}\n {e.StackTrace}");
				return [new() { Version = "-1" }];
			}
		}

/*		internal async Task<string> FetchLatestVersion(string modId)
		{
			return null;
			try
			{
				var json = await _http.GetStringAsync($"{BaseUrl}/api/mods/{modId}/versions");
				var response = JsonConvert.DeserializeObject<VersionsResponse>(json);
				return response?.Versions?.FirstOrDefault()?.Version;
			}
			catch (Exception e)
			{
				Bridge.Logger.LogError($"[PluginRepository] Failed to fetch versions for {modId}: {e}");
				return "-1";
			}
		}*/

		internal async Task<byte[]> DownloadVersion(string modId, string version, Action<byte[]> onComplete)
		{
			try
			{
				//clear progress first
				ClearDownloadProgress(modId);
				var ep = Endpoint<ApiResponseBase>("download");
				var response = await ep.FetchContent(slug: modId, version: version);

				var total = response.Headers.ContentLength ?? -1L;
				var buffer = new byte[8192];
				var ms = new MemoryStream();

				using var stream = await response.ReadAsStreamAsync();
				long downloaded = 0;
				int read;

				while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
				{
					await ms.WriteAsync(buffer, 0, read);
					downloaded += read;
					if (total > 0)
					{
						_downloadProgress[modId] = (float)downloaded / total;
					}
				}

				var result = ms.ToArray();
				onComplete?.Invoke(result);
				return result;
			}
			catch (Exception e)
			{
				Bridge.Logger.LogError($"[PluginRepository] Failed to download {modId} v{version}: {e}");
				return null;
			}
		}



		internal class ApiResponseBase
		{
			[JsonProperty("error")] public string Error { get; set; }
		}

		internal class ApiResponseMods : ApiResponseBase
		{
			[JsonProperty("mods")] public List<ApiResponseModSummary> Mods { get; set; }
			[JsonProperty("pagination")] public ApiPagination Pagination { get; set; }
		}

		internal class ApiResponseMod : ApiResponseBase
		{
			[JsonProperty("slug")] public string Slug { get; set; }
			[JsonProperty("name")] public string Name { get; set; }
			[JsonProperty("shortDescription")] public string ShortDescription { get; set; }
			[JsonProperty("logoUrl")] public string LogoUrl { get; set; }
			[JsonProperty("sourceUrl")] public string SourceUrl { get; set; }
			[JsonProperty("author")] public ApiAuthorDetail Author { get; set; }
			[JsonProperty("tags")] public List<string> Tags { get; set; }
			[JsonProperty("viewCount")] public int ViewCount { get; set; }
			[JsonProperty("downloadCount")] public int DownloadCount { get; set; }
			[JsonProperty("createdAt")] public DateTime CreatedAt { get; set; }
			[JsonProperty("updatedAt")] public DateTime UpdatedAt { get; set; }
		}

		internal class ApiResponseModVersions : ApiResponseBase
		{
			[JsonProperty("modSlug")] public string ModSlug { get; set; }
			[JsonProperty("modName")] public string ModName { get; set; }
			[JsonProperty("versions")] public List<ApiModVersion> Versions { get; set; }
		}

		internal class ApiResponseModVersion : ApiResponseBase
		{
			[JsonProperty("modSlug")] public string ModSlug { get; set; }
			[JsonProperty("modName")] public string ModName { get; set; }
			[JsonProperty("version")] public string Version { get; set; }
			[JsonProperty("changelog")] public string Changelog { get; set; }
			[JsonProperty("downloadCount")] public int DownloadCount { get; set; }
			[JsonProperty("fileSize")] public string FileSize { get; set; }
			[JsonProperty("fileName")] public string FileName { get; set; }
			[JsonProperty("createdAt")] public DateTime CreatedAt { get; set; }
		}

		internal class ApiResponseModUpdateCheck : ApiResponseBase
		{
			[JsonProperty("modSlug")] public string ModSlug { get; set; }
			[JsonProperty("currentVersion")] public string CurrentVersion { get; set; }
			[JsonProperty("updateAvailable")] public bool UpdateAvailable { get; set; }
			[JsonProperty("updates")] public List<ApiModVersion> Updates { get; set; }
		}

		internal class ApiResponseModsUpdates : ApiResponseBase
		{
			[JsonProperty("checked")] public int Checked { get; set; }
			[JsonProperty("results")] public List<ApiResponseModsUpdatesResult> Results { get; set; }
		}

		internal class ApiResponseModsUpdatesResult
		{
			[JsonProperty("slug")] public string Slug { get; set; }
			[JsonProperty("currentVersion")] public string CurrentVersion { get; set; }
			[JsonProperty("updateAvailable")] public bool UpdateAvailable { get; set; }
			[JsonProperty("latestVersion")] public string LatestVersion { get; set; }
		}

		internal class ApiResponseTags : ApiResponseBase
		{
			[JsonProperty("tags")] public List<string> Tags { get; set; }
		}

		internal class ApiRequestModsUpdates
		{
			[JsonProperty("mods")] public List<ApiRequestModsUpdatesEntry> Mods { get; set; }
		}

		internal class ApiRequestModsUpdatesEntry
		{
			[JsonProperty("slug")] public string Slug { get; set; }
			[JsonProperty("currentVersion")] public string CurrentVersion { get; set; }
		}

		internal class ApiResponseModSummary
		{
			[JsonProperty("slug")] public string Slug { get; set; }
			[JsonProperty("name")] public string Name { get; set; }
			[JsonProperty("shortDescription")] public string ShortDescription { get; set; }
			[JsonProperty("logoUrl")] public string LogoUrl { get; set; }
			[JsonProperty("author")] public ApiAuthor Author { get; set; }
			[JsonProperty("tags")] public List<string> Tags { get; set; }
			[JsonProperty("viewCount")] public int ViewCount { get; set; }
			[JsonProperty("downloadCount")] public int DownloadCount { get; set; }
			[JsonProperty("createdAt")] public DateTime CreatedAt { get; set; }
			[JsonProperty("updatedAt")] public DateTime UpdatedAt { get; set; }
		}

		internal class ApiAuthor
		{
			[JsonProperty("username")] public string Username { get; set; }
			[JsonProperty("avatarUrl")] public string AvatarUrl { get; set; }
		}

		internal class ApiAuthorDetail
		{
			[JsonProperty("id")] public string Id { get; set; }
			[JsonProperty("username")] public string Username { get; set; }
			[JsonProperty("avatarUrl")] public string AvatarUrl { get; set; }
		}

		internal class ApiPagination
		{
			[JsonProperty("page")] public int Page { get; set; }
			[JsonProperty("limit")] public int Limit { get; set; }
			[JsonProperty("total")] public int Total { get; set; }
			[JsonProperty("pages")] public int Pages { get; set; }
		}

		internal class ApiModVersion
		{
			[JsonProperty("version")] public string Version { get; set; }
			[JsonProperty("changelog")] public string Changelog { get; set; }
			[JsonProperty("downloadCount")] public int DownloadCount { get; set; }
			[JsonProperty("fileSize")] public string FileSize { get; set; }
			[JsonProperty("fileName")] public string FileName { get; set; }
			[JsonProperty("createdAt")] public DateTime CreatedAt { get; set; }
		}
	}
}