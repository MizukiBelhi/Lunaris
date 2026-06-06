#include <windows.h>
#include <string>
static HMODULE realWinHttp = nullptr;

typedef void* HINTERNET;
typedef unsigned short INTERNET_PORT;

static void ensureRealDll() {
  if (!realWinHttp) {
    char sys[MAX_PATH]; GetSystemDirectoryA(sys, MAX_PATH);
    std::string path = std::string(sys) + "\\winhttp.dll";
    realWinHttp = LoadLibraryA(path.c_str());
  }
}

template<typename T> static T GetFn(const char* name) {
	ensureRealDll();
	return (T)GetProcAddress(realWinHttp, name);
}

extern "C" {
	__declspec(dllexport) HINTERNET WINAPI WinHttpOpen(LPCWSTR pszAgentW, DWORD dwAccessType, LPCWSTR pszProxyW, LPCWSTR pszProxyBypassW, DWORD dwFlags)
	{
		static auto fn = GetFn<HINTERNET(WINAPI*)(LPCWSTR, DWORD, LPCWSTR, LPCWSTR, DWORD)>("WinHttpOpen");
		return fn ? fn(pszAgentW, dwAccessType, pszProxyW, pszProxyBypassW, dwFlags) : nullptr;
	}

	__declspec(dllexport) HINTERNET WINAPI WinHttpConnect(HINTERNET hSession, LPCWSTR pswzServerName, INTERNET_PORT nServerPort, DWORD dwReserved)
	{
		static auto fn = GetFn<HINTERNET(WINAPI*)(HINTERNET, LPCWSTR, INTERNET_PORT, DWORD)>("WinHttpConnect");
		return fn ? fn(hSession, pswzServerName, nServerPort, dwReserved) : nullptr;
	}

	__declspec(dllexport) HINTERNET WINAPI WinHttpOpenRequest(HINTERNET hConnect, LPCWSTR pwszVerb, LPCWSTR pwszObjectName, LPCWSTR pwszVersion,LPCWSTR pwszReferrer, LPCWSTR* ppwszAcceptTypes, DWORD dwFlags)
	{
		static auto fn = GetFn<HINTERNET(WINAPI*)(HINTERNET, LPCWSTR, LPCWSTR, LPCWSTR, LPCWSTR, LPCWSTR*, DWORD)>("WinHttpOpenRequest");
		return fn ? fn(hConnect, pwszVerb, pwszObjectName, pwszVersion, pwszReferrer, ppwszAcceptTypes, dwFlags) : nullptr;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpSendRequest(HINTERNET hRequest, LPCWSTR lpszHeaders, DWORD dwHeadersLength,LPVOID lpOptional, DWORD dwOptionalLength, DWORD dwTotalLength, DWORD_PTR dwContext)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, LPCWSTR, DWORD, LPVOID, DWORD, DWORD, DWORD_PTR)>("WinHttpSendRequest");
		return fn ? fn(hRequest, lpszHeaders, dwHeadersLength, lpOptional, dwOptionalLength, dwTotalLength, dwContext) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpReceiveResponse(HINTERNET hRequest, LPVOID lpReserved)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, LPVOID)>("WinHttpReceiveResponse");
		return fn ? fn(hRequest, lpReserved) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpReadData(HINTERNET hRequest, LPVOID lpBuffer, DWORD dwNumberOfBytesToRead, LPDWORD lpdwNumberOfBytesRead)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, LPVOID, DWORD, LPDWORD)>("WinHttpReadData");
		return fn ? fn(hRequest, lpBuffer, dwNumberOfBytesToRead, lpdwNumberOfBytesRead) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpWriteData(HINTERNET hRequest, LPCVOID lpBuffer, DWORD dwNumberOfBytesToWrite, LPDWORD lpdwNumberOfBytesWritten)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, LPCVOID, DWORD, LPDWORD)>("WinHttpWriteData");
		return fn ? fn(hRequest, lpBuffer, dwNumberOfBytesToWrite, lpdwNumberOfBytesWritten) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpCloseHandle(HINTERNET hInternet)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET)>("WinHttpCloseHandle");
		return fn ? fn(hInternet) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpAddRequestHeaders(HINTERNET hRequest, LPCWSTR lpszHeaders, DWORD dwHeadersLength, DWORD dwModifiers)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, LPCWSTR, DWORD, DWORD)>("WinHttpAddRequestHeaders");
		return fn ? fn(hRequest, lpszHeaders, dwHeadersLength, dwModifiers) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpAddRequestHeadersEx(HINTERNET hRequest, LPCWSTR pwszHeaders, ULONGLONG ullHeadersLength, ULONGLONG ullModifiers, DWORD cUnknownHeaders, void* pUnknownHeaders)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, LPCWSTR, ULONGLONG, ULONGLONG, DWORD, void*)>("WinHttpAddRequestHeadersEx");
		return fn ? fn(hRequest, pwszHeaders, ullHeadersLength, ullModifiers, cUnknownHeaders, pUnknownHeaders) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpCheckPlatform()
	{
		static auto fn = GetFn<BOOL(WINAPI*)()>("WinHttpCheckPlatform");
		return fn ? fn() : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpCrackUrl(LPCWSTR pwszUrl, DWORD dwUrlLength, DWORD dwFlags, void* lpUrlComponents)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(LPCWSTR, DWORD, DWORD, void*)>("WinHttpCrackUrl");
		return fn ? fn(pwszUrl, dwUrlLength, dwFlags, lpUrlComponents) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpCreateUrl(void* lpUrlComponents, DWORD dwFlags, LPWSTR pwszUrl, LPDWORD lpdwUrlLength)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(void*, DWORD, LPWSTR, LPDWORD)>("WinHttpCreateUrl");
		return fn ? fn(lpUrlComponents, dwFlags, pwszUrl, lpdwUrlLength) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpDetectAutoProxyConfigUrl(DWORD dwAutoDetectFlags, LPWSTR* ppwszAutoConfigUrl)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(DWORD, LPWSTR*)>("WinHttpDetectAutoProxyConfigUrl");
		return fn ? fn(dwAutoDetectFlags, ppwszAutoConfigUrl) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpSetOption(HINTERNET hInternet, DWORD dwOption, LPVOID lpBuffer, DWORD dwBufferLength)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, DWORD, LPVOID, DWORD)>("WinHttpSetOption");
		return fn ? fn(hInternet, dwOption, lpBuffer, dwBufferLength) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpSetCredentials(HINTERNET hRequest, DWORD AuthTargets, DWORD AuthScheme, LPCWSTR pwszUserName, LPCWSTR pwszPassword, LPVOID pAuthParams)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, DWORD, DWORD, LPCWSTR, LPCWSTR, LPVOID)>("WinHttpSetCredentials");
		return fn ? fn(hRequest, AuthTargets, AuthScheme, pwszUserName, pwszPassword, pAuthParams) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpSetDefaultProxyConfiguration(void* pProxyInfo)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(void*)>("WinHttpSetDefaultProxyConfiguration");
		return fn ? fn(pProxyInfo) : FALSE;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpResetAutoProxy(HINTERNET hSession, DWORD dwFlags)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, DWORD)>("WinHttpResetAutoProxy");
		return fn ? fn(hSession, dwFlags) : FALSE;
	}

	__declspec(dllexport) DWORD WINAPI WinHttpCreateProxyResolver(HINTERNET hSession, HINTERNET* phProxyResolver)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(HINTERNET, HINTERNET*)>("WinHttpCreateProxyResolver");
		return fn ? fn(hSession, phProxyResolver) : ERROR_NOT_SUPPORTED;
	}

	__declspec(dllexport) DWORD WINAPI WinHttpGetProxyForUrlEx2(HINTERNET hResolver, LPCWSTR pcwszUrl, void* pAutoProxyOptions, DWORD_PTR pContext)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(HINTERNET, LPCWSTR, void*, DWORD_PTR)>("WinHttpGetProxyForUrlEx2");
		return fn ? fn(hResolver, pcwszUrl, pAutoProxyOptions, pContext) : ERROR_NOT_SUPPORTED;
	}

	__declspec(dllexport) DWORD WINAPI WinHttpGetProxyResult(HINTERNET hResolver, void* pProxyResult)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(HINTERNET, void*)>("WinHttpGetProxyResult");
		return fn ? fn(hResolver, pProxyResult) : ERROR_NOT_SUPPORTED;
	}

	__declspec(dllexport) DWORD WINAPI WinHttpGetProxyResultEx(HINTERNET hResolver, void* pProxyResultEx)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(HINTERNET, void*)>("WinHttpGetProxyResultEx");
		return fn ? fn(hResolver, pProxyResultEx) : ERROR_NOT_SUPPORTED;
	}

	__declspec(dllexport) void WINAPI WinHttpFreeProxyResult(void* pProxyResult)
	{
		static auto fn = GetFn<void(WINAPI*)(void*)>("WinHttpFreeProxyResult");
		if (fn) fn(pProxyResult);
	}

	__declspec(dllexport) DWORD WINAPI WinHttpGetProxySettingsVersion(HINTERNET hSession, DWORD* pdwProxySettingsVersion)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(HINTERNET, DWORD*)>("WinHttpGetProxySettingsVersion");
		return fn ? fn(hSession, pdwProxySettingsVersion) : ERROR_NOT_SUPPORTED;
	}

	__declspec(dllexport) DWORD WINAPI WinHttpSetProxySettingsPerUser(BOOL fProxySettingsPerUser)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(BOOL)>("WinHttpSetProxySettingsPerUser");
		return fn ? fn(fProxySettingsPerUser) : ERROR_NOT_SUPPORTED;
	}

	__declspec(dllexport) BOOL WINAPI WinHttpSaveProxyCredentials(HINTERNET hSession, void* pProxyCredentials)
	{
		static auto fn = GetFn<BOOL(WINAPI*)(HINTERNET, void*)>("WinHttpSaveProxyCredentials");
		return fn ? fn(hSession, pProxyCredentials) : FALSE;
	}

	__declspec(dllexport) DWORD WINAPI WinHttpWebSocketSend(HINTERNET hWebSocket, DWORD eBufferType, void* pvBuffer, DWORD dwBufferLength)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(HINTERNET, DWORD, void*, DWORD)>("WinHttpWebSocketSend");
		return fn ? fn(hWebSocket, eBufferType, pvBuffer, dwBufferLength) : ERROR_NOT_SUPPORTED;
	}

	__declspec(dllexport) DWORD WINAPI WinHttpWebSocketReceive(HINTERNET hWebSocket, void* pvBuffer, DWORD dwBufferLength, DWORD* pdwBytesRead, DWORD* peBufferType)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(HINTERNET, void*, DWORD, DWORD*, DWORD*)>("WinHttpWebSocketReceive");
		return fn ? fn(hWebSocket, pvBuffer, dwBufferLength, pdwBytesRead, peBufferType) : ERROR_NOT_SUPPORTED;
	}

	__declspec(dllexport) DWORD WINAPI WinHttpWebSocketShutdown(HINTERNET hWebSocket, USHORT usStatus, void* pvReason, DWORD dwReasonLength)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(HINTERNET, USHORT, void*, DWORD)>("WinHttpWebSocketShutdown");
		return fn ? fn(hWebSocket, usStatus, pvReason, dwReasonLength) : ERROR_NOT_SUPPORTED;
	}

	__declspec(dllexport) DWORD WINAPI WinHttpWebSocketQueryCloseStatus(HINTERNET hWebSocket, USHORT* pusStatus, void* pvReason, DWORD dwReasonLength, DWORD* pdwReasonLengthConsumed)
	{
		static auto fn = GetFn<DWORD(WINAPI*)(HINTERNET, USHORT*, void*, DWORD, DWORD*)>("WinHttpWebSocketQueryCloseStatus");
		return fn ? fn(hWebSocket, pusStatus, pvReason, dwReasonLength, pdwReasonLengthConsumed) : ERROR_NOT_SUPPORTED;
	}

	typedef struct {
		BOOL  fAutoDetect;
		LPWSTR lpszAutoConfigUrl;
		LPWSTR lpszProxy;
		LPWSTR lpszProxyBypass;
	} WINHTTP_CURRENT_USER_IE_PROXY_CONFIG;


	__declspec(dllexport) BOOL WINAPI WinHttpGetIEProxyConfigForCurrentUser(WINHTTP_CURRENT_USER_IE_PROXY_CONFIG* pProxyConfig)
	{
		ensureRealDll();

		using fn_t = BOOL(WINAPI*)(WINHTTP_CURRENT_USER_IE_PROXY_CONFIG*);
		static fn_t fn = (fn_t)GetProcAddress(realWinHttp, "WinHttpGetIEProxyConfigForCurrentUser");

		if (!fn)
			return FALSE;

		return fn(pProxyConfig);
	}
}