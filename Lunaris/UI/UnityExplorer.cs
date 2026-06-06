using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using ImGuiNET;

namespace Lunaris
{
	internal class UnityExplorer
	{
		private static readonly HashSet<string> _blacklisted = ["bodyPosition","bodyRotation","rootPosition","rootRotation","minVolume","maxVolume","areaProgress"];
		private static readonly string[] primitiveOptions = ["Empty","Cube","Sphere","Cylinder","Capsule","Plane","Quad"];
		private static readonly Dictionary<string, string> matProps = new() { { "color", "_Color" }, { "mainTexture", "_MainTex" }, { "mainTextureOffset", "_MainTex" }, { "mainTextureScale", "_MainTex" } };

		private class InspectTarget
		{
			public readonly GameObject GO;
			public readonly Component Comp;
			public readonly UnityEngine.Object Obj;
			public readonly Type InspectingObjType;
			public readonly Component ObjParent;
			public bool IsComponent => Comp != null;
			public bool IsObj => Obj != null;
			public InspectTarget(GameObject go)
			{
				GO = go;
			}
			public InspectTarget(Component comp)
			{
				GO = comp.gameObject;
				Comp = comp;
			}
			public InspectTarget(UnityEngine.Object obj, Type type, Component parent)
			{
				Obj = obj;
				InspectingObjType = type;
				ObjParent = parent;
				GO = parent.gameObject;
			}
			public int ID => IsComponent ? Comp.GetInstanceID() : IsObj ? Obj.GetInstanceID() : GO.GetInstanceID();
			public string TabLabel => IsComponent ? $"{Comp.GetType().Name} ({GO.name})##{ID}" : IsObj ? $"{Obj.name}##{ID}" : $"{GO.name}##{ID}";
		}

		private static GameObject[] rootCache;
		private static GameObject[] ddnCache;
		private static GameObject[] hdsCache;
		private static float nextCacheTime;
		private static readonly HashSet<int> openNodes = [];

		private static bool isInspectorOpen = false;
		private static InspectTarget activeTab;
		private static InspectTarget pendingTab;
		private static readonly List<InspectTarget> inspectTargets = [];
		private static readonly Dictionary<int, Dictionary<Type, CachedMember[]>> _typeCache = [];
		private static readonly Dictionary<int, bool> liveUpdateMap = [];
		private static readonly Dictionary<int, InspectorState> _inspectorStates = [];

		private static int selectedSceneIndex = 0;
		private static string sceneNameFilter = "";
		private static readonly bool sceneAutoUpdate = false;
		private static float sceneAutoUpdateTimer;
		private static string sceneLoaderFilter = "";
		private static int sceneLoadMode = 0;

		private static int objSearchSubTab = 0;
		private static string uoSearchName = "";
		private static string uoSearchType = "";
		private static List<UnityEngine.Object> uoResults = [];

		private static string singletonFilter = "";
		private static readonly List<(Type type, object instance)> singletonResults = [];
		private static bool singletonScanned = false;

		private static string staticFilter = "";
		private static List<Type> staticResults = [];
		private static bool staticScanned = false;

		private static bool showAddComponent;
		private static GameObject addComponentTarget;
		private static string addComponentSearch = "";
		private static Type[] allComponentTypes;
		private static List<Type> filteredComponentTypes = [];

		private static bool showAddGameObject;
		private static Transform addGOParent;
		private static string newGOName = "New GameObject";
		private static int newGOPrimitiveIdx = 0;

		private static bool showConfirmDestroy;
		private static GameObject destroyTarget;

		private static bool showTexturePreview;
		private static Texture2D previewTexture;
		private static string previewTextureName = "";

		public class CachedMember
		{
			public string Name;
			public Type Type;
			public Type DeclaringType;
			public FieldInfo Field;
			public PropertyInfo Prop;
			public MethodInfo Method;
			public object Value;
			public string ShaderPropName;
			public UnityEngine.Rendering.ShaderPropertyType ShaderPropType;
			public bool IsField => Field != null;
			public bool IsProp => Prop != null;
			public bool IsMethod => Method != null;
			public bool CanWrite => IsField || (Prop != null && Prop.CanWrite);
			public bool IsStatic => Field?.IsStatic ?? (Prop?.GetGetMethod(true)?.IsStatic ?? (Method?.IsStatic ?? false));
			public bool IsPrivate => Field?.IsPrivate ?? (Prop?.GetGetMethod(true)?.IsPrivate ?? (Method?.IsPrivate ?? false));
			public ParameterInfo[] Parameters => Method?.GetParameters() ?? [];
			public string DisplayName => DeclaringType != null ? $"{DeclaringType.Name}.{Name}" : Name;
			private object _savedTar;
			public void Pull(object target)
			{
				_savedTar = target;
				if (target is Material mat)
				{
					if (!string.IsNullOrEmpty(ShaderPropName))
					{
						Value = ShaderPropType switch
						{
							UnityEngine.Rendering.ShaderPropertyType.Color => mat.GetColor(ShaderPropName),
							UnityEngine.Rendering.ShaderPropertyType.Vector => mat.GetVector(ShaderPropName),
							UnityEngine.Rendering.ShaderPropertyType.Float or UnityEngine.Rendering.ShaderPropertyType.Range => mat.GetFloat(ShaderPropName),
							UnityEngine.Rendering.ShaderPropertyType.Texture => mat.GetTexture(ShaderPropName),
							_ => null
						};
						return;
					}

					if (!IsField && matProps.TryGetValue(Prop.Name, out string shaderProp) && !IsPropertyInShader(mat.shader, shaderProp))
					{
						Value = null;
						return;
					}
				}

				Value = IsField ? Field.GetValue(target) : Prop.GetValue(target);
			}
			public void Push(object target)
			{
				if (_savedTar is Material mat && !string.IsNullOrEmpty(ShaderPropName))
				{
					switch (ShaderPropType)
					{
						case UnityEngine.Rendering.ShaderPropertyType.Color: mat.SetColor(ShaderPropName, (Color)Value); break;
						case UnityEngine.Rendering.ShaderPropertyType.Vector: mat.SetVector(ShaderPropName, (Vector4)Value); break;
						case UnityEngine.Rendering.ShaderPropertyType.Float:
						case UnityEngine.Rendering.ShaderPropertyType.Range: mat.SetFloat(ShaderPropName, (float)Value); break;
						case UnityEngine.Rendering.ShaderPropertyType.Texture: mat.SetTexture(ShaderPropName, (Texture)Value); break;
					}
					return;
				}
				if (IsField) Field.SetValue(target, Value);
				else if (CanWrite) Prop.SetValue(target, Value);
			}

			private bool IsPropertyInShader(Shader s, string propertyName)
			{
				if (s == null)
					return false;
				int count = s.GetPropertyCount();
				for (int i = 0; i < count; i++)
				{
					if (s.GetPropertyName(i) == propertyName)
						return true;
				}
				return false;
			}
		}

		private class InspectorState
		{
			public string MemberFilter = "";
			public bool ShowFields = true;
			public bool ShowProps = true;
			public bool ShowMethods = false;
			public bool ShowStatic = true;
			public bool ShowPrivate = true;
			public HashSet<string> Expanded = [];
			public Dictionary<string, string[]> MethodParams = [];
			public Dictionary<string, string> MethodResults = [];
		}

		private static InspectorState GetState(int id)
		{
			if (!_inspectorStates.TryGetValue(id, out var s))
				_inspectorStates[id] = s = new InspectorState();
			return s;
		}

		public static void Draw(ref bool isObjOpn)
		{
			RefreshCacheIfNeeded();
			DrawObjectExplorer(ref isObjOpn);
			if (isInspectorOpen) DrawInspectorWindow();
			DrawAddComponentPopup();
			DrawAddGameObjectPopup();
			DrawConfirmDestroyPopup();
			DrawTexturePreviewPopup();
		}

		private static void RefreshCacheIfNeeded()
		{
			bool force = sceneAutoUpdate && Time.realtimeSinceStartup > sceneAutoUpdateTimer;
			if (force) sceneAutoUpdateTimer = Time.realtimeSinceStartup + 2f;

			if (force || rootCache == null || Time.realtimeSinceStartup > nextCacheTime)
			{
				rootCache = SceneManager.GetActiveScene().GetRootGameObjects();
				ddnCache = GetDontDestroyOnLoadObjects();
				hdsCache = GetHideAndDontSaveObjects();
				nextCacheTime = Time.realtimeSinceStartup + 2f;
			}
		}

		private static void ForceRefresh() => nextCacheTime = 0f;

		private static void DrawObjectExplorer(ref bool isObjOpn)
		{
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(440, 660), ImGuiCond.FirstUseEver);
			if (!ImGui.Begin("Hierarchy", ref isObjOpn, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.MenuBar))
			{
				ImGui.End();
				return;
			}

			if (ImGui.BeginMenuBar())
			{
				if (ImGui.MenuItem("+ GameObject"))
				{
					addGOParent = null;
					newGOName = "New GameObject";
					newGOPrimitiveIdx = 0;
					showAddGameObject = true;
				}
				ImGui.EndMenuBar();
			}

			if (ImGui.BeginTabBar("OETopTabs"))
			{
				if (ImGui.BeginTabItem("Scene Explorer"))
				{
					DrawSceneExplorerTab();
					ImGui.EndTabItem();
				}
				if (ImGui.BeginTabItem("Object Search"))
				{
					DrawObjectSearchTab();
					ImGui.EndTabItem();
				}
				ImGui.EndTabBar();
			}

			ImGui.End();
		}


		private static void DrawSceneExplorerTab()
		{
			string activeSceneName = SceneManager.GetActiveScene().name;
			string[] cats = [activeSceneName, "DontDestroyOnLoad", "HideAndDontSave"];

			ImGui.SetNextItemWidth(200f);
			ImGui.Combo("##scenecat", ref selectedSceneIndex, cats, cats.Length);
			ImGui.SameLine();
			if (ImGui.Button("Scene Loader")) ImGui.OpenPopup("SceneLoaderPop");
			ImGui.SameLine();
			//ImGui.Checkbox("Auto", ref sceneAutoUpdate);

			ImGui.SetNextItemWidth(-1f);
			ImGui.InputTextWithHint("##sceneFilter", "Filter by name...", ref sceneNameFilter, 256);
			ImGui.Separator();

			if (ImGui.BeginPopup("SceneLoaderPop"))
			{
				ImGui.Text("Scene Loader");
				ImGui.Separator();
				ImGui.SetNextItemWidth(180f);
				ImGui.InputTextWithHint("##slFilter", "Filter scenes...", ref sceneLoaderFilter, 128);
				ImGui.SameLine();
				string[] modes = ["Single", "Additive"];
				ImGui.SetNextItemWidth(80f);
				ImGui.Combo("##slMode", ref sceneLoadMode, modes, modes.Length);
				int sc = SceneManager.sceneCountInBuildSettings;
				for (int i = 0; i < sc; i++)
				{
					string sPath = SceneUtility.GetScenePathByBuildIndex(i);
					string sName = System.IO.Path.GetFileNameWithoutExtension(sPath);
					if (!string.IsNullOrEmpty(sceneLoaderFilter) && !sName.ToLower().Contains(sceneLoaderFilter.ToLower())) continue;
					if (ImGui.MenuItem($"[{i}] {sName}"))
					{
						SceneManager.LoadScene(i, sceneLoadMode == 0 ? LoadSceneMode.Single : LoadSceneMode.Additive);
						ForceRefresh();
					}
				}
				if (sc == 0) ImGui.TextDisabled("(No build scenes found)");
				ImGui.EndPopup();
			}

			ImGui.BeginChild("SceneScroll##ss");
			var src = selectedSceneIndex == 0 ? rootCache : (selectedSceneIndex == 1 ? ddnCache : hdsCache);
			if (src != null)
			{
				string f = sceneNameFilter.ToLower();
				foreach (var obj in src)
				{
					if (!obj) continue;
					if (!string.IsNullOrEmpty(sceneNameFilter) && !ContainsName(obj, f)) continue;
					DrawNode(obj);
				}
			}
			ImGui.EndChild();
		}

		private static bool ContainsName(GameObject obj, string f)
		{
			if (obj.name.ToLower().Contains(f))
				return true;
			for (int i = 0; i < obj.transform.childCount; i++)
			{
				if (ContainsName(obj.transform.GetChild(i).gameObject, f))
					return true;
			}
			return false;
		}

		private static void DrawObjectSearchTab()
		{
			string[] subs = ["UnityObject", "Singletons", "Static Classes"];
			ImGui.SetNextItemWidth(-1f);
			ImGui.Combo("##osMode", ref objSearchSubTab, subs, subs.Length);
			ImGui.Separator();
			if (objSearchSubTab == 0) DrawUnityObjectSearch();
			else if (objSearchSubTab == 1) DrawSingletonSearch();
			else DrawStaticClassSearch();
		}

		private static void DrawUnityObjectSearch()
		{
			ImGui.SetNextItemWidth(-1f);
			ImGui.InputTextWithHint("##uoName", "Name filter...", ref uoSearchName, 256);
			ImGui.SetNextItemWidth(-1f);
			ImGui.InputTextWithHint("##uoType", "Type (e.g. MeshRenderer)...", ref uoSearchType, 256);
			if (ImGui.Button("Search##uoBtn", new System.Numerics.Vector2(-1, 0)))
				RunUnityObjectSearch();
			ImGui.Separator();
			ImGui.TextDisabled($"{uoResults.Count} result(s)");
			ImGui.BeginChild("UOScroll##uos");
			foreach (var obj in uoResults)
			{
				if (!obj) continue;
				if (ImGui.Selectable($"{obj.name} ({obj.GetType().Name})##{obj.GetInstanceID()}"))
				{
					var go = obj is Component c ? c.gameObject : obj as GameObject;
					if (go) OpenInspectorGO(go);
				}
			}
			ImGui.EndChild();
		}

		private static void RunUnityObjectSearch()
		{
			Type searchType = typeof(UnityEngine.Object);
			if (!string.IsNullOrWhiteSpace(uoSearchType))
			{
				var found = AllTypes().FirstOrDefault(t => t.Name.Equals(uoSearchType, StringComparison.OrdinalIgnoreCase) && typeof(UnityEngine.Object).IsAssignableFrom(t));
				if (found != null) searchType = found;
			}
			var method = typeof(Resources).GetMethod("FindObjectsOfTypeAll", new[] { typeof(Type) });
			var raw = method?.Invoke(null, new object[] { searchType }) as UnityEngine.Object[];
			string nf = uoSearchName.ToLower();
			uoResults = [.. (raw ?? []).Where(o => o && !o.name.Contains("Lunaris") && (object)o != Bridge.go)
				.Where(o => string.IsNullOrEmpty(nf) || o.name.ToLower().Contains(nf))
				.OrderBy(o => o.name).Take(500)];
		}

		private static void DrawSingletonSearch()
		{
			ImGui.SetNextItemWidth(-1f);
			ImGui.InputTextWithHint("##singFilter", "Filter singletons...", ref singletonFilter, 256);
			if (ImGui.Button("Scan##singBtn", new System.Numerics.Vector2(-1, 0)))
			{
				ScanSingletons();
				singletonScanned = true;
			}
			if (!singletonScanned)
			{
				ImGui.TextDisabled("Press Scan to search.");
				return;
			}
			string sf = singletonFilter.ToLower();
			var filt = singletonResults.Where(x => string.IsNullOrEmpty(sf) || x.type.Name.ToLower().Contains(sf)).ToList();
			ImGui.TextDisabled($"{filt.Count} singleton(s)");
			ImGui.BeginChild("SSScroll##sss");
			foreach (var (type, inst) in filt)
			{
				string lbl = inst is UnityEngine.Object uo ? $"{type.FullName}  ->  {uo.name}" : type.FullName;
				if (ImGui.Selectable($"{lbl}##{type.AssemblyQualifiedName}"))
				{
					if (inst is GameObject go)
						OpenInspectorGO(go);
					else if (inst is Component cmp)
						OpenInspectorGO(cmp.gameObject);
				}
			}
			ImGui.EndChild();
		}

		private static void ScanSingletons()
		{
			singletonResults.Clear();
			string[] names = ["Instance", "instance", "Current", "Singleton", "Default", "_instance"];
			foreach (var t in AllTypes())
			{
				if (t.IsInterface || t.IsGenericType) continue;
				foreach (var memberName in names)
				{
					try
					{
						object val = null;
						var f = t.GetField(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
						if (f != null) val = f.GetValue(null);
						else
						{
							var p = t.GetProperty(memberName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
							if (p?.CanRead == true)
								val = p.GetValue(null);
						}
						if (val != null)
						{
							singletonResults.Add((t, val));
							break;
						}
					}
					catch { }
				}
			}
		}

		private static void DrawStaticClassSearch()
		{
			ImGui.SetNextItemWidth(-1f);
			ImGui.InputTextWithHint("##scFilter", "Filter static classes...", ref staticFilter, 256);
			if (ImGui.Button("Scan##scBtn", new System.Numerics.Vector2(-1, 0)))
			{
				ScanStaticClasses();
				staticScanned = true;
			}
			if (!staticScanned)
			{
				ImGui.TextDisabled("Press Scan to search.");
				return;
			}
			string sf = staticFilter.ToLower();
			var filt = staticResults.Where(t => string.IsNullOrEmpty(sf) || (t.FullName ?? "").ToLower().Contains(sf)).ToList();
			ImGui.TextDisabled($"{filt.Count} static class(es)");
			ImGui.BeginChild("SCScroll##scs");
			foreach (var t in filt)
				ImGui.Selectable($"{t.FullName}##{t.AssemblyQualifiedName}");
			ImGui.EndChild();
		}


		private static void DrawNode(GameObject obj)
		{
			int id = obj.GetInstanceID();
			bool hasChildren = obj.transform.childCount > 0;

			if (hasChildren)
			{
				ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0, 0, 0, 0));
				ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new System.Numerics.Vector2(2f, 2f));
				if (ImGui.ArrowButton($"##arr_{id}", openNodes.Contains(id) ? ImGuiDir.Down : ImGuiDir.Right))
				{
					if (openNodes.Contains(id))
						openNodes.Remove(id);
					else
						openNodes.Add(id);
				}
				ImGui.PopStyleVar();
				ImGui.PopStyleColor();
			}
			else
				ImGui.Dummy(new System.Numerics.Vector2(20, 12));

			ImGui.SameLine();
			bool active = obj.activeSelf;
			if (ImGui.Checkbox($"##act_{id}", ref active))
				obj.SetActive(active);
			ImGui.SameLine();

			bool dimmed = !obj.activeInHierarchy;
			if (dimmed) ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.LunarisGrey);

			bool isSelected = activeTab != null && !activeTab.IsComponent && activeTab.GO == obj && isInspectorOpen;
			if (ImGui.Selectable($"{obj.name}##{id}", isSelected))
				OpenInspectorGO(obj);

			if (ImGui.BeginPopupContextItem($"NodeCtx_{id}"))
			{
				if (ImGui.MenuItem("Inspect"))
					OpenInspectorGO(obj);
				if (ImGui.MenuItem("Jump to in tree"))
					JumpToInHierarchy(obj);
				ImGui.Separator();
				if (ImGui.MenuItem("Create Child"))
				{
					addGOParent = obj.transform;
					newGOName = "New GameObject";
					newGOPrimitiveIdx = 0;
					showAddGameObject = true;
				}
				if (ImGui.MenuItem("Duplicate"))
				{
					var d = UnityEngine.Object.Instantiate(obj, obj.transform.parent);
					d.name = obj.name;
					ForceRefresh();
				}
				ImGui.Separator();
				if (ImGui.MenuItem("Destroy"))
				{
					destroyTarget = obj;
					showConfirmDestroy = true;
				}
				ImGui.EndPopup();
			}

			if (dimmed) ImGui.PopStyleColor();

			if (openNodes.Contains(id))
			{
				ImGui.Indent();
				for (int i = 0; i < obj.transform.childCount; i++)
					DrawNode(obj.transform.GetChild(i).gameObject);
				ImGui.Unindent();
			}
		}

		private static void JumpToInHierarchy(GameObject obj)
		{
			var t = obj.transform.parent;
			while (t != null)
			{
				openNodes.Add(t.gameObject.GetInstanceID());
				t = t.parent;
			}
		}


		private static void OpenInspectorGO(GameObject go) => OpenTarget(new InspectTarget(go));

		private static void OpenInspectorComp(Component comp) => OpenTarget(new InspectTarget(comp));
		private static void OpenInspectorObj(UnityEngine.Object obj, Type t, Component parent) => OpenTarget(new InspectTarget(obj, t, parent));

		private static void OpenTarget(InspectTarget t)
		{
			var existing = inspectTargets.FirstOrDefault(x => x.ID == t.ID);
			if (existing == null)
			{
				inspectTargets.Add(t);
				existing = t;
			}
			isInspectorOpen = true;
			activeTab = existing;
			pendingTab = existing;
		}


		private static void DrawInspectorWindow()
		{
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(1000, 680), ImGuiCond.FirstUseEver);
			if (!ImGui.Begin("Inspector", ref isInspectorOpen, ImGuiWindowFlags.NoCollapse))
			{
				ImGui.End();
				return;
			}

			if (inspectTargets.Count == 0)
			{
				ImGui.Text("No objects selected.");
				ImGui.End();
				return;
			}

			if (ImGui.BeginTabBar("InspTabs", ImGuiTabBarFlags.Reorderable | ImGuiTabBarFlags.FittingPolicyResizeDown | ImGuiTabBarFlags.AutoSelectNewTabs))
			{
				for (int i = 0; i < inspectTargets.Count; i++)
				{
					var tgt = inspectTargets[i];

					if (!tgt.GO || (tgt.IsComponent && !tgt.Comp) || (tgt.IsObj && tgt.Obj == null))
					{
						CloseTab(tgt, i);
						i--;
						continue;
					}

					var flags = (tgt == pendingTab) ? ImGuiTabItemFlags.SetSelected : ImGuiTabItemFlags.None;
					bool open = true;
					if (ImGui.BeginTabItem(tgt.TabLabel, ref open, flags))
					{
						activeTab = tgt;
						if (tgt.IsComponent)
							DrawComponentTab(tgt);
						else if(tgt.IsObj)
							DrawComponentObj(tgt);
						else
							DrawGameObjectTab(tgt);
							

						ImGui.EndTabItem();
					}
					if (!open)
					{
						CloseTab(tgt, i);
						i--;
					}
				}

				pendingTab = null;
				if (inspectTargets.Count == 0)
					isInspectorOpen = false;
				ImGui.EndTabBar();
			}
			ImGui.End();
		}

		private static void CloseTab(InspectTarget tgt, int idx)
		{
			_typeCache.Remove(tgt.ID);
			_inspectorStates.Remove(tgt.ID);
			liveUpdateMap.Remove(tgt.ID);
			if (idx >= 0 && idx < inspectTargets.Count)
				inspectTargets.RemoveAt(idx);
		}

		private static void DrawComponentTab(InspectTarget tgt)
		{
			var comp = tgt.Comp;
			int instId = tgt.ID;
			var state = GetState(instId);
			if (!liveUpdateMap.ContainsKey(instId)) liveUpdateMap[instId] = false;

			bool en = true;
			var enP = comp.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
			if (enP?.CanRead == true)
				en = (bool)enP.GetValue(comp);

			if (ImGui.Checkbox($"##cen_{instId}", ref en))
			{
				if (enP?.CanWrite == true)
					enP.SetValue(comp, en);
			}

			ImGui.SameLine();
			ImGui.Text(comp.GetType().Name);
			ImGui.SameLine();
			ImGui.TextDisabled($"on");
			ImGui.SameLine();
			if (ImGui.SmallButton($"{comp.gameObject.name}##gothru_{instId}"))
				OpenInspectorGO(comp.gameObject);
			ImGui.SameLine();

			bool live = liveUpdateMap[instId];

			if (ImGui.Checkbox($"Live##{instId}", ref live))
				liveUpdateMap[instId] = live;

			if (comp is not Transform)
			{
				ImGui.SameLine();
				ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.1f, 0.1f, 1f));
				if (ImGui.SmallButton($"Remove##{instId}"))
				{
					inspectTargets.RemoveAll(t => t.Comp == comp);
					UnityEngine.Object.Destroy(comp);
				}
				ImGui.PopStyleColor();
			}

			ImGui.Separator();
			DrawMemberFilterBar(state);
			ImGui.Separator();

			if (live && _typeCache.ContainsKey(instId))
				_typeCache.Remove(instId);

			ImGui.BeginChild($"CompMembers_{instId}");
			DrawSingleComponentMembers(comp, state, instId);
			ImGui.EndChild();
		}

		private static void DrawComponentObj(InspectTarget tgt)
		{
			var comp = tgt.Obj;
			int instId = tgt.ID;
			var state = GetState(instId);
			if (!liveUpdateMap.ContainsKey(instId)) liveUpdateMap[instId] = false;

			bool en = true;
			var enP = comp.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
			if (enP?.CanRead == true)
				en = (bool)enP.GetValue(comp);

			if (ImGui.Checkbox($"##cen_{instId}", ref en))
			{
				if (enP?.CanWrite == true)
					enP.SetValue(comp, en);
			}

			ImGui.SameLine();
			ImGui.Text(comp.GetType().Name);
			ImGui.SameLine();
			ImGui.TextDisabled($"on");
			ImGui.SameLine();
			if (ImGui.SmallButton($"{tgt.ObjParent.name}##gothru_{instId}"))
				OpenInspectorComp(tgt.ObjParent);
			ImGui.SameLine();

			bool live = liveUpdateMap[instId];

			if (ImGui.Checkbox($"Live##{instId}", ref live))
				liveUpdateMap[instId] = live;

			if (comp is not Transform)
			{
				ImGui.SameLine();
				ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.1f, 0.1f, 1f));
				if (ImGui.SmallButton($"Remove##{instId}"))
				{
					inspectTargets.RemoveAll(t => t.Comp == comp);
					UnityEngine.Object.Destroy(comp);
				}
				ImGui.PopStyleColor();
			}

			ImGui.Separator();
			DrawMemberFilterBar(state);
			ImGui.Separator();

			if (live && _typeCache.ContainsKey(instId))
				_typeCache.Remove(instId);

			ImGui.BeginChild($"CompMembers_{instId}");
			DrawSingleObjMembers(comp, state, instId, tgt.ObjParent);
			ImGui.EndChild();
		}

		private static void DrawGameObjectTab(InspectTarget tgt)
		{
			var obj = tgt.GO;
			int instId = tgt.ID;
			var state = GetState(instId);
			if (!liveUpdateMap.ContainsKey(instId))
				liveUpdateMap[instId] = false;

			DrawGOHeader(obj, instId);
			ImGui.Separator();
			DrawMemberFilterBar(state);
			ImGui.Separator();

			bool live = liveUpdateMap[instId];
			if (live && _typeCache.ContainsKey(instId))
				_typeCache.Remove(instId);

			float rightW = 260f;
			float leftW = ImGui.GetContentRegionAvail().X - rightW - 8f;

			ImGui.BeginChild($"Members_{instId}", new System.Numerics.Vector2(leftW, 0));
			DrawObjectMembers(obj, state, instId);
			ImGui.EndChild();

			ImGui.SameLine();

			ImGui.BeginGroup();
			DrawRightPanel(obj, instId);
			ImGui.EndGroup();
		}

		private static void DrawGOHeader(GameObject obj, int instId)
		{
			var pathParts = new List<GameObject>();
			var t = obj.transform;
			while (t != null) { pathParts.Insert(0, t.gameObject); t = t.parent; }

			if (obj.transform.parent != null)
			{
				if (ImGui.SmallButton($"< Parent##{instId}"))
					OpenInspectorGO(obj.transform.parent.gameObject);
				ImGui.SameLine();
			}
			for (int pi = 0; pi < pathParts.Count; pi++)
			{
				if (pi > 0) { ImGui.SameLine(0, 2); ImGui.TextDisabled("/"); ImGui.SameLine(0, 2); }
				var seg = pathParts[pi];
				ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0, 0, 0, 0));
				ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(1, 1, 1, 0.1f));
				if (ImGui.SmallButton($"{seg.name}##pathseg{pi}_{instId}"))
					OpenInspectorGO(seg);
				ImGui.PopStyleColor(2);
			}

			ImGui.Spacing();

			bool active = obj.activeSelf;
			if (ImGui.Checkbox($"##active_{instId}", ref active)) obj.SetActive(active);
			ImGui.SameLine();
			string goName = obj.name;
			ImGui.SetNextItemWidth(180f);
			if (ImGui.InputText($"##goname_{instId}", ref goName, 256) && ImGui.IsItemDeactivatedAfterEdit())
				obj.name = goName;
			ImGui.SameLine();
			ImGui.TextDisabled("Tag:");
			ImGui.SameLine();
			string tag = obj.tag;
			ImGui.SetNextItemWidth(90f);
			if (ImGui.InputText($"##tag_{instId}", ref tag, 64) && ImGui.IsItemDeactivatedAfterEdit())
			{
				try
				{
					obj.tag = tag;
				}catch {}
			}
			ImGui.SameLine();
			ImGui.TextDisabled("Layer:");
			ImGui.SameLine();
			string[] layers = GetLayerNames();
			int layer = obj.layer;
			ImGui.SetNextItemWidth(110f);
			if (ImGui.Combo($"##layer_{instId}", ref layer, layers, layers.Length))
				obj.layer = layer;
			ImGui.SameLine();
			bool live = liveUpdateMap[instId];
			if (ImGui.Checkbox($"Live##{instId}", ref live))
				liveUpdateMap[instId] = live;
			ImGui.SameLine();
			ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.1f, 0.1f, 1f));
			if (ImGui.SmallButton($"Destroy##{instId}"))
			{
				destroyTarget = obj;
				showConfirmDestroy = true;
			}
			ImGui.PopStyleColor();
			ImGui.SameLine();
			ImGui.TextDisabled($"ID:{instId}");
		}

		private static void DrawMemberFilterBar(InspectorState s)
		{
			ImGui.SetNextItemWidth(180f);
			ImGui.InputTextWithHint("##mf", "Filter members...", ref s.MemberFilter, 256);
			ImGui.SameLine(); ImGui.Checkbox("Fields", ref s.ShowFields);
			ImGui.SameLine(); ImGui.Checkbox("Props", ref s.ShowProps);
			ImGui.SameLine(); ImGui.Checkbox("Methods", ref s.ShowMethods);
			ImGui.SameLine(); ImGui.Checkbox("Static", ref s.ShowStatic);
			ImGui.SameLine(); ImGui.Checkbox("Private", ref s.ShowPrivate);
		}


		private static void DrawRightPanel(GameObject obj, int instId)
		{
			float totalH = ImGui.GetContentRegionAvail().Y;
			int childCount = obj.transform.childCount;
			var components = obj.GetComponents<Component>();
			float compH = totalH * 0.5f;

			ImGui.TextDisabled("Components");
			ImGui.SameLine();
			if (ImGui.Button("Add Component", new System.Numerics.Vector2(-1, 0)))
			{
				addComponentTarget = obj;
				addComponentSearch = "";
				EnsureComponentTypes();
				FilterComponentTypes();
				showAddComponent = true;
			}
			ImGui.Separator();
			ImGui.BeginChild($"Comps_{instId}", new System.Numerics.Vector2(260f, compH));
			DrawComponentList(obj, components);
			ImGui.EndChild();

			if (childCount > 0)
			{
				ImGui.TextDisabled($"Children ({childCount})");
				ImGui.Separator();
				ImGui.BeginChild($"Children_{instId}", new System.Numerics.Vector2(260f, 0));
				for (int i = 0; i < childCount; i++)
				{
					var child = obj.transform.GetChild(i).gameObject;
					bool childActive = child.activeSelf;
					if (ImGui.Checkbox($"##cact_{child.GetInstanceID()}", ref childActive))
						child.SetActive(childActive);
					ImGui.SameLine();
					if (!child.activeInHierarchy) ImGui.PushStyleColor(ImGuiCol.Text, UI.LunarisColors.LunarisGrey);
					if (ImGui.Selectable($"{child.name}##child_{child.GetInstanceID()}"))
						OpenInspectorGO(child);
					if (!child.activeInHierarchy) ImGui.PopStyleColor();
				}
				ImGui.EndChild();
			}
		}

		private static void DrawComponentList(GameObject obj, Component[] comps)
		{
			foreach (var comp in comps)
			{
				if (!comp) continue;
				bool en = true;
				var enP = comp.GetType().GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
				if (enP?.CanRead == true)
					en = (bool)enP.GetValue(comp);

				if (ImGui.Checkbox($"##cen_{comp.GetInstanceID()}", ref en))
					if (enP?.CanWrite == true) enP.SetValue(comp, en);
				ImGui.SameLine();

				if (comp is not Transform)
				{
					ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.5f, 0.1f, 0.1f, 1f));
					if (ImGui.SmallButton($"X##{comp.GetInstanceID()}"))
						UnityEngine.Object.Destroy(comp);
					ImGui.PopStyleColor();
					ImGui.SameLine();
				}

				if (ImGui.Selectable($"{comp.GetType().Name}##{comp.GetInstanceID()}"))
					OpenInspectorComp(comp);
			}
			ImGui.Separator();
		}

		private static CachedMember[] GetCachedMembers(Type type, int cacheId, object target = null)
		{
			if (!_typeCache.TryGetValue(cacheId, out var byType))
				_typeCache[cacheId] = byType = [];

			if (byType.TryGetValue(type, out var cached))
				return cached;

			var list = new List<CachedMember>();
			var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.FlattenHierarchy;


			if (target is Material mat)
			{
				int propCount = mat.shader.GetPropertyCount();
				for (int i = 0; i < propCount; i++)
				{
					string propName = mat.shader.GetPropertyName(i);
					var propType = mat.shader.GetPropertyType(i);

					list.Add(new CachedMember { Name = propName, ShaderPropName = propName, ShaderPropType = propType, DeclaringType = typeof(Material),
						Type = propType switch
						{
							UnityEngine.Rendering.ShaderPropertyType.Color => typeof(Color),
							UnityEngine.Rendering.ShaderPropertyType.Vector => typeof(Vector4),
							UnityEngine.Rendering.ShaderPropertyType.Texture => typeof(Texture),
							_ => typeof(float)
						}});
				}
			}

			foreach (var f in type.GetFields(flags))
			{
				if (f.Name.Contains("<") || f.Name.Contains("OffsetOfInstance")) continue;

				list.Add(new CachedMember { Name = f.Name, Type = f.FieldType, Field = f, DeclaringType = f.DeclaringType });
			}
			foreach (var p in type.GetProperties(flags))
			{
				if (!p.CanRead || _blacklisted.Contains(p.Name)) continue;
				if (Attribute.IsDefined(p, typeof(ObsoleteAttribute))) continue;

				string tn = type.FullName ?? "";
				if (tn.Contains("UnityEngine") && (p.Name.StartsWith("body") || p.Name.StartsWith("root"))) continue;

				list.Add(new CachedMember { Name = p.Name, Type = p.PropertyType, Prop = p, DeclaringType = p.DeclaringType });
			}
			foreach (var m in type.GetMethods(flags))
			{
				if (m.IsSpecialName || m.Name.Contains("<")) continue;
				if (m.DeclaringType == typeof(object) || m.DeclaringType == typeof(UnityEngine.Object)) continue;

				list.Add(new CachedMember { Name = m.Name, Type = m.ReturnType, Method = m, DeclaringType = m.DeclaringType });
			}

			list.Sort((a, b) =>
			{
				if (a.IsMethod != b.IsMethod) return a.IsMethod ? 1 : -1;

				bool aOwn = a.DeclaringType == type;
				bool bOwn = b.DeclaringType == type;

				if (aOwn != bOwn) return aOwn ? -1 : 1;

				int dtCmp = string.Compare(a.DeclaringType?.Name, b.DeclaringType?.Name, StringComparison.OrdinalIgnoreCase);

				if (dtCmp != 0) return dtCmp;

				return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
			});

			return byType[type] = [.. list];
		}

		private static void DrawObjectMembers(GameObject go, InspectorState state, int cacheId)
		{
			foreach (var comp in go.GetComponents<Component>())
			{
				if (!comp) continue;
				if (!ImGui.CollapsingHeader($"{comp.GetType().Name}##{comp.GetInstanceID()}"))
					continue;

				DrawSingleComponentMembers(comp, state, cacheId);
			}
		}

		private static void DrawSingleComponentMembers(Component comp, InspectorState state, int cacheId)
		{
			var members = GetCachedMembers(comp.GetType(), cacheId).Where(m => PassesFilter(m, state)).ToArray();

			if (!ImGui.BeginTable($"Tbl_{comp.GetInstanceID()}", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit))
				return;

			ImGui.TableSetupColumn("Member", ImGuiTableColumnFlags.WidthFixed, 240f);
			ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);

			foreach (var m in members)
			{
				if (m.IsMethod)
				{
					DrawMethodRow(comp, m, state);
					continue;
				}
				try
				{
					if (m.Value == null)
						m.Pull(comp);
				} catch
				{
					continue;
				}

				System.Numerics.Vector4? col = m.IsStatic ? UI.LunarisColors.ParsedGold : m.IsPrivate ? UI.LunarisColors.ParsedOrange : null;
				if (col.HasValue)
					ImGui.PushStyleColor(ImGuiCol.Text, col.Value);
				DrawMemberRow(comp, m, state, cacheId);
				if (col.HasValue)
					ImGui.PopStyleColor();
			}
			ImGui.EndTable();
		}

		private static void DrawSingleObjMembers(UnityEngine.Object comp, InspectorState state, int cacheId, Component par)
		{
			var members = GetCachedMembers(comp.GetType(), cacheId, comp).Where(m => PassesFilter(m, state)).ToArray();

			if (!ImGui.BeginTable($"Tbl_{comp.GetInstanceID()}", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingFixedFit))
				return;

			ImGui.TableSetupColumn("Member", ImGuiTableColumnFlags.WidthFixed, 240f);
			ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);


			foreach (var m in members)
			{
				if (m.IsMethod)
				{
					//DrawMethodRow(comp, m, state);
					continue;
				}
				try
				{
					if (m.Value == null)
						m.Pull(comp);
				}
				catch
				{
					continue;
				}

				System.Numerics.Vector4? col = m.IsStatic ? UI.LunarisColors.ParsedGold : m.IsPrivate ? UI.LunarisColors.ParsedOrange : null;
				if (col.HasValue)
					ImGui.PushStyleColor(ImGuiCol.Text, col.Value);
				DrawMemberRow(par, m, state, cacheId);
				if (col.HasValue)
					ImGui.PopStyleColor();
			}
			ImGui.EndTable();
		}

		private static bool PassesFilter(CachedMember m, InspectorState s)
		{
			if (!s.ShowFields && m.IsField) return false;
			if (!s.ShowProps && m.IsProp) return false;
			if (!s.ShowMethods && m.IsMethod) return false;
			if (!s.ShowStatic && m.IsStatic) return false;
			if (!s.ShowPrivate && m.IsPrivate) return false;
			if (!string.IsNullOrEmpty(s.MemberFilter) && !m.Name.ToLower().Contains(s.MemberFilter.ToLower()) && !m.DisplayName.ToLower().Contains(s.MemberFilter.ToLower())) return false;
			return true;
		}

		private static void DrawMethodRow(Component target, CachedMember m, InspectorState state)
		{
			string sig = $"{target.GetInstanceID()}.{m.DeclaringType?.Name}.{m.Name}({string.Join(",", m.Parameters.Select(p => p.ParameterType.Name))})";
			ImGui.TableNextRow();
			ImGui.TableSetColumnIndex(0);
			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.4f, 0.8f, 1f, 1f));
			ImGui.Text($"[M] {m.DisplayName}");
			ImGui.PopStyleColor();
			ImGui.TableSetColumnIndex(1);

			var parms = m.Parameters;
			if (!state.MethodParams.ContainsKey(sig))
				state.MethodParams[sig] = new string[parms.Length];
			var bufs = state.MethodParams[sig];

			for (int pi = 0; pi < parms.Length; pi++)
			{
				if (bufs[pi] == null) bufs[pi] = "";
				ImGui.SetNextItemWidth(90f);
				ImGui.InputText($"{parms[pi].Name}##{sig}{pi}", ref bufs[pi], 256);
				if (pi < parms.Length - 1)
					ImGui.SameLine();
			}
			if (parms.Length > 0)
				ImGui.SameLine();

			if (ImGui.SmallButton($"Invoke##{sig}"))
			{
				try
				{
					var args = new object[parms.Length];
					for (int pi = 0; pi < parms.Length; pi++)
						args[pi] = ParseParam(bufs[pi] ?? "", parms[pi].ParameterType);
					var result = m.Method.Invoke(m.IsStatic ? null : target, args);
					state.MethodResults[sig] = result == null ? "(void/null)" : result.ToString();
				}
				catch (Exception ex)
				{
					state.MethodResults[sig] = $"Error: {ex.InnerException?.Message ?? ex.Message}";
				}
			}
			if (state.MethodResults.TryGetValue(sig, out var res))
			{
				ImGui.SameLine();
				ImGui.TextDisabled($"-> {res}");
			}
		}

		private static object ParseParam(string input, Type t)
		{
			if (t == typeof(string)) return input;
			if (t == typeof(int)) { int.TryParse(input, out int v); return v; }
			if (t == typeof(float)) { float.TryParse(input, out float v); return v; }
			if (t == typeof(double)) { double.TryParse(input, out double v); return v; }
			if (t == typeof(bool)) { bool.TryParse(input, out bool v); return v; }
			if (t.IsEnum)
			{
				try
				{
					return Enum.Parse(t, input, true);
				}
				catch
				{
					return Enum.GetValues(t).GetValue(0);
				}
			}
			return null;
		}

		private static void DrawMemberRow(Component target, CachedMember m, InspectorState state, int cacheId)
		{
			string expandKey = $"{target.GetInstanceID()}.{m.DeclaringType?.Name}.{m.Name}";
			ImGui.TableNextRow();
			ImGui.TableSetColumnIndex(0);
			string prefix = m.IsStatic ? "[S] " : (m.IsPrivate ? "[~] " : "");
			ImGui.Text($"{prefix}{m.DisplayName}");
			ImGui.TableSetColumnIndex(1);

			object val;
			try
			{
				val = m.Value;
			}
			catch
			{
				ImGui.TextDisabled("(error)");
				return;
			}
			if (val == null)
			{
				ImGui.TextDisabled("null");
				return;
			}

			string lbl = $"##{target.GetInstanceID()}{m.DeclaringType?.Name}{m.Name}";

			try
			{
				DrawValueWidget(target, m, val, m.Type, lbl, state, expandKey);
			}
			catch(Exception ex)
			{
				ImGui.TextDisabled($"(ex: {ex.GetType().Name})");
			}
		}

		private static void DrawValueWidget(Component target, CachedMember m, object val, Type type, string lbl, InspectorState state, string expandKey)
		{
			void Set(object v)
			{
				m.Value = v;
				m.Push(target);
			}

			if (type == typeof(float))
			{
				float f = (float)val;
				if (ImGui.DragFloat(lbl, ref f, 0.01f))
					Set(f);
			}
			else if (type == typeof(int))
			{
				int i = (int)val;
				if (ImGui.DragInt(lbl, ref i))
					Set(i);
			}
			else if (type == typeof(uint))
			{
				int i = (int)(uint)val;
				if (ImGui.DragInt(lbl, ref i))
					Set((uint)Math.Max(0, i));
			}
			else if (type == typeof(double))
			{
				float f = (float)(double)val;
				if (ImGui.DragFloat(lbl, ref f, 0.01f))
					Set((double)f);
			}
			else if (type == typeof(long))
			{
				int i = (int)(long)val;
				if (ImGui.DragInt(lbl, ref i))
					Set((long)i);
			}
			else if (type == typeof(short))
			{
				int i = (short)val;
				if (ImGui.DragInt(lbl, ref i))
					Set((short)i);
			}
			else if (type == typeof(byte))
			{
				int i = (byte)val;
				if (ImGui.DragInt(lbl, ref i))
					Set((byte)MathF.Clamp(i, 0, 255));
			}
			else if (type == typeof(bool))
			{
				bool b = (bool)val;
				if (ImGui.Checkbox(lbl, ref b))
				Set(b);
			}
			else if (type == typeof(string))
			{
				string s = (string)val ?? "";
				if (ImGui.InputText(lbl, ref s, 512))
					Set(s);
			}
			else if (type.IsEnum)
			{
				bool flags = type.IsDefined(typeof(FlagsAttribute), false);
				string[] names = Enum.GetNames(type);
				if (flags)
				{
					if (ImGui.SmallButton($"{val}##{lbl}flags"))
						ImGui.OpenPopup($"FlagsEdit{lbl}");
					if (ImGui.BeginPopup($"FlagsEdit{lbl}"))
					{
						long current = Convert.ToInt64(val);
						foreach (var n in names)
						{
							long bit = Convert.ToInt64(Enum.Parse(type, n));
							if (bit == 0) continue;
							bool on = (current & bit) != 0;
							if (ImGui.Checkbox($"{n}##fe{lbl}{n}", ref on))
								current = on ? (current | bit) : (current & ~bit);
						}
						Set(Enum.ToObject(type, current));
						ImGui.EndPopup();
					}
				}
				else
				{
					int idx = Array.IndexOf(names, val.ToString());
					if (idx < 0) idx = 0;
					if (ImGui.Combo(lbl, ref idx, names, names.Length))
						Set(Enum.Parse(type, names[idx]));
				}
			}
			else if (type == typeof(Vector2))
			{
				Vector2 v = (Vector2)val;
				var nv = new System.Numerics.Vector2(v.x, v.y);
				if (ImGui.DragFloat2(lbl, ref nv, 0.01f))
					Set(new Vector2(nv.X, nv.Y));
			}
			else if (type == typeof(Vector3))
			{
				Vector3 v = (Vector3)val;
				var nv = new System.Numerics.Vector3(v.x, v.y, v.z);
				if (ImGui.DragFloat3(lbl, ref nv, 0.01f))
					Set(new Vector3(nv.X, nv.Y, nv.Z));
			}
			else if (type == typeof(Vector4))
			{
				Vector4 v = (Vector4)val;
				var nv = new System.Numerics.Vector4(v.x, v.y, v.z, v.w);
				if (ImGui.DragFloat4(lbl, ref nv, 0.01f))
					Set(new Vector4(nv.X, nv.Y, nv.Z, nv.W));
			}
			else if (type == typeof(Vector2Int))
			{
				Vector2Int v = (Vector2Int)val;
				var nv = new System.Numerics.Vector2(v.x, v.y);
				if (ImGui.DragFloat2(lbl, ref nv, 1f))
					Set(new Vector2Int((int)nv.X, (int)nv.Y));
			}
			else if (type == typeof(Vector3Int))
			{
				Vector3Int v = (Vector3Int)val;
				var nv = new System.Numerics.Vector3(v.x, v.y, v.z);
				if (ImGui.DragFloat3(lbl, ref nv, 1f))
					Set(new Vector3Int((int)nv.X, (int)nv.Y, (int)nv.Z));
			}
			else if (type == typeof(Rect))
			{
				Rect r = (Rect)val;
				var nv = new System.Numerics.Vector4(r.x, r.y, r.width, r.height);
				if (ImGui.DragFloat4(lbl, ref nv, 0.01f))
					Set(new Rect(nv.X, nv.Y, nv.Z, nv.W));
			}
			else if (type == typeof(RectInt))
			{
				RectInt r = (RectInt)val;
				var nv = new System.Numerics.Vector4(r.x, r.y, r.width, r.height);
				if (ImGui.DragFloat4(lbl, ref nv, 1f))
					Set(new RectInt((int)nv.X, (int)nv.Y, (int)nv.Z, (int)nv.W));
			}
			else if (type == typeof(Color))
			{
				Color c = (Color)val;
				var nc = new System.Numerics.Vector4(c.r, c.g, c.b, c.a);
				if (ImGui.ColorEdit4(lbl, ref nc))
					Set(new Color(nc.X, nc.Y, nc.Z, nc.W));
			}
			else if (type == typeof(Color32))
			{
				Color c = (Color32)val;
				var nc = new System.Numerics.Vector4(c.r, c.g, c.b, c.a);
				if (ImGui.ColorEdit4(lbl, ref nc))
					Set((Color32)new Color(nc.X, nc.Y, nc.Z, nc.W));
			}
			else if (type == typeof(Quaternion))
			{
				Quaternion q = (Quaternion)val;
				var euler = new System.Numerics.Vector3(q.eulerAngles.x, q.eulerAngles.y, q.eulerAngles.z);
				if (ImGui.DragFloat3(lbl, ref euler, 0.1f))
					Set(Quaternion.Euler(euler.X, euler.Y, euler.Z));
			}
			else if (type == typeof(LayerMask))
			{
				int mask = ((LayerMask)val).value;
				if (ImGui.DragInt(lbl, ref mask))
					Set((LayerMask)mask);
			}
			else if (type == typeof(Bounds))
			{
				DrawBoundsWidget(m, val, lbl, state, expandKey, Set);
			}
			else if (type == typeof(BoundsInt))
			{
				DrawBoundsIntWidget(m, val, lbl, state, expandKey, Set);
			}
			else if (type == typeof(Matrix4x4))
			{
				DrawMatrix4x4Widget(m, val, lbl, state, expandKey, Set);
			}
			else if (type == typeof(AnimationCurve))
			{
				var curve = (AnimationCurve)val;
				int kc = curve?.keys.Length ?? 0;
				ImGui.TextDisabled($"AnimationCurve ({kc} keys)");
			}
			else if (type == typeof(Gradient))
			{
				ImGui.TextDisabled("Gradient");
			}
			else if (val is Texture2D t2d)
			{
				DrawTextureWidget(t2d, lbl, m.Name);
			}
			else if (typeof(Texture).IsAssignableFrom(type))
			{
				ImGui.TextDisabled($"Texture ({type.Name}): {(val as UnityEngine.Object)?.name ?? "?"}");
			}
			else if (type == typeof(Sprite))
			{
				var sprite = (Sprite)val;
				DrawTextureWidget(sprite?.texture, lbl, m.Name);
				if (sprite != null)
				{
					ImGui.SameLine();
					ImGui.TextDisabled($"(Sprite: {sprite.name})");
				}
			}
			else if (typeof(Material).IsAssignableFrom(type))
			{
				DrawMaterialWidget(val as Material, lbl, target);
			}
			else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
			{
				var uo = val as UnityEngine.Object;
				string objLabel = uo ? $"{uo.name} ({type.Name})" : "None";
				if (ImGui.Selectable($"{objLabel}{lbl}"))
				{
					if (uo is GameObject go2)
						OpenInspectorGO(go2);
					else if (uo is Component c2)
						OpenInspectorComp(c2);
				}
			}
			else if (IsEditableList(type, out var elemType))
			{
				DrawEditableListWidget(m, val, type, elemType, lbl, state, expandKey, Set);
			}
			else if (IsExpandableEnumerable(type))
			{
				ToggleExpand(state, expandKey);
				ImGui.SameLine();
				ImGui.TextDisabled($"{type.Name} [{GetCount(val)}]");
				if (state.Expanded.Contains(expandKey))
					DrawReadOnlyEnumerable(val, expandKey, state);
			}
			else if (!type.IsPrimitive && type != typeof(string))
			{
				ToggleExpand(state, expandKey);
				ImGui.SameLine();
				ImGui.TextDisabled($"{type.Name}: {val}");
				if (state.Expanded.Contains(expandKey))
					DrawStructFields(val, type);
			}
			else
			{
				ImGui.TextDisabled(val.ToString());
			}
		}

		private static void DrawBoundsWidget(CachedMember m, object val, string lbl, InspectorState state, string expandKey, Action<object> set)
		{
			ToggleExpand(state, expandKey);
			ImGui.SameLine();
			var b = (Bounds)val;
			ImGui.TextDisabled($"Bounds  C:{b.center}  E:{b.extents}");
			if (!state.Expanded.Contains(expandKey))
				return;

			ImGui.Indent();
			var center = new System.Numerics.Vector3(b.center.x, b.center.y, b.center.z);
			var size = new System.Numerics.Vector3(b.size.x, b.size.y, b.size.z);
			bool ch = false;
			if (ImGui.DragFloat3($"Center{lbl}", ref center, 0.01f))
				ch = true;
			if (ImGui.DragFloat3($"Size{lbl}", ref size, 0.01f))
				ch = true;
			if (ch)
				set(new Bounds(new Vector3(center.X, center.Y, center.Z), new Vector3(size.X, size.Y, size.Z)));
			ImGui.Unindent();
		}

		private static void DrawBoundsIntWidget(CachedMember m, object val, string lbl,
			InspectorState state, string expandKey, Action<object> set)
		{
			ToggleExpand(state, expandKey);
			ImGui.SameLine();
			var b = (BoundsInt)val;
			ImGui.TextDisabled($"BoundsInt  P:{b.position}  S:{b.size}");
			if (!state.Expanded.Contains(expandKey))
				return;

			ImGui.Indent();
			var pos = new System.Numerics.Vector3(b.position.x, b.position.y, b.position.z);
			var size = new System.Numerics.Vector3(b.size.x, b.size.y, b.size.z);
			bool ch = false;
			if (ImGui.DragFloat3($"Position{lbl}", ref pos, 1f))
				ch = true;
			if (ImGui.DragFloat3($"Size{lbl}", ref size, 1f))
				ch = true;
			if (ch)
				set(new BoundsInt(new Vector3Int((int)pos.X, (int)pos.Y, (int)pos.Z), new Vector3Int((int)size.X, (int)size.Y, (int)size.Z)));
			ImGui.Unindent();
		}


		private static void DrawMatrix4x4Widget(CachedMember m, object val, string lbl, InspectorState state, string expandKey, Action<object> set)
		{
			ToggleExpand(state, expandKey);
			ImGui.SameLine();
			ImGui.TextDisabled("Matrix4x4");
			if (!state.Expanded.Contains(expandKey))
				return;

			var mat = (Matrix4x4)val;
			bool changed = false;
			ImGui.Indent();
			for (int row = 0; row < 4; row++)
			{
				var rowVec = new System.Numerics.Vector4(mat[row, 0], mat[row, 1], mat[row, 2], mat[row, 3]);
				if (ImGui.DragFloat4($"##mat{lbl}r{row}", ref rowVec, 0.001f))
				{
					mat[row, 0] = rowVec.X; mat[row, 1] = rowVec.Y;
					mat[row, 2] = rowVec.Z; mat[row, 3] = rowVec.W;
					changed = true;
				}
			}
			ImGui.Unindent();
			if (changed) set(mat);
		}

		private static void DrawTextureWidget(Texture2D tex, string lbl, string memberName)
		{
			if (tex == null)
			{
				ImGui.TextDisabled("None (Texture2D)");
				return;
			}
			ImGui.Text($"{tex.name} ({tex.width}x{tex.height})");
			ImGui.SameLine();
			if (ImGui.SmallButton($"View{lbl}"))
			{
				previewTexture = tex;
				previewTextureName = $"{memberName}: {tex.name} ({tex.width}x{tex.height})";
				showTexturePreview = true;
			}
		}

		private static void DrawMaterialWidget(Material mat, string lbl, Component comp)
		{
			if (mat == null)
			{
				ImGui.TextDisabled("None (Material)");
				return;
			}

			int propCount = mat.shader.GetPropertyCount();
			for (int i = 0; i < propCount; i++)
			{
				if (mat.shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Texture)
				{
					string propName = mat.shader.GetPropertyName(i);
					Texture tex = mat.GetTexture(propName);

					if (tex is Texture2D t2d)
					{
						if (ImGui.SmallButton($"View {propName}##{lbl}_{i}"))
						{
							previewTexture = t2d;
							previewTextureName = $"{mat.name} [{propName}]: {t2d.name} ({t2d.width}x{t2d.height})";
							showTexturePreview = true;
						}
						ImGui.SameLine();
						break; //just assume the first texture found is *the chosen one*
					}
				}
			}

			if (ImGui.Selectable($"{mat.name}##{mat.GetInstanceID()}_select"))
			{
				OpenInspectorObj(mat, typeof(Material), comp);
			}
		}

		private static void DrawTexturePreviewPopup()
		{
			if (!showTexturePreview || previewTexture == null)
			{
				showTexturePreview = false;
				return;
			}

			float maxSize = 600f;
			float aspect = (float)previewTexture.width / Math.Max(1, previewTexture.height);
			float dispW = aspect >= 1f ? maxSize : maxSize * aspect;
			float dispH = aspect >= 1f ? maxSize / aspect : maxSize;

			ImGui.SetNextWindowSize(new System.Numerics.Vector2(dispW + 20f, dispH + 60f), ImGuiCond.Always);
			ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.FirstUseEver, new System.Numerics.Vector2(0.5f, 0.5f));
			if (ImGui.Begin($"Texture: {previewTextureName}##texpreview", ref showTexturePreview, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
			{
				IntPtr ptr = previewTexture.GetNativeTexturePtr();
				ImGuiWrap.RegisterTexture(ptr, previewTexture);
				ImGui.Image(ptr, new System.Numerics.Vector2(dispW, dispH));
			}
			ImGui.End();

			if (!showTexturePreview)
			{
				IntPtr ptr = previewTexture.GetNativeTexturePtr();
				ImGuiWrap.UnregisterTexture(ptr);
			}
		}

		private static bool IsEditableList(Type type, out Type elemType)
		{
			elemType = null;
			if (!typeof(IList).IsAssignableFrom(type))
				return false;
			if (type.IsArray)
			{
				elemType = type.GetElementType();
				return elemType != null;
			}
			var gen = type.GetGenericArguments();
			if (gen.Length == 1)
			{
				elemType = gen[0];
				return true;
			}
			return false;
		}

		private static void DrawEditableListWidget(CachedMember m, object val, Type type, Type elemType, string lbl, InspectorState state, string expandKey, Action<object> set)
		{
			var list = (IList)val;
			ToggleExpand(state, expandKey);
			ImGui.SameLine();
			ImGui.TextDisabled($"{type.Name} [{list.Count}]");
			if (!state.Expanded.Contains(expandKey)) return;

			bool canSimpleEdit = IsSimpleEditable(elemType);
			/*bool isGO = elemType == typeof(GameObject);
			bool isComp = typeof(Component).IsAssignableFrom(elemType);
			bool isUO = typeof(UnityEngine.Object).IsAssignableFrom(elemType);*/

			ImGui.Indent();
			for (int idx = 0; idx < list.Count; idx++)
			{
				var item = list[idx];

				if (item is GameObject go)
				{
					ImGui.Text($"[{idx}]");
					ImGui.SameLine();
					if (ImGui.Selectable($"{go.name}##listgo{lbl}{idx}"))
						OpenInspectorGO(go);
				}
				else if (item is Component c2)
				{
					ImGui.Text($"[{idx}]");
					ImGui.SameLine();
					if (ImGui.Selectable($"{c2.GetType().Name} @ {c2.gameObject.name}##listc{lbl}{idx}"))
						OpenInspectorComp(c2);
				}
				else if (item is UnityEngine.Object uo)
				{
					ImGui.Text($"[{idx}]");
					ImGui.SameLine();
					ImGui.TextDisabled($"{uo.name} ({elemType.Name})");
				}
				else if (canSimpleEdit)
				{
					ImGui.Text($"[{idx}]");
					ImGui.SameLine();
					object edited = DrawInlineEditable($"{lbl}_{idx}", elemType, item);
					if (!Equals(edited, item))
					{
						try
						{
							if (type.IsArray)
							{
								var arr = (Array)list;
								var newArr = Array.CreateInstance(elemType, arr.Length);
								arr.CopyTo(newArr, 0);
								newArr.SetValue(edited, idx);
								set(newArr);
							}
							else
							{
								list[idx] = edited;
								set(list);
							}
						}
						catch { }
					}
				}
				else
				{
					string itemKey = $"{expandKey}[{idx}]";
					ToggleExpand(state, itemKey);
					ImGui.SameLine();
					ImGui.Text($"[{idx}] {item?.ToString() ?? "null"}");
					if (state.Expanded.Contains(itemKey) && item != null)
						DrawStructFields(item, elemType);
				}
			}

			if (!type.IsArray)
			{
				if (ImGui.SmallButton($"+ Add##{lbl}"))
				{
					try
					{
						object newElem = elemType.IsValueType ? Activator.CreateInstance(elemType) : null;
						list.Add(newElem);
						set(list);
					}
					catch { }
				}
				if (list.Count > 0)
				{
					ImGui.SameLine();
					if (ImGui.SmallButton($"- Remove Last##{lbl}"))
					{
						try
						{
							list.RemoveAt(list.Count - 1);
							set(list);
						} catch { }
					}
				}
			}
			ImGui.Unindent();
		}

		private static bool IsSimpleEditable(Type t) => t == typeof(float) || t == typeof(int) || t == typeof(bool) || t == typeof(string) || t == typeof(double) || t.IsEnum;

		private static object DrawInlineEditable(string lbl, Type t, object val)
		{
			if (t == typeof(float)) { float f = val != null ? (float)val : 0f; if (ImGui.DragFloat(lbl, ref f, 0.01f)) return f; }
			else if (t == typeof(int)) { int i = val != null ? (int)val : 0; if (ImGui.DragInt(lbl, ref i)) return i; }
			else if (t == typeof(bool)) { bool b = val != null && (bool)val; if (ImGui.Checkbox(lbl, ref b)) return b; }
			else if (t == typeof(string)) { string s = (string)val ?? ""; if (ImGui.InputText(lbl, ref s, 512)) return s; }
			else if (t == typeof(double)) { float f = val != null ? (float)(double)val : 0f; if (ImGui.DragFloat(lbl, ref f, 0.01f)) return (double)f; }
			else if (t.IsEnum)
			{
				string[] names = Enum.GetNames(t);
				int idx = val != null ? Array.IndexOf(names, val.ToString()) : 0;
				if (idx < 0) idx = 0;
				if (ImGui.Combo(lbl, ref idx, names, names.Length))
					return Enum.Parse(t, names[idx]);
			}
			else { ImGui.TextDisabled(val?.ToString() ?? "null"); }
			return val;
		}

		private static void ToggleExpand(InspectorState state, string key)
		{
			bool ex = state.Expanded.Contains(key);
			if (ImGui.SmallButton(ex ? $"v##{key}" : $">##{key}"))
			{
				if (ex)
					state.Expanded.Remove(key);
				else
					state.Expanded.Add(key);
			}
		}

		private static bool IsExpandableEnumerable(Type t) => typeof(IEnumerable).IsAssignableFrom(t) && t != typeof(string);

		private static int GetCount(object val)
		{
			if (val is ICollection c) return c.Count;
			int n = 0;
			try
			{
				foreach (var _ in (IEnumerable)val)
				{
					if (++n > 9999)
						break;
				}
			} catch { }

			return n;
		}

		private static void DrawReadOnlyEnumerable(object val, string key, InspectorState state)
		{
			ImGui.Indent();
			int idx = 0;
			try
			{
				if (val is IDictionary dict)
				{
					foreach (DictionaryEntry e in dict)
					{
						ImGui.Text($"  [{idx}] {e.Key}  ->  {e.Value?.ToString() ?? "null"}");
						TryInspectLink(e.Value, key, idx);
					}
				}
				else
				{
					foreach (var item in (IEnumerable)val)
					{
						ImGui.Text($"  [{idx}] {item?.ToString() ?? "null"}");
						TryInspectLink(item, key, idx);
					}
				}
			}
			catch
			{
				ImGui.TextDisabled("  (error iterating)");
			}
			ImGui.Unindent();
		}

		private static void DrawStructFields(object val, Type type)
		{
			ImGui.Indent();
			foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
			{
				try
				{
					ImGui.Text($"  {f.Name}: {f.GetValue(val)?.ToString() ?? "null"}");
				} catch { }
			}
			foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (!p.CanRead)
					continue;
				try
				{
					ImGui.Text($"  {p.Name}: {p.GetValue(val)?.ToString() ?? "null"}");
				} catch { }
			}
			ImGui.Unindent();
		}

		private static void TryInspectLink(object item, string key, int idx)
		{
			if (item is GameObject go)
			{
				ImGui.SameLine();
				if (ImGui.SmallButton($"Inspect##{key}{idx}"))
					OpenInspectorGO(go);
			}
			else if (item is Component c)
			{
				ImGui.SameLine();
				if (ImGui.SmallButton($"Inspect##{key}{idx}"))
					OpenInspectorComp(c);
			}
		}


		private static void EnsureComponentTypes()
		{
			if (allComponentTypes != null) return;
			allComponentTypes = [.. AllTypes().Where(t => typeof(Component).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface && !t.IsGenericType).OrderBy(t => t.FullName)];
		}

		private static void FilterComponentTypes()
		{
			string q = addComponentSearch.ToLower();
			filteredComponentTypes = string.IsNullOrEmpty(q) ? [.. allComponentTypes.Take(100)] : [.. allComponentTypes.Where(t => t.Name.ToLower().Contains(q) || (t.FullName ?? "").ToLower().Contains(q)).Take(100)];
		}

		private static void DrawAddComponentPopup()
		{
			if (!showAddComponent) return;
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(420, 460), ImGuiCond.Always);
			ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new System.Numerics.Vector2(0.5f, 0.5f));
			if (ImGui.Begin("Add Component##popup", ref showAddComponent, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
			{
				ImGui.SetNextItemWidth(-1f);
				if (ImGui.InputTextWithHint("##acs", "Search...", ref addComponentSearch, 256))
					FilterComponentTypes();
				ImGui.Separator();
				ImGui.BeginChild("ACList##acl", new System.Numerics.Vector2(0, -35));
				foreach (var t in filteredComponentTypes)
				{
					if (ImGui.Selectable($"{t.FullName}##{t.AssemblyQualifiedName}"))
					{
						if (addComponentTarget)
							addComponentTarget.AddComponent(t);
						showAddComponent = false;
					}
				}
				ImGui.EndChild();
				if (ImGui.Button("Cancel", new System.Numerics.Vector2(-1, 0)))
					showAddComponent = false;
			}
			ImGui.End();
		}

		private static void DrawAddGameObjectPopup()
		{
			if (!showAddGameObject) return;
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(360, 190), ImGuiCond.Always);
			ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new System.Numerics.Vector2(0.5f, 0.5f));
			if (ImGui.Begin("Create GameObject##popup", ref showAddGameObject, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
			{
				ImGui.InputText("Name##gon", ref newGOName, 256);
				ImGui.Combo("Type##got", ref newGOPrimitiveIdx, primitiveOptions, primitiveOptions.Length);
				ImGui.Separator();
				if (ImGui.Button("Create", new System.Numerics.Vector2(160, 0)))
				{
					GameObject created;
					string prim = primitiveOptions[newGOPrimitiveIdx];
					if (prim == "Empty")
						created = new GameObject(newGOName);
					else
					{
						created = GameObject.CreatePrimitive((PrimitiveType)Enum.Parse(typeof(PrimitiveType), prim));
						created.name = newGOName;
					}
					if (addGOParent != null)
						created.transform.SetParent(addGOParent, false);
					OpenInspectorGO(created);
					ForceRefresh();
					showAddGameObject = false;
				}
				ImGui.SameLine();
				if (ImGui.Button("Cancel", new System.Numerics.Vector2(-1, 0)))
					showAddGameObject = false;
			}
			ImGui.End();
		}

		private static void DrawConfirmDestroyPopup()
		{
			if (!showConfirmDestroy || !destroyTarget)
			{
				showConfirmDestroy = false;
				return;
			}
			ImGui.SetNextWindowSize(new System.Numerics.Vector2(340, 120), ImGuiCond.Always);
			ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Always, new System.Numerics.Vector2(0.5f, 0.5f));
			if (ImGui.Begin("Confirm Destroy##popup", ref showConfirmDestroy, ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoResize))
			{
				ImGui.TextWrapped($"Destroy \"{destroyTarget.name}\" and all its children?");
				ImGui.Separator();
				ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.6f, 0.1f, 0.1f, 1f));
				if (ImGui.Button("Destroy", new System.Numerics.Vector2(150, 0)))
				{
					inspectTargets.RemoveAll(t => t.GO == destroyTarget);
					UnityEngine.Object.Destroy(destroyTarget);
					ForceRefresh();
					destroyTarget = null;
					showConfirmDestroy = false;
				}
				ImGui.PopStyleColor();
				ImGui.SameLine();
				if (ImGui.Button("Cancel", new System.Numerics.Vector2(-1, 0)))
				{
					destroyTarget = null;
					showConfirmDestroy = false;
				}
			}
			ImGui.End();
		}


		private static string[] _cachedLayerNames;
		private static string[] GetLayerNames()
		{
			if (_cachedLayerNames != null) return _cachedLayerNames;
			var n = new string[32];
			for (int i = 0; i < 32; i++)
				n[i] = LayerMask.LayerToName(i);
			return _cachedLayerNames = n;
		}

		private static IEnumerable<Type> AllTypes() => AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => {try { return a.GetTypes(); } catch { return []; } });

		private static GameObject[] GetDontDestroyOnLoadObjects() =>
			[.. Resources.FindObjectsOfTypeAll<GameObject>().Where(x => x.hideFlags == HideFlags.None && x.transform.parent == null
			&& x.scene.name == "DontDestroyOnLoad"
			&& x != Bridge.go && !x.name.Contains("Lunaris"))];

		private static GameObject[] GetHideAndDontSaveObjects() =>
			[.. Resources.FindObjectsOfTypeAll<GameObject>().Where(x => (x.hideFlags & HideFlags.HideAndDontSave) != 0
			&& x.transform.parent == null
			&& x != Bridge.go && !x.name.Contains("Lunaris"))];

		private static void ScanStaticClasses() => staticResults = [.. AllTypes().Where(t => t.IsSealed && t.IsAbstract && !t.IsGenericType).OrderBy(t => t.FullName)];
	}
}