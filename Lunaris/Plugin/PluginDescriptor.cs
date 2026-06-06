using Mono.Cecil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Lunaris
{
	internal enum ModSource
	{
		Lunaris,
		Legacy,
	}
	internal sealed class PluginDescriptor
	{
		public string Id { get; set; }
		public string SetPluginName { get; set; }
		public string Version { get; set; }
		public string Author { get; set; }
		public string Description { get; set; } = "No description available.";

		public bool IsLoaded { get; set; }

		public LunarisPermission DeclaredPermissions { get; set; }
		public LunarisPermission EffectivePermissions { get; set; }

		public ModSource Source { get; set; }
		public PluginManifest Manifest { get; set; }

		public string filePath { get; set; }
		public string OriginalFilePath { get; set; }
		public Assembly Assembly { get; set; }
		public AssemblyDefinition Definition { get; set; }
		public GameObject GameObject { get; set; }

		public override string ToString()
		{
			var status = Assembly != null ? "Loaded" : (Definition != null ? "Scanned" : "Empty");
			return $"PluginDescriptor: {SetPluginName} ({Id}) | Version: {Version} | Source: {Source} | Status: {status}\n" +
				   $"Path: {filePath}\n" +
				   $"Permissions: {EffectivePermissions} (Declared: {DeclaredPermissions})";
		}
	}
}
