#pragma once

#include <vector>
#include <string>
#include <fstream>
#include <stdio.h>
#include <direct.h>

#if defined(_WIN32)
#include <windows.h>
#include <dxgi1_4.h>
#include <filesystem>
#pragma comment(lib, "dxgi.lib")
#elif defined(__linux__) || defined(__APPLE__)
#include <dlfcn.h>
#include <unistd.h>
#include <fcntl.h>
#include <sys/mman.h>
#include <sys/stat.h>
#endif

// --- exported-function registration structure
struct ExportThunk {
	const char* name;
	void* tramp; // runtime trampoline to real function
};

static std::vector<ExportThunk> g_thunks;

extern "C" __declspec(dllexport) void DebugLog(const char* msg);

#define MAKE_FORWARDER_FN(NAME) \
extern "C" __declspec(dllexport) void NAME() { \
    for (auto &t : g_thunks) { if (_stricmp(t.name, #NAME) == 0 && t.tramp) { ((void(*)())t.tramp)(); return; } } \
    /* if missing, fail gracefully */ ; \
}