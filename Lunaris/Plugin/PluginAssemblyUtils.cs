using Mono.Cecil;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Lunaris
{
	internal static class PluginAssemblyUtils
	{

		internal static string GetGuid(string path)
		{
			var readerParams = new ReaderParameters { ReadingMode = ReadingMode.Immediate, InMemory = true };
			using var ms = new MemoryStream(File.ReadAllBytes(path));
			var assembly = AssemblyDefinition.ReadAssembly(ms, readerParams);
			if(assembly == null) return "unk";
			var gid = GetGuid(assembly);
			assembly.Dispose();
			return gid;
		}

		internal static string GetGuid(AssemblyDefinition assembly)
		{
			var lunarisAttr = assembly.CustomAttributes.FirstOrDefault(x =>
				x.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute" &&
				x.ConstructorArguments.Count == 2 &&
				x.ConstructorArguments[0].Value?.ToString() == "LunarisPluginId");

			if (lunarisAttr != null)
				return lunarisAttr.ConstructorArguments[1].Value?.ToString();

			var perm = PluginPermissions.GetUsedPermissions(assembly);
			if (perm.HasFlag(LunarisPermission.BepinPlugin))
			{
				foreach (var type in assembly.MainModule.GetTypes())
				{
					if (type.BaseType?.Name != "BaseUnityPlugin") continue;
					var attr = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == "BepInEx.BepInPlugin");
					if (attr != null && attr.ConstructorArguments.Count >= 3)
						return attr.ConstructorArguments[0].Value?.ToString();
				}
			}

			return assembly.Name.Name;
		}

		internal static byte[] InjectGuid(byte[] dllBytes, string guid)
		{
			using var ms = new MemoryStream(dllBytes);
			var asm = AssemblyDefinition.ReadAssembly(ms);
			var module = asm.MainModule;

			var existing = asm.CustomAttributes.Where(a =>
				a.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute" &&
				a.ConstructorArguments.Count == 2 &&
				a.ConstructorArguments[0].Value?.ToString() == "LunarisPluginId").ToList();

			foreach (var attr in existing)
				asm.CustomAttributes.Remove(attr);

			var ctor = typeof(AssemblyMetadataAttribute).GetConstructor([typeof(string), typeof(string)]);
			var importedCtor = module.ImportReference(ctor);

			var newAttr = new CustomAttribute(importedCtor);
			newAttr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, "LunarisPluginId"));
			newAttr.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, guid));
			asm.CustomAttributes.Add(newAttr);

			using var outMs = new MemoryStream();
			asm.Write(outMs);
			asm.Dispose();
			return outMs.ToArray();
		}

		internal static string CopyToCache(string originalPath, bool renameAssembly = false)
		{
			try
			{
				if (!Directory.Exists(PluginLoader.cacheRoot))
					Directory.CreateDirectory(PluginLoader.cacheRoot);

				var fileName = Path.GetFileName(originalPath);
				var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
				var ticks = Guid.NewGuid();
				var uniqueName = $"{nameWithoutExt}_{ticks}{Path.GetExtension(fileName)}";
				var targetPath = Path.Combine(PluginLoader.cacheRoot, uniqueName);
				var targetLoc = Path.GetFullPath(targetPath);

				AssemblyHooks.AddLoc(targetLoc, originalPath);
				if (renameAssembly)
					CopyRenamedAssembly(originalPath, targetPath, ticks.ToString("N"));
				else
					File.Copy(originalPath, targetPath, true);

				foreach (var symPath in new[] { originalPath + ".mdb", Path.ChangeExtension(originalPath, ".pdb") })
				{
					if (!File.Exists(symPath)) continue;
					var targetSymName = symPath.EndsWith(".mdb") ? uniqueName + ".mdb" : $"{nameWithoutExt}_{ticks}.pdb";
					File.Copy(symPath, Path.Combine(PluginLoader.cacheRoot, targetSymName), true);
				}

				return targetLoc;
			}
			catch (Exception ex)
			{
				Debug.LogError($"Cache: Failed to copy {originalPath}: {ex.Message}");
				return null;
			}
		}

		internal static Guid GetMvid(string path)
		{
			var readerParams = new ReaderParameters { ReadingMode = ReadingMode.Immediate, InMemory = true };
			using var ms = new MemoryStream(File.ReadAllBytes(path));
			using var assembly = AssemblyDefinition.ReadAssembly(ms, readerParams);
			return assembly.MainModule.Mvid;
		}

		private static void CopyRenamedAssembly(string originalPath, string targetPath, string suffix)
		{
			var readerParams = new ReaderParameters
			{
				ReadingMode = ReadingMode.Immediate,
				InMemory = true,
				AssemblyResolver = HarmonyFixes.resolver
			};

			using var ms = new MemoryStream(File.ReadAllBytes(originalPath));
			using var assembly = AssemblyDefinition.ReadAssembly(ms, readerParams);
			assembly.Name.Name = $"{assembly.Name.Name}_{suffix}";
			assembly.MainModule.Name = Path.GetFileName(targetPath);
			assembly.Write(targetPath);
		}

		internal static bool IsManagedAssembly(string path)
		{
			try
			{
				using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
				using var br = new BinaryReader(fs);

				if (br.ReadUInt16() != 0x5A4D) return false;
				fs.Seek(0x3C, SeekOrigin.Begin);
				var peOffset = br.ReadUInt32();
				fs.Seek(peOffset, SeekOrigin.Begin);
				if (br.ReadUInt32() != 0x00004550) return false;
				fs.Seek(peOffset + 24, SeekOrigin.Begin);
				var magic = br.ReadUInt16();
				int clrOffset = magic switch { 0x010B => 208, 0x020B => 224, _ => -1 };
				if (clrOffset == -1) return false;
				fs.Seek(peOffset + 24 + clrOffset, SeekOrigin.Begin);
				return br.ReadUInt32() != 0;
			}
			catch { return false; }
		}

		internal static bool IsManagedAssembly(byte[] bytes)
		{
			try
			{
				using var ms = new MemoryStream(bytes);
				using var br = new BinaryReader(ms);

				if (br.ReadUInt16() != 0x5A4D) return false;
				ms.Seek(0x3C, SeekOrigin.Begin);
				var peOffset = br.ReadUInt32();
				ms.Seek(peOffset, SeekOrigin.Begin);
				if (br.ReadUInt32() != 0x00004550) return false;
				ms.Seek(peOffset + 24, SeekOrigin.Begin);
				var magic = br.ReadUInt16();
				int clrOffset = magic switch { 0x010B => 208, 0x020B => 224, _ => -1 };
				if (clrOffset == -1) return false;
				ms.Seek(peOffset + 24 + clrOffset, SeekOrigin.Begin);
				return br.ReadUInt32() != 0;
			}
			catch { return false; }
		}

	}
}
