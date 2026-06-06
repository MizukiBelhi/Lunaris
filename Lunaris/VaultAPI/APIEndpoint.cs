using Newtonsoft.Json;
using System;
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
		private T _cached;
		private DateTime _lastFetched;
		private readonly TimeSpan _cacheDuration;

		public string EndpointName => _endpoint;
		public bool HasCached => _cached != null;

		public APIEndpoint(HttpClient client, string endpoint, TimeSpan? cacheDuration = null)
		{
			_endpoint = endpoint;
			_client = client;
			_cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(5);
		}

		public async Task<T> FetchAsync(string slug = "", string version = "", bool forceRefresh = false)
		{
			if (!forceRefresh && _cached != null && DateTime.UtcNow - _lastFetched < _cacheDuration)
				return _cached;

			var endpoint = _endpoint;
			if (!string.IsNullOrEmpty(slug))
				endpoint = endpoint.Replace("{slug}", slug);

			if (!string.IsNullOrEmpty(version))
				endpoint = endpoint.Replace("{version}", version);

			var response = await _client.GetAsync(endpoint);
			//response.EnsureSuccessStatusCode();

			var json = await response.Content.ReadAsStringAsync();
			_cached = JsonConvert.DeserializeObject<T>(json) ?? new T();
			_lastFetched = DateTime.UtcNow;

			return _cached;
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

		public void Invalidate() => _cached = null;
	}

}
