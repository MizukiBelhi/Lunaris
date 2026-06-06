
#include "dllmain.h"
#include "updater.h"

HANDLE _stdConHandle;

namespace fs = std::filesystem;



void CreateConsole() {
	HKEY hKey;
	DWORD openConsole = 0;
	if (RegOpenKeyExW(HKEY_CURRENT_USER, L"Software\\Lunaris", 0, KEY_READ, &hKey) == ERROR_SUCCESS)
	{
		DWORD dataSize = sizeof(openConsole);
		if (RegQueryValueExW(hKey, L"EnableConsole", nullptr, nullptr, reinterpret_cast<BYTE*>(&openConsole), &dataSize) != ERROR_SUCCESS)
			openConsole = 0;
		RegCloseKey(hKey);
	}

	if (!openConsole) return;


	if (AllocConsole()) {
		

		FILE* fp;
		freopen_s(&fp, "CONOUT$", "w", stdout);
		freopen_s(&fp, "CONOUT$", "w", stderr);
		freopen_s(&fp, "CONIN$", "r", stdin);

		SetConsoleTitleA("Lunaris - Console");

		std::ios::sync_with_stdio();

		_stdConHandle = GetStdHandle(STD_OUTPUT_HANDLE);
		SetStdHandle(STD_OUTPUT_HANDLE, _stdConHandle);
		SetStdHandle(STD_ERROR_HANDLE, _stdConHandle);

		DWORD dwMode = 0;
		GetConsoleMode(_stdConHandle, &dwMode);

		dwMode |= ENABLE_PROCESSED_OUTPUT | ENABLE_WRAP_AT_EOL_OUTPUT;
		SetConsoleMode(_stdConHandle, dwMode);

		dwMode |= 0x0004;
		if (!SetConsoleMode(_stdConHandle, dwMode)) {
			dwMode &= ~0x0004;
			SetConsoleMode(_stdConHandle, dwMode);
		}
	}
}


void ResetHotreloadCache() {
	const char* dir = ".hotreload_cache";

	fs::path cachePath(dir);

	if (fs::exists(cachePath)) {
		fs::remove_all(cachePath);
	}
	fs::create_directories(cachePath);

	SetFileAttributesA(dir, FILE_ATTRIBUTE_HIDDEN);
}

void BackupLogFile()
{
	char dllPath[MAX_PATH];
	HMODULE hModule = nullptr;
	if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
		GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
		(LPCSTR)&BackupLogFile, &hModule)) {
		return;
	}

	if (GetModuleFileNameA(hModule, dllPath, MAX_PATH)) {
		std::string folder = std::string(dllPath);
		folder = folder.substr(0, folder.find_last_of("\\/"));
		std::string logFile = folder + "\\lunaris.log";
		std::string backupFile = folder + "\\lunaris_backup.log";

		DeleteFileA(backupFile.c_str());

		MoveFileA(logFile.c_str(), backupFile.c_str());
	}
}



enum MonoImageOpenStatus {
	MONO_IMAGE_OK,
	MONO_IMAGE_ERROR_ERRNO,
	MONO_IMAGE_ERROR_BAD_FORMAT,
	MONO_IMAGE_ERROR_UNRECOGNIZED_FORMAT,
	MONO_IMAGE_ERROR_IMAGE_INVALID,
	MONO_IMAGE_ERROR_ASSEMBLY_BUILDER,
	MONO_IMAGE_ERROR_NOT_FOUND,
	MONO_IMAGE_ERROR_TOO_MANY_FILES,
	MONO_IMAGE_ERROR_IO,
	MONO_IMAGE_ERROR_AOT_DISABLED,
	MONO_IMAGE_ERROR_WRONG_API,
	MONO_IMAGE_ERROR_SIGNATURE_MISMATCH
};

static void WriteLog(const char* msg, const char* lineEnd, bool prefix) {
	char dllPath[MAX_PATH];
	HMODULE hModule = nullptr;
	if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS |
		GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
		(LPCSTR)&WriteLog, &hModule)) return;
	if (GetModuleFileNameA(hModule, dllPath, MAX_PATH)) {
		std::string folder = std::string(dllPath);
		folder = folder.substr(0, folder.find_last_of("\\/"));
		std::ofstream out(folder + "\\lunaris.log", std::ios::app);
		if (out) out << msg << lineEnd;
	}
	if (prefix) printf("[Lunaris] %s\n", msg);
	else printf("%s%s", msg, lineEnd);
	fflush(stdout);
}

#define STRINGIZE(x) #x
#define SHARP_STR(x) STRINGIZE(x)

extern "C" __declspec(dllexport) void DebugLog(const char* msg) { WriteLog(msg, "\n", true); }
extern "C" __declspec(dllexport) void DebugLogSimple(const char* msg) { WriteLog(msg, "\n", false); }
extern "C" __declspec(dllexport) void DebugLogSL(const char* msg) { WriteLog(msg, "", false); }

extern "C" __declspec(dllexport) HANDLE GetNativeConsoleHandle() {
	return _stdConHandle;
}

extern "C" __declspec(dllexport) HWND GetConsoleWindowHandle() {
	return GetConsoleWindow();
}

extern "C" __declspec(dllexport) char* GetAPIKey() {
	const char* secret = SHARP_STR(API_KEY_SECRET);
	size_t len = strlen(secret) + 1;
	char* buffer = (char*)CoTaskMemAlloc(len);

	if (buffer != nullptr)
		strcpy_s(buffer, len, secret);

	return buffer;
}

extern "C" __declspec(dllexport) long long GetProcessVramUsage() {
#if defined(_WIN32)
	IDXGIFactory4* pFactory;
	if (FAILED(CreateDXGIFactory1(__uuidof(IDXGIFactory4), (void**)&pFactory))) return -1;

	IDXGIAdapter3* pAdapter;
	if (SUCCEEDED(pFactory->EnumAdapters(0, (IDXGIAdapter**)&pAdapter))) {
		DXGI_QUERY_VIDEO_MEMORY_INFO memInfo;
		if (SUCCEEDED(pAdapter->QueryVideoMemoryInfo(0, DXGI_MEMORY_SEGMENT_GROUP_LOCAL, &memInfo))) {
			long long usage = memInfo.CurrentUsage;

			pAdapter->Release();
			pFactory->Release();
			return usage;
		}
		pAdapter->Release();
	}
	pFactory->Release();
#endif
	return 0;
}


void ClearHotreloadCache() {
	const char* dir = ".hotreload_cache";

	fs::path cachePath(dir);

	if (fs::exists(cachePath)) {
		fs::remove_all(cachePath);
	}

	DebugLog("Cleared Cache Folder.");
}

extern "C" __declspec(dllexport) void ClearCache()
{
	ClearHotreloadCache();
}

BOOL WINAPI ConsoleHandler(DWORD ctrlType) {
	switch (ctrlType) {
	case CTRL_CLOSE_EVENT:
	case CTRL_C_EVENT:
	case CTRL_BREAK_EVENT:
	{
		HMODULE check;
		while(GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, "cimgui.dll", &check))
		{
			FreeLibrary(check);
		}

		ClearHotreloadCache();
		return TRUE;
	}
	default:
		return FALSE;
	}
}

// --- mono function typedefs ---
typedef void* (*mono_jit_init_version_t)(const char*, const char*);
typedef void* (*mono_assembly_open_t)(const char*, void*);
typedef void* (*mono_assembly_open_full_t)(const char* filename, MonoImageOpenStatus* status, void*);
typedef void* (*mono_assembly_get_image_t)(void*);
typedef void* (*mono_class_from_name_t)(void*, const char*, const char*);
typedef void* (*mono_class_get_method_from_name_t)(void*, const char*, int);
typedef void* (*mono_runtime_invoke_t)(void*, void*, void**, void**);
typedef void* (*mono_thread_attach_t)(void*);
typedef void  (*mono_thread_detach_t)(void*);
typedef void* (*mono_object_to_string_t)(void*, void**);
typedef char* (*mono_string_to_utf8_t)(void*);
typedef void* (*mono_method_desc_new_t)(const char*, bool);
typedef void* (*mono_method_desc_search_in_image_t)(void*, void*);
typedef void  (*mono_method_desc_free_t)(void*);
typedef void* (*mono_method_signature_t)(void*);
typedef unsigned int (*mono_signature_get_param_count_t)(void*);
typedef void* (*mono_domain_get_t)();
typedef void* (*mono_domain_assembly_open_t)(void*, const char*);
typedef void (*mono_free_t)(void*);
typedef void* (*mono_install_assembly_search_hook_t)(void*, void*);
typedef void* (*mono_assembly_name_get_name_t)(void*);


// --- global function pointers ---
mono_jit_init_version_t mono_jit_init_version = nullptr;
mono_assembly_open_t mono_assembly_open = nullptr;
mono_assembly_open_full_t mono_assembly_open_full;
mono_assembly_get_image_t mono_assembly_get_image = nullptr;
mono_class_from_name_t mono_class_from_name = nullptr;
mono_class_get_method_from_name_t mono_class_get_method_from_name = nullptr;
mono_runtime_invoke_t mono_runtime_invoke = nullptr;
mono_thread_attach_t mono_thread_attach = nullptr;
mono_thread_detach_t mono_thread_detach = nullptr;
mono_object_to_string_t mono_object_to_string = nullptr;
mono_string_to_utf8_t mono_string_to_utf8 = nullptr;
mono_method_desc_new_t mono_method_desc_new = nullptr;
mono_method_desc_search_in_image_t mono_method_desc_search_in_image = nullptr;
mono_method_desc_free_t mono_method_desc_free = nullptr;
mono_method_signature_t mono_method_signature = nullptr;
mono_signature_get_param_count_t mono_signature_get_param_count = nullptr;
mono_domain_get_t mono_domain_get = nullptr;
mono_domain_assembly_open_t mono_domain_assembly_open = nullptr;
mono_assembly_name_get_name_t mono_assembly_name_get_name;
mono_free_t mono_free;
mono_install_assembly_search_hook_t mono_install_assembly_search_hook;

mono_jit_init_version_t original_mono_jit_init_version = nullptr;

char g_patched_assembly_path[MAX_PATH] = { 0 };

static std::once_flag mono_initialized;

void* lunaCBMethod;

extern "C" __declspec(dllexport) void OnStartCB(const char* msg) {
	if (lunaCBMethod)
	{
		mono_runtime_invoke(lunaCBMethod, nullptr, nullptr, nullptr);
	}
}


void load_mono_funcs(HMODULE monoModule) {
	std::call_once(mono_initialized, [monoModule]()
	{
		mono_jit_init_version = (mono_jit_init_version_t)GetProcAddress(monoModule, "mono_jit_init_version");
		mono_assembly_open = (mono_assembly_open_t)GetProcAddress(monoModule, "mono_assembly_open");
		mono_assembly_get_image = (mono_assembly_get_image_t)GetProcAddress(monoModule, "mono_assembly_get_image");
		mono_class_from_name = (mono_class_from_name_t)GetProcAddress(monoModule, "mono_class_from_name");
		mono_class_get_method_from_name = (mono_class_get_method_from_name_t)GetProcAddress(monoModule, "mono_class_get_method_from_name");
		mono_runtime_invoke = (mono_runtime_invoke_t)GetProcAddress(monoModule, "mono_runtime_invoke");
		mono_thread_attach = (mono_thread_attach_t)GetProcAddress(monoModule, "mono_thread_attach");
		mono_thread_detach = (mono_thread_detach_t)GetProcAddress(monoModule, "mono_thread_detach");
		mono_object_to_string = (mono_object_to_string_t)GetProcAddress(monoModule, "mono_object_to_string");
		mono_string_to_utf8 = (mono_string_to_utf8_t)GetProcAddress(monoModule, "mono_string_to_utf8");
		mono_method_desc_new = (mono_method_desc_new_t)GetProcAddress(monoModule, "mono_method_desc_new");
		mono_method_desc_search_in_image = (mono_method_desc_search_in_image_t)GetProcAddress(monoModule, "mono_method_desc_search_in_image");
		mono_method_desc_free = (mono_method_desc_free_t)GetProcAddress(monoModule, "mono_method_desc_free");
		mono_method_signature = (mono_method_signature_t)GetProcAddress(monoModule, "mono_method_signature");
		mono_signature_get_param_count = (mono_signature_get_param_count_t)GetProcAddress(monoModule, "mono_signature_get_param_count");
		mono_domain_get = (mono_domain_get_t)GetProcAddress(monoModule, "mono_domain_get");
		mono_domain_assembly_open = (mono_domain_assembly_open_t)GetProcAddress(monoModule, "mono_domain_assembly_open");
		mono_assembly_open_full = (mono_assembly_open_full_t)GetProcAddress(monoModule, "mono_assembly_open_full");
		mono_assembly_name_get_name = (mono_assembly_name_get_name_t)GetProcAddress(monoModule, "mono_assembly_name_get_name");
		mono_free = (mono_free_t)GetProcAddress(monoModule, "mono_free");
		mono_install_assembly_search_hook = (mono_install_assembly_search_hook_t)GetProcAddress(monoModule, "mono_install_assembly_search_hook");

		if (!mono_assembly_name_get_name)
			DebugLog("Failed to load mono_assembly_name_get_name");

		DebugLog("Mono functions loaded via detour");
	});
}


void* WINAPI MonoJitInitDetour(const char* domain_name, const char* runtime_version) {
	//char sysdir[MAX_PATH];
	//if (GetSystemDirectoryA(sysdir, MAX_PATH)) {
	//	std::string path = std::string(sysdir) + "\\winhttp.dll";
	//	HMODULE h = LoadLibraryExA(path.c_str(), NULL, LOAD_LIBRARY_SEARCH_SYSTEM32 | LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
	//	if (!h) {
	//		DebugLog("Warning: failed to LoadLibraryEx System32\\winhttp.dll");
	//	}
	//	/*else {
	//		DebugLog("Loaded System32\\winhttp.dll");
	//	}*/

	//	
	//}
	CheckAndUpdate();

	void* domain = original_mono_jit_init_version(domain_name, runtime_version);
	mono_thread_attach(domain);
	DebugLog("Mono domain initialized, running bootstrap...");


	//mono_thread_attach(mono_domain_get());
	void* assembly = mono_domain_assembly_open(domain, "Lunaris.Boot.dll");
	if (!assembly) {
		DebugLog("Failed to open Lunaris.dll");
		return domain;
	}

	void* image = mono_assembly_get_image(assembly);
	if (!image) {
		DebugLog("Failed to get assembly image");
		return domain;
	}

	void* klass = mono_class_from_name(image, "Lunaris", "Lunaris");
	if (!klass) {
		DebugLog("Failed to find class in assembly");
		return domain;
	}

	void* method = mono_class_get_method_from_name(klass, "Init", 0);
	if (!method) {
		DebugLog("Failed to find method in class");
		return domain;
	}

	lunaCBMethod = mono_class_get_method_from_name(klass, "OnStarted", 0);

	DebugLog("Invoking managed method...");
	mono_runtime_invoke(method, nullptr, nullptr, nullptr);
	DebugLog("Managed DLL loaded successfully!");

	return domain;
}


FARPROC WINAPI GetProcAddressDetour(HMODULE module, LPCSTR name) {
	if (!name) return ::GetProcAddress(module, name);

	if (strcmp(name, "mono_jit_init_version") == 0 ||
		strcmp(name, "mono_assembly_open") == 0 ||
		strcmp(name, "mono_assembly_get_image") == 0 ||
		strcmp(name, "mono_class_from_name") == 0 ||
		strcmp(name, "mono_class_get_method_from_name") == 0 ||
		strcmp(name, "mono_runtime_invoke") == 0 ||
		strcmp(name, "mono_thread_attach") == 0 ||
		strcmp(name, "mono_thread_detach") == 0 ||
		strcmp(name, "mono_object_to_string") == 0 ||
		strcmp(name, "mono_string_to_utf8") == 0 ||
		strcmp(name, "mono_method_desc_new") == 0 ||
		strcmp(name, "mono_method_desc_search_in_image") == 0 ||
		strcmp(name, "mono_method_desc_free") == 0 ||
		strcmp(name, "mono_method_signature") == 0 ||
		strcmp(name, "mono_signature_get_param_count") == 0 ||
		strcmp(name, "mono_domain_get") == 0 ||
		strcmp(name, "mono_domain_assembly_open") == 0 ||
		strcmp(name, "mono_assembly_open_full") == 0 ||
		strcmp(name, "mono_assembly_name_get_name") == 0 ||
		strcmp(name, "mono_free") == 0 || 
		strcmp(name, "mono_install_assembly_search_hook") == 0)
	{
		load_mono_funcs(module);
		if (strcmp(name, "mono_jit_init_version") == 0) {
			if (!original_mono_jit_init_version)
				original_mono_jit_init_version = (mono_jit_init_version_t)::GetProcAddress(module, name);
			return (FARPROC)MonoJitInitDetour;
		}
		if (strcmp(name, "mono_assembly_open") == 0) return (FARPROC)mono_assembly_open;
		if (strcmp(name, "mono_assembly_get_image") == 0) return (FARPROC)mono_assembly_get_image;
		if (strcmp(name, "mono_class_from_name") == 0) return (FARPROC)mono_class_from_name;
		if (strcmp(name, "mono_class_get_method_from_name") == 0) return (FARPROC)mono_class_get_method_from_name;
		if (strcmp(name, "mono_runtime_invoke") == 0) return (FARPROC)mono_runtime_invoke;
		if (strcmp(name, "mono_thread_attach") == 0) return (FARPROC)mono_thread_attach;
		if (strcmp(name, "mono_thread_detach") == 0) return (FARPROC)mono_thread_detach;
		if (strcmp(name, "mono_object_to_string") == 0) return (FARPROC)mono_object_to_string;
		if (strcmp(name, "mono_string_to_utf8") == 0) return (FARPROC)mono_string_to_utf8;
		if (strcmp(name, "mono_method_desc_new") == 0) return (FARPROC)mono_method_desc_new;
		if (strcmp(name, "mono_method_desc_search_in_image") == 0) return (FARPROC)mono_method_desc_search_in_image;
		if (strcmp(name, "mono_method_desc_free") == 0) return (FARPROC)mono_method_desc_free;
		if (strcmp(name, "mono_method_signature") == 0) return (FARPROC)mono_method_signature;
		if (strcmp(name, "mono_signature_get_param_count") == 0) return (FARPROC)mono_signature_get_param_count;
		if (strcmp(name, "mono_domain_get") == 0) return (FARPROC)mono_domain_get;
		if (strcmp(name, "mono_domain_assembly_open") == 0) return (FARPROC)mono_domain_assembly_open;
		if (strcmp(name, "mono_assembly_open_full") == 0) return (FARPROC)mono_assembly_open_full;
		if (strcmp(name, "mono_assembly_name_get_name") == 0) return (FARPROC)mono_assembly_name_get_name;
		if (strcmp(name, "mono_free") == 0) return (FARPROC)mono_free;
		if (strcmp(name, "mono_install_assembly_search_hook") == 0) return (FARPROC)mono_install_assembly_search_hook;

	}

	return ::GetProcAddress(module, name);
}



bool g_is_loading_patched_assembly = false;

void* CustomAssemblyLoadHook(void* aname, void* user_data) {

	const char* name = (const char*)mono_assembly_name_get_name(aname);
	if (!name) return nullptr;

	if (strcmp(name, "UnityEngine.CoreModule") == 0) {
		if (g_patched_assembly_path[0] == '\0') {
			DebugLog("Intercepted request for UnityEngine.CoreModule, but patched path is not set.");
			return nullptr;
		}
		if (g_is_loading_patched_assembly)
			return nullptr;

		char fullPath[MAX_PATH];
		if (!_fullpath(fullPath, g_patched_assembly_path, MAX_PATH)) {
			DebugLog("ERROR: Failed to normalize path");
			return nullptr;
		}
		/*std::string normalizedPath = fullPath;
		DebugLog("Attempting to load from normalized path: ");
		DebugLog(normalizedPath.c_str());*/

		DWORD fileAttributes = GetFileAttributesA(g_patched_assembly_path);
		if (fileAttributes == INVALID_FILE_ATTRIBUTES) {
			DebugLog("ERROR: Patched DLL does not exist at path: ");
			DebugLog(g_patched_assembly_path);
		}

		g_is_loading_patched_assembly = true;

		MonoImageOpenStatus status;
		void* patched_assembly = mono_assembly_open_full(g_patched_assembly_path, &status, 0);
		g_is_loading_patched_assembly = false;
		if (patched_assembly) {
			//DebugLog("Successfully loaded patched UnityEngine.CoreModule.dll!");
			return patched_assembly;
		}
		else {
			switch (status) {
			case MONO_IMAGE_ERROR_BAD_FORMAT:
				DebugLog("ERROR: Failed to load DLL. Reason: Bad image format.");
				break;
			case MONO_IMAGE_ERROR_UNRECOGNIZED_FORMAT:
				DebugLog("ERROR: Failed to load DLL. Reason: Unrecognized format.");
				break;
			case MONO_IMAGE_ERROR_IMAGE_INVALID:
				DebugLog("ERROR: Failed to load DLL. Reason: Image invalid.");
				break;
			case MONO_IMAGE_ERROR_SIGNATURE_MISMATCH:
				DebugLog("ERROR: Failed to load DLL. Reason: Strong name/signature mismatch.");
				break;
			case MONO_IMAGE_ERROR_IO:
				DebugLog("ERROR: Failed to load DLL. Reason: IO error.");
				break;
			default:
				DebugLog(std::to_string(GetLastError()).c_str());
				break;
			}
		}
	}

	return nullptr;
}

PIMAGE_IMPORT_DESCRIPTOR GetImportDescriptor(HMODULE module, const char* importModuleName) {
	if (!module) return nullptr;

	BYTE* base = (BYTE*)module;
	IMAGE_DOS_HEADER* dos = (IMAGE_DOS_HEADER*)base;
	IMAGE_NT_HEADERS* nt = (IMAGE_NT_HEADERS*)(base + dos->e_lfanew);

	IMAGE_DATA_DIRECTORY dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
	if (!dir.VirtualAddress) return nullptr;

	PIMAGE_IMPORT_DESCRIPTOR desc = (PIMAGE_IMPORT_DESCRIPTOR)(base + dir.VirtualAddress);

	// iterate until the end
	for (; desc->Name; desc++) {
		const char* name = (const char*)(base + desc->Name);
		if (_stricmp(name, importModuleName) == 0) return desc;
	}
	return nullptr;
}

bool HookIAT(HMODULE targetModule, const char* importModule, const char* importName, FARPROC detour) {
	if (!targetModule || !importModule || !importName || !detour) return false;

	PIMAGE_IMPORT_DESCRIPTOR importDesc = GetImportDescriptor(targetModule, importModule);
	if (!importDesc) return false;

	for (; importDesc->Name; importDesc++) {
		const char* modName = (const char*)((BYTE*)targetModule + importDesc->Name);
		if (_stricmp(modName, importModule) != 0) continue;

		PIMAGE_THUNK_DATA thunk = (PIMAGE_THUNK_DATA)((BYTE*)targetModule + importDesc->FirstThunk);
		PIMAGE_THUNK_DATA origThunk = (PIMAGE_THUNK_DATA)((BYTE*)targetModule + importDesc->OriginalFirstThunk);

		for (; thunk->u1.Function; thunk++, origThunk++) {
			IMAGE_IMPORT_BY_NAME* import = (IMAGE_IMPORT_BY_NAME*)((BYTE*)targetModule + origThunk->u1.AddressOfData);
			if (!import) continue;
			if (_stricmp((char*)import->Name, importName) == 0) {
				DWORD oldProtect;
				VirtualProtect(&thunk->u1.Function, sizeof(FARPROC), PAGE_EXECUTE_READWRITE, &oldProtect);
				thunk->u1.Function = (ULONG_PTR)detour;
				VirtualProtect(&thunk->u1.Function, sizeof(FARPROC), oldProtect, &oldProtect);
				return true;
			}
		}
	}
	return false;
}

extern "C" __declspec(dllexport) void SetPatchedPath(const char* mono_path) {
	//DebugLog("Received patched path from managed code");
	//DebugLog((const char*)mono_path);
	//char* path_utf8 = (char*)mono_string_to_utf8(mono_path);
	strncpy_s(g_patched_assembly_path, MAX_PATH, mono_path, _TRUNCATE);
	//mono_free(path_utf8);
	mono_install_assembly_search_hook(CustomAssemblyLoadHook, nullptr);
}



bool inject() {
	HMODULE targetModule = GetModuleHandleA("UnityPlayer.dll");
	HMODULE appModule = GetModuleHandle(NULL); // fallback to exe

	if (!targetModule) {
		DebugLog("No UnityPlayer module found! Using executable as target.");
		targetModule = appModule;
	}

	if (!targetModule) {
		DebugLog("No module found to hook GetProcAddress");
		return false;
	}

	FARPROC realGetProc = GetProcAddress(GetModuleHandleA("kernel32.dll"), "GetProcAddress");
	if (!realGetProc) {
		DebugLog("Failed to get real GetProcAddress");
		return false;
	}

	if (!HookIAT(targetModule, "kernel32.dll", "GetProcAddress", (FARPROC)GetProcAddressDetour)) {
		DebugLog("Failed to hook IAT for GetProcAddress");
		return false;
	}

	DebugLog("IAT hook installed for GetProcAddress");
	return true;
}



HMODULE WaitForModule(const char* name, DWORD timeoutMs = 5000, DWORD pollIntervalMs = 50) {
	DWORD waited = 0;
	HMODULE mod = nullptr;
	while (waited < timeoutMs) {
		mod = GetModuleHandleA(name);
		if (mod) return mod;
		Sleep(pollIntervalMs);
		waited += pollIntervalMs;
	}
	return nullptr;
}

void WaitForExit() {
	DebugLog("Press ENTER to exit...");
	getchar();
}


DWORD WINAPI BootstrapThread(LPVOID lp) {
	DebugLog("BootstrapThread start");

	try {
		//std::atexit(ClearHotreloadCache);


		ResetHotreloadCache();
	}
	catch (const std::exception& e)
	{
		DebugLog(e.what());
	}
	catch (...) {
		DebugLog("wtf");
	}

	

	HMODULE monoModule = WaitForModule("UnityPlayer.dll");
	if (!monoModule) {
		DebugLog("Error: mono module not found after waiting");
		return 0;
	}
	/*else {
		DebugLog("Found Module.");
	}*/

	if (!inject()) {
		WaitForExit();
		return 0;
	}

	return 0;
}



BOOL WINAPI DllMain(HINSTANCE hinst, DWORD reason, LPVOID reserved) {
	if (reason == DLL_PROCESS_ATTACH) {
		BackupLogFile();

		CreateConsole();
		DisableThreadLibraryCalls(hinst);
		
		SetConsoleCtrlHandler(ConsoleHandler, TRUE);
		HANDLE h = CreateThread(nullptr, 0, BootstrapThread, nullptr, 0, nullptr);
		if (h) CloseHandle(h);
	}
	if (reason == DLL_PROCESS_DETACH)
	{
		DebugLog("Starting Cleanup.");
		ClearHotreloadCache();
	}
	return TRUE;
}
