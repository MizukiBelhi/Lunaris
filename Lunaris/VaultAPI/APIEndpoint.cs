using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Lunaris.VaultAPI
{
	internal class APIEndpoint<T> where T : class, new()
	{
		private readonly string _endpoint;
		private readonly HttpClient _client;
		private readonly ConcurrentDictionary<string, (T cached, DateTime lastFetched)> _cache = new();
		private readonly ConcurrentDictionary<string, long> _requestVersions = new();
		private readonly TimeSpan _cacheDuration;

		public string EndpointName => _endpoint;
		public bool HasCached => _cache.Count > 0;

		public APIEndpoint(HttpClient client, string endpoint, TimeSpan? cacheDuration = null)
		{
			_endpoint = endpoint;
			_client = client;
			_cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(5);
		}

		public async Task<T> FetchAsync(string slug = "", string version = "", bool forceRefresh = false, IReadOnlyDictionary<string, string> query = null)
		{
			var endpoint = _endpoint;
			if (!string.IsNullOrEmpty(slug))
				endpoint = endpoint.Replace("{slug}", slug);

			if (!string.IsNullOrEmpty(version))
				endpoint = endpoint.Replace("{version}", version);

			if (query != null && query.Count > 0)
			{
				var queryString = string.Join("&", query
					.OrderBy(entry => entry.Key, StringComparer.Ordinal)
					.Select(entry => $"{Uri.EscapeDataString(entry.Key)}={Uri.EscapeDataString(entry.Value ?? string.Empty)}"));
				endpoint += endpoint.Contains("?") ? $"&{queryString}" : $"?{queryString}";
			}

			if (!forceRefresh && _cache.TryGetValue(endpoint, out var entry) && DateTime.UtcNow - entry.lastFetched < _cacheDuration)
				return entry.cached;

			var requestVersion = _requestVersions.AddOrUpdate(endpoint, 1, (_, current) => current + 1);
			var response = await _client.GetAsync(endpoint);
			//response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync();
			var obj = JsonConvert.DeserializeObject<T>(json) ?? new T();
			if (_requestVersions.TryGetValue(endpoint, out var latestRequestVersion) && latestRequestVersion == requestVersion)
				_cache[endpoint] = (obj, DateTime.UtcNow);

			return obj;
		}

		public async Task<byte[]> FetchBytesAsync(string slug = "", string version = "")
		{
			var endpoint = _endpoint;
			if (!string.IsNullOrEmpty(slug))
				endpoint = endpoint.Replace("{slug}", slug);
			if (!string.IsNullOrEmpty(version))
				endpoint = endpoint.Replace("{version}", version);

			var response = await _client.GetAsync(endpoint);
			response.EnsureSuccessStatusCode();
			return await response.Content.ReadAsByteArrayAsync();
		}

		public async Task<HttpContent> FetchContent(string slug = "", string version = "")
		{
			var endpoint = _endpoint;
			if (!string.IsNullOrEmpty(slug))
				endpoint = endpoint.Replace("{slug}", slug);
			if (!string.IsNullOrEmpty(version))
				endpoint = endpoint.Replace("{version}", version);

			var response = await _client.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();
			return response.Content;
		}

		public async Task<TResponse> PostAsync<TRequest, TResponse>(TRequest body)
		{
			var content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
			var response = await _client.PostAsync(_endpoint, content);
			response.EnsureSuccessStatusCode();
			return JsonConvert.DeserializeObject<TResponse>(await response.Content.ReadAsStringAsync());
		}

		public void Invalidate() => _cache.Clear();
	}

}
