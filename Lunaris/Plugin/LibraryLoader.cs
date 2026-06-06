using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Lunaris
{
	internal static class LibraryLoader
	{
		private static readonly List<LibraryDescriptor> _libraries = [];
		private static readonly List<LibraryDescriptor> _loadQueue = [];

		internal static IReadOnlyList<LibraryDescriptor> Libraries => _libraries;

		internal static void Enqueue(LibraryDescriptor lib)
		{
			_loadQueue.Add(lib);
		}

		internal static void LoadQueued()
		{
			foreach (var lib in _loadQueue)
			{
				try
				{
					string assemblyName;
					using (var ms = new MemoryStream(File.ReadAllBytes(lib.OriginalFilePath)))
					{
						using var ass = AssemblyDefinition.ReadAssembly(ms);
						assemblyName = ass.Name.Name;
					}

					if (_libraries.Any(t => t.Name == assemblyName))
					{
						Bridge.Logger.LogWarning($"Lib '{assemblyName}' already loaded, skipping.");
						continue;
					}

					lib.FilePath = PluginAssemblyUtils.CopyToCache(lib.OriginalFilePath);
					lib.assembly = Assembly.LoadFrom(lib.FilePath);
					lib.Name = assemblyName;

					_libraries.Add(lib);
					Bridge.Logger.Log($"Loaded lib: {lib.Name}");
				}
				catch (Exception ex)
				{
					Bridge.Logger.LogError($"Failed to load lib '{lib.OriginalFilePath}': {ex.Message}");
				}
			}

			_loadQueue.Clear();
		}

		internal static bool IsAssemblyLoaded(string assemblyName)
		{
			if (_libraries.Any(t => t.Name == assemblyName))
				return true;

			return AppDomain.CurrentDomain.GetAssemblies()
				.Any(a => a.GetName().Name == assemblyName);
		}

		internal static List<string> CollectDependencies(AssemblyDefinition definition)
		{
			var deps = new List<string>();

			foreach (var reference in definition.MainModule.AssemblyReferences)
			{
				var match = _libraries.FirstOrDefault(l => l.Name == reference.Name);
				if (match != null)
					deps.Add(Path.GetFileName(match.OriginalFilePath));
			}

			return deps.Count > 0 ? deps : null;
		}

		internal static void RegisterDependency(string pluginId, string libFilename)
		{
			var lib = _libraries.FirstOrDefault(l => Path.GetFileName(l.OriginalFilePath) == libFilename);
			if (lib != null && !lib.ReferencedBy.Contains(pluginId))
				lib.ReferencedBy.Add(pluginId);
		}

		internal static List<string> UnregisterPlugin(string pluginId)
		{
			var orphans = new List<string>();

			foreach (var lib in _libraries)
			{
				lib.ReferencedBy.Remove(pluginId);
				if (lib.ReferencedBy.Count == 0)
					orphans.Add(lib.OriginalFilePath);
			}

			return orphans;
		}
	}
}