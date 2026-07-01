#include "updater.h"
#include <cstring>
#include <cstdint>
#include <algorithm>
#include <cstdlib>
#include <cctype>

#ifdef _WIN32
#define SECURITY_WIN32
#include <security.h>
#include <schannel.h>
#pragma comment(lib, "secur32.lib")
#pragma comment(lib, "ws2_32.lib")
#endif

#ifndef _WIN32
#include <sys/stat.h>
#include <unistd.h>
#endif

// URL of the plain-text version file, format:
//   1.2.3
//   https://example.com/Lunaris.dll
//   https://example.com/Lunaris.Boot.dll
//   <sha256 hex of Lunaris.dll>
//   <sha256 hex of Lunaris.Boot.dll>
#define LUNARIS_VERSION_URL "https://raw.githubusercontent.com/MizukiBelhi/LunarisUpdate/main/lunaris_update.txt"


// Portable SHA-256 (public domain, Brad Conte)

static const uint32_t SHA256_K[64] = {
	0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,
	0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
	0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
	0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
	0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,
	0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
	0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,
	0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2
};

#define ROTR(x,n) (((x)>>(n))|((x)<<(32-(n))))
#define CH(x,y,z)  (((x)&(y))^(~(x)&(z)))
#define MAJ(x,y,z) (((x)&(y))^((x)&(z))^((y)&(z)))
#define EP0(x) (ROTR(x,2) ^ROTR(x,13)^ROTR(x,22))
#define EP1(x) (ROTR(x,6) ^ROTR(x,11)^ROTR(x,25))
#define SIG0(x)(ROTR(x,7) ^ROTR(x,18)^((x)>>3))
#define SIG1(x)(ROTR(x,17)^ROTR(x,19)^((x)>>10))

struct Sha256Ctx {
	uint8_t  data[64];
	uint32_t datalen;
	uint64_t bitlen;
	uint32_t state[8];
};

static void sha256_transform(Sha256Ctx& ctx, const uint8_t* data) {
	uint32_t a, b, c, d, e, f, g, h, t1, t2, m[64];
	for (int i = 0, j = 0; i < 16; ++i, j += 4)
		m[i] = ((uint32_t)data[j] << 24) | ((uint32_t)data[j + 1] << 16) | ((uint32_t)data[j + 2] << 8) | data[j + 3];
	for (int i = 16; i < 64; ++i)
		m[i] = SIG1(m[i - 2]) + m[i - 7] + SIG0(m[i - 15]) + m[i - 16];
	a = ctx.state[0];b = ctx.state[1];c = ctx.state[2];d = ctx.state[3];
	e = ctx.state[4];f = ctx.state[5];g = ctx.state[6];h = ctx.state[7];
	for (int i = 0; i < 64; ++i) {
		t1 = h + EP1(e) + CH(e, f, g) + SHA256_K[i] + m[i];
		t2 = EP0(a) + MAJ(a, b, c);
		h = g;g = f;f = e;e = d + t1;d = c;c = b;b = a;a = t1 + t2;
	}
	ctx.state[0] += a;ctx.state[1] += b;ctx.state[2] += c;ctx.state[3] += d;
	ctx.state[4] += e;ctx.state[5] += f;ctx.state[6] += g;ctx.state[7] += h;
}

static void sha256_init(Sha256Ctx& ctx) {
	ctx.datalen = 0; ctx.bitlen = 0;
	ctx.state[0] = 0x6a09e667; ctx.state[1] = 0xbb67ae85;
	ctx.state[2] = 0x3c6ef372; ctx.state[3] = 0xa54ff53a;
	ctx.state[4] = 0x510e527f; ctx.state[5] = 0x9b05688c;
	ctx.state[6] = 0x1f83d9ab; ctx.state[7] = 0x5be0cd19;
}

static void sha256_update(Sha256Ctx& ctx, const uint8_t* data, size_t len) {
	for (size_t i = 0; i < len; ++i) {
		ctx.data[ctx.datalen++] = data[i];
		if (ctx.datalen == 64) { sha256_transform(ctx, ctx.data); ctx.bitlen += 512; ctx.datalen = 0; }
	}
}

static void sha256_final(Sha256Ctx& ctx, uint8_t hash[32]) {
	uint32_t i = ctx.datalen;
	ctx.data[i++] = 0x80;
	if (ctx.datalen < 56) { while (i < 56) ctx.data[i++] = 0; }
	else { while (i < 64) ctx.data[i++] = 0; sha256_transform(ctx, ctx.data); memset(ctx.data, 0, 56); }
	ctx.bitlen += ctx.datalen * 8;
	ctx.data[63] = (uint8_t)ctx.bitlen;       ctx.data[62] = (uint8_t)(ctx.bitlen >> 8);  ctx.data[61] = (uint8_t)(ctx.bitlen >> 16);
	ctx.data[60] = (uint8_t)(ctx.bitlen >> 24);ctx.data[59] = (uint8_t)(ctx.bitlen >> 32); ctx.data[58] = (uint8_t)(ctx.bitlen >> 40);
	ctx.data[57] = (uint8_t)(ctx.bitlen >> 48);ctx.data[56] = (uint8_t)(ctx.bitlen >> 56);
	sha256_transform(ctx, ctx.data);
	for (i = 0; i < 4; ++i) {
		hash[i] = (uint8_t)((ctx.state[0] >> (24 - i * 8)) & 0xff);
		hash[i + 4] = (uint8_t)((ctx.state[1] >> (24 - i * 8)) & 0xff);
		hash[i + 8] = (uint8_t)((ctx.state[2] >> (24 - i * 8)) & 0xff);
		hash[i + 12] = (uint8_t)((ctx.state[3] >> (24 - i * 8)) & 0xff);
		hash[i + 16] = (uint8_t)((ctx.state[4] >> (24 - i * 8)) & 0xff);
		hash[i + 20] = (uint8_t)((ctx.state[5] >> (24 - i * 8)) & 0xff);
		hash[i + 24] = (uint8_t)((ctx.state[6] >> (24 - i * 8)) & 0xff);
		hash[i + 28] = (uint8_t)((ctx.state[7] >> (24 - i * 8)) & 0xff);
	}
}

static std::string Sha256File(const std::string& path) {
	Sha256Ctx ctx;
	sha256_init(ctx);
	uint8_t buf[8192];

#ifdef _WIN32
	HANDLE hFile = CreateFileA(path.c_str(), GENERIC_READ, FILE_SHARE_READ,
		nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
	if (hFile == INVALID_HANDLE_VALUE) return "";
	DWORD read = 0;
	while (ReadFile(hFile, buf, sizeof(buf), &read, nullptr) && read > 0)
		sha256_update(ctx, buf, (size_t)read);
	CloseHandle(hFile);
#else
	FILE* f = fopen(path.c_str(), "rb");
	if (!f) return "";
	size_t read = 0;
	while ((read = fread(buf, 1, sizeof(buf), f)) > 0)
		sha256_update(ctx, buf, read);
	fclose(f);
#endif

	uint8_t hash[32];
	sha256_final(ctx, hash);

	char hex[65];
	for (int i = 0; i < 32; ++i)
		snprintf(hex + i * 2, 3, "%02x", hash[i]);
	hex[64] = '\0';
	return std::string(hex);
}

static bool FileExists(const std::string& path) {
#ifdef _WIN32
	DWORD attr = GetFileAttributesA(path.c_str());
	return attr != INVALID_FILE_ATTRIBUTES && !(attr & FILE_ATTRIBUTE_DIRECTORY);
#else
	struct stat st = {};
	return stat(path.c_str(), &st) == 0 && S_ISREG(st.st_mode);
#endif
}


static bool IsValidHttpsUrl(const std::string& url) {
	if (url.size() < 9 || url.size() > 2048) return false;
	if (url.compare(0, 8, "https://") != 0) return false;
	for (unsigned char c : url) {
		if (isalnum(c)) continue;
		if (strchr("-._~:/?#@%=+,", (char)c) != nullptr) continue;
		return false;
	}
	return true;
}

static bool IsValidSha256Hex(const std::string& s) {
	if (s.size() != 64) return false;
	for (unsigned char c : s)
		if (!isxdigit(c)) return false;
	return true;
}

struct LunarisVersion {
	int major = 0, minor = 0, patch = 0;
	bool operator>(const LunarisVersion& o) const {
		if (major != o.major) return major > o.major;
		if (minor != o.minor) return minor > o.minor;
		return patch > o.patch;
	}
};

static LunarisVersion ParseVersion(const std::string& s) {
	LunarisVersion v;
	sscanf_s(s.c_str(), "%d.%d.%d", &v.major, &v.minor, &v.patch);
	return v;
}

static LunarisVersion GetStoredVersion() {
	LunarisVersion v;
#ifdef _WIN32
	HKEY hKey;
	if (RegOpenKeyExW(HKEY_CURRENT_USER, L"Software\\Lunaris", 0, KEY_READ, &hKey) == ERROR_SUCCESS) {
		char buf[32] = {};
		DWORD size = sizeof(buf);
		if (RegQueryValueExA(hKey, "LunarisVersion", nullptr, nullptr, (BYTE*)buf, &size) == ERROR_SUCCESS)
			v = ParseVersion(buf);
		RegCloseKey(hKey);
	}
#else
	const char* home = getenv("HOME");
	if (home) {
		std::string path = std::string(home) + "/.config/lunaris/version";
		FILE* f = fopen(path.c_str(), "r");
		if (f) {
			char buf[32] = {};
			if (fgets(buf, sizeof(buf), f)) v = ParseVersion(buf);
			fclose(f);
		}
	}
#endif
	return v;
}

static void SetStoredVersion(const LunarisVersion& v) {
	std::string ver = std::to_string(v.major) + "." + std::to_string(v.minor) + "." + std::to_string(v.patch);
#ifdef _WIN32
	HKEY hKey;
	if (RegCreateKeyExW(HKEY_CURRENT_USER, L"Software\\Lunaris", 0, nullptr, 0,
		KEY_WRITE, nullptr, &hKey, nullptr) == ERROR_SUCCESS) {
		RegSetValueExA(hKey, "LunarisVersion", 0, REG_SZ,
			(BYTE*)ver.c_str(), (DWORD)ver.size() + 1);
		RegCloseKey(hKey);
	}
#else
	const char* home = getenv("HOME");
	if (home) {
		std::string dir = std::string(home) + "/.config/lunaris";
		std::string path = dir + "/version";
		mkdir(dir.c_str(), 0755);
		FILE* f = fopen(path.c_str(), "w");
		if (f) { fputs(ver.c_str(), f); fclose(f); }
	}
#endif
}

static std::string GetDllFolder() {
#ifdef _WIN32
	char dllPath[MAX_PATH];
	HMODULE hModule = nullptr;
	if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCSTR)&GetDllFolder, &hModule)) return "";
	if (!GetModuleFileNameA(hModule, dllPath, MAX_PATH)) return "";
	std::string path = dllPath;
	return path.substr(0, path.find_last_of("\\/")) + "\\";
#else
	FILE* f = fopen("/proc/self/maps", "r");
	if (!f) return "";
	char line[512];
	std::string result;
	void* selfAddr = (void*)&GetDllFolder;
	while (fgets(line, sizeof(line), f)) {
		unsigned long start, end;
		char perm[5], path[256] = {};
		if (sscanf(line, "%lx-%lx %4s %*s %*s %*s %255s", &start, &end, perm, path) >= 3) {
			if ((void*)start <= selfAddr && selfAddr < (void*)end && path[0]) {
				result = path;
				break;
			}
		}
	}
	fclose(f);
	if (result.empty()) return "";
	size_t slash = result.find_last_of('/');
	return (slash != std::string::npos) ? result.substr(0, slash + 1) : "./";
#endif
}

#ifdef _WIN32

struct ParsedHttpsUrl {
	std::string host;
	std::string path;
	unsigned short port = 443;
};

struct TlsConnection {
	SOCKET sock = INVALID_SOCKET;
	CredHandle cred = {};
	CtxtHandle ctx = {};
	bool haveCred = false;
	bool haveCtx = false;
	SecPkgContext_StreamSizes sizes = {};
	std::vector<char> encrypted;
};

static std::string ToLowerAscii(std::string s) {
	std::transform(s.begin(), s.end(), s.begin(), [](unsigned char c) {
		return (char)tolower(c);
	});
	return s;
}

static bool ParseHttpsUrl(const std::string& url, ParsedHttpsUrl& parsed) {
	const std::string prefix = "https://";
	if (url.compare(0, prefix.size(), prefix) != 0)
		return false;

	size_t authorityStart = prefix.size();
	size_t pathStart = url.find('/', authorityStart);
	std::string authority = pathStart == std::string::npos
		? url.substr(authorityStart)
		: url.substr(authorityStart, pathStart - authorityStart);
	parsed.path = pathStart == std::string::npos ? "/" : url.substr(pathStart);

	size_t colon = authority.rfind(':');
	if (colon != std::string::npos) {
		parsed.host = authority.substr(0, colon);
		int port = atoi(authority.substr(colon + 1).c_str());
		if (port <= 0 || port > 65535)
			return false;
		parsed.port = (unsigned short)port;
	}
	else {
		parsed.host = authority;
		parsed.port = 443;
	}

	return !parsed.host.empty() && !parsed.path.empty();
}

static bool EnsureWinsock() {
	static bool started = false;
	if (started)
		return true;

	WSADATA wsa = {};
	if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
		DebugLog(("DownloadFile: WSAStartup failed: " + std::to_string(WSAGetLastError())).c_str());
		return false;
	}
	started = true;
	return true;
}

static int ReceiveEncrypted(TlsConnection& tls) {
	char recvBuf[8192];
	int got = recv(tls.sock, recvBuf, sizeof(recvBuf), 0);
	if (got > 0) tls.encrypted.insert(tls.encrypted.end(), recvBuf, recvBuf + got);
	return got;
}

static SOCKET ConnectTcp(const std::string& host, unsigned short port) {
	if (!EnsureWinsock())
		return INVALID_SOCKET;

	hostent* entry = gethostbyname(host.c_str());
	if (!entry) {
		DebugLog(("DownloadFile: DNS lookup failed: " + std::to_string(WSAGetLastError())).c_str());
		return INVALID_SOCKET;
	}

	for (char** addr = entry->h_addr_list; addr && *addr; ++addr) {
		SOCKET sock = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
		if (sock == INVALID_SOCKET)
			continue;

		sockaddr_in target = {};
		target.sin_family = AF_INET;
		target.sin_port = htons(port);
		memcpy(&target.sin_addr, *addr, entry->h_length);

		if (connect(sock, (sockaddr*)&target, sizeof(target)) == 0) return sock;

		closesocket(sock);
	}

	DebugLog(("DownloadFile: TCP connect failed: " + std::to_string(WSAGetLastError())).c_str());
	return INVALID_SOCKET;
}

static bool SendAll(SOCKET sock, const void* data, int len) {
	const char* p = (const char*)data;
	while (len > 0) {
		int sent = send(sock, p, len, 0);
		if (sent <= 0)
			return false;
		p += sent;
		len -= sent;
	}
	return true;
}

static bool SendSecurityBuffer(SOCKET sock, SecBuffer& buffer) {
	if (buffer.cbBuffer == 0 || !buffer.pvBuffer)
		return true;

	bool ok = SendAll(sock, buffer.pvBuffer, (int)buffer.cbBuffer);
	FreeContextBuffer(buffer.pvBuffer);
	buffer.pvBuffer = nullptr;
	buffer.cbBuffer = 0;
	return ok;
}

static bool KeepHandshakeExtra(const SecBuffer& buffer, std::vector<char>& encrypted) {
	if (buffer.BufferType != SECBUFFER_EXTRA) {
		encrypted.clear();
		return true;
	}

	if (buffer.cbBuffer > encrypted.size()) {
		DebugLog("DownloadFile: TLS extra buffer size was invalid.");
		return false;
	}

	// InitializeSecurityContext reports handshake extras as bytes remaining at the tail.
	std::vector<char> extraBytes(encrypted.end() - buffer.cbBuffer, encrypted.end());
	encrypted.swap(extraBytes);
	return true;
}

static bool TlsHandshake(TlsConnection& tls, const std::string& host) {
	SCHANNEL_CRED cred = {};
	cred.dwVersion = SCHANNEL_CRED_VERSION;
	cred.dwFlags = SCH_USE_STRONG_CRYPTO;

	TimeStamp expiry = {};
	SECURITY_STATUS status = AcquireCredentialsHandleA(nullptr, const_cast<SEC_CHAR*>(UNISP_NAME_A),
		SECPKG_CRED_OUTBOUND, nullptr, &cred, nullptr, nullptr, &tls.cred, &expiry);
	if (status != SEC_E_OK) {
		DebugLog(("DownloadFile: AcquireCredentialsHandle failed: " + std::to_string((long)status)).c_str());
		return false;
	}
	tls.haveCred = true;

	DWORD flags = ISC_REQ_SEQUENCE_DETECT | ISC_REQ_REPLAY_DETECT | ISC_REQ_CONFIDENTIALITY |
		ISC_REQ_EXTENDED_ERROR | ISC_REQ_ALLOCATE_MEMORY | ISC_REQ_STREAM;
	DWORD attrs = 0;
	SecBuffer outBuffer = { 0, SECBUFFER_TOKEN, nullptr };
	SecBufferDesc outDesc = { SECBUFFER_VERSION, 1, &outBuffer };

	status = InitializeSecurityContextA(&tls.cred, nullptr, const_cast<SEC_CHAR*>(host.c_str()),
		flags, 0, SECURITY_NATIVE_DREP, nullptr, 0, &tls.ctx, &outDesc, &attrs, &expiry);
	if (status != SEC_E_OK && status != SEC_I_CONTINUE_NEEDED) {
		DebugLog(("DownloadFile: InitializeSecurityContext failed: " + std::to_string((long)status)).c_str());
		return false;
	}
	tls.haveCtx = true;

	if (!SendSecurityBuffer(tls.sock, outBuffer))
		return false;

	while (status == SEC_I_CONTINUE_NEEDED || status == SEC_E_INCOMPLETE_MESSAGE) {
		if (status == SEC_E_INCOMPLETE_MESSAGE || tls.encrypted.empty()) {
			int got = ReceiveEncrypted(tls);
			if (got <= 0) {
				DebugLog("DownloadFile: TLS handshake failed.");
				return false;
			}
		}

		SecBuffer inBuffers[2] = {};
		inBuffers[0].BufferType = SECBUFFER_TOKEN;
		inBuffers[0].pvBuffer = tls.encrypted.data();
		inBuffers[0].cbBuffer = (DWORD)tls.encrypted.size();
		inBuffers[1].BufferType = SECBUFFER_EMPTY;
		SecBufferDesc inDesc = { SECBUFFER_VERSION, 2, inBuffers };
		outBuffer = { 0, SECBUFFER_TOKEN, nullptr };
		outDesc = { SECBUFFER_VERSION, 1, &outBuffer };

		status = InitializeSecurityContextA(&tls.cred, &tls.ctx, const_cast<SEC_CHAR*>(host.c_str()),
			flags, 0, SECURITY_NATIVE_DREP, &inDesc, 0, &tls.ctx, &outDesc, &attrs, &expiry);
		if (!SendSecurityBuffer(tls.sock, outBuffer))
			return false;

		if (status == SEC_E_INCOMPLETE_MESSAGE)
			continue;
		if (status != SEC_E_OK && status != SEC_I_CONTINUE_NEEDED) {
			DebugLog(("DownloadFile: TLS handshake failed: " + std::to_string((long)status)).c_str());
			return false;
		}

		if (!KeepHandshakeExtra(inBuffers[1], tls.encrypted))
			return false;
	}

	status = QueryContextAttributesA(&tls.ctx, SECPKG_ATTR_STREAM_SIZES, &tls.sizes);
	if (status != SEC_E_OK) {
		DebugLog(("DownloadFile: QueryContextAttributes failed: " + std::to_string((long)status)).c_str());
		return false;
	}

	return true;
}

static bool TlsSend(TlsConnection& tls, const std::string& plain) {
	size_t offset = 0;
	while (offset < plain.size()) {
		DWORD chunk = (DWORD)std::min<size_t>(plain.size() - offset, tls.sizes.cbMaximumMessage);
		std::vector<char> packet(tls.sizes.cbHeader + chunk + tls.sizes.cbTrailer);
		memcpy(packet.data() + tls.sizes.cbHeader, plain.data() + offset, chunk);

		SecBuffer buffers[4] = {};
		buffers[0] = { tls.sizes.cbHeader, SECBUFFER_STREAM_HEADER, packet.data() };
		buffers[1] = { chunk, SECBUFFER_DATA, packet.data() + tls.sizes.cbHeader };
		buffers[2] = { tls.sizes.cbTrailer, SECBUFFER_STREAM_TRAILER, packet.data() + tls.sizes.cbHeader + chunk };
		buffers[3] = { 0, SECBUFFER_EMPTY, nullptr };
		SecBufferDesc desc = { SECBUFFER_VERSION, 4, buffers };

		SECURITY_STATUS status = EncryptMessage(&tls.ctx, 0, &desc, 0);
		if (status != SEC_E_OK) {
			DebugLog(("DownloadFile: EncryptMessage failed: " + std::to_string((long)status)).c_str());
			return false;
		}

		DWORD packetSize = buffers[0].cbBuffer + buffers[1].cbBuffer + buffers[2].cbBuffer;
		if (!SendAll(tls.sock, packet.data(), (int)packetSize)) {
			DebugLog(("DownloadFile: TLS send failed: " + std::to_string(WSAGetLastError())).c_str());
			return false;
		}
		offset += chunk;
	}
	return true;
}

static bool TlsReadAll(TlsConnection& tls, std::string& out) {
	for (;;) {
		if (tls.encrypted.empty()) {
			int got = ReceiveEncrypted(tls);
			if (got == 0) return true;
			if (got < 0) { DebugLog(("DownloadFile: TLS recv failed: " + std::to_string(WSAGetLastError())).c_str()); return false; }
		}

		SecBuffer buffers[4] = {};
		buffers[0] = { (DWORD)tls.encrypted.size(), SECBUFFER_DATA, tls.encrypted.data() };
		buffers[1] = { 0, SECBUFFER_EMPTY, nullptr };
		buffers[2] = { 0, SECBUFFER_EMPTY, nullptr };
		buffers[3] = { 0, SECBUFFER_EMPTY, nullptr };
		SecBufferDesc desc = { SECBUFFER_VERSION, 4, buffers };

		SECURITY_STATUS status = DecryptMessage(&tls.ctx, &desc, 0, nullptr);
		if (status == SEC_E_INCOMPLETE_MESSAGE) {
			int got = ReceiveEncrypted(tls);
			if (got <= 0) { DebugLog("DownloadFile: TLS read failed."); return false; }
			continue;
		}
		if (status == SEC_I_CONTEXT_EXPIRED) return true;
		if (status != SEC_E_OK) {
			DebugLog(("DownloadFile: DecryptMessage failed: " + std::to_string((long)status)).c_str());
			return false;
		}

		std::vector<char> extra;
		for (SecBuffer& buffer : buffers) {
			if (buffer.BufferType == SECBUFFER_DATA && buffer.pvBuffer && buffer.cbBuffer)
				out.append((char*)buffer.pvBuffer, buffer.cbBuffer);
			else if (buffer.BufferType == SECBUFFER_EXTRA && buffer.pvBuffer && buffer.cbBuffer) {
				char* p = (char*)buffer.pvBuffer;
				extra.assign(p, p + buffer.cbBuffer);
			}
		}

		tls.encrypted.swap(extra);
	}
}

static void CloseTls(TlsConnection& tls) {
	if (tls.haveCtx)
		DeleteSecurityContext(&tls.ctx);
	if (tls.haveCred)
		FreeCredentialsHandle(&tls.cred);
	if (tls.sock != INVALID_SOCKET)
		closesocket(tls.sock);
	tls = {};
}

static bool DecodeChunkedBody(const std::string& input, std::string& output) {
	size_t pos = 0;
	while (true) {
		size_t lineEnd = input.find("\r\n", pos);
		if (lineEnd == std::string::npos)
			return false;
		std::string line = input.substr(pos, lineEnd - pos);
		size_t semi = line.find(';');
		if (semi != std::string::npos)
			line.resize(semi);

		char* end = nullptr;
		unsigned long chunkSize = strtoul(line.c_str(), &end, 16);
		if (end == line.c_str())
			return false;

		pos = lineEnd + 2;
		if (chunkSize == 0)
			return true;
		if (pos + chunkSize + 2 > input.size())
			return false;

		output.append(input.data() + pos, chunkSize);
		pos += chunkSize;
		if (input.compare(pos, 2, "\r\n") != 0)
			return false;
		pos += 2;
	}
}

static bool WriteHttpBodyToFile(const std::string& response, const std::string& destPath) {
	size_t headerEnd = response.find("\r\n\r\n");
	if (headerEnd == std::string::npos) {
		DebugLog("DownloadFile: malformed HTTP response.");
		return false;
	}

	std::string headers = response.substr(0, headerEnd);
	std::string lowerHeaders = ToLowerAscii(headers);
	std::string body = response.substr(headerEnd + 4);

	size_t firstSpace = headers.find(' ');
	int statusCode = firstSpace == std::string::npos ? 0 : atoi(headers.c_str() + firstSpace + 1);
	if (statusCode < 200 || statusCode > 299) {
		DebugLog(("DownloadFile: unexpected HTTP status: " + std::to_string(statusCode)).c_str());
		return false;
	}

	if (lowerHeaders.find("transfer-encoding: chunked") != std::string::npos) {
		std::string decoded;
		if (!DecodeChunkedBody(body, decoded)) {
			DebugLog("DownloadFile: failed to decode chunked HTTP body.");
			return false;
		}
		body.swap(decoded);
	}

	DeleteFileA(destPath.c_str());
	HANDLE file = CreateFileA(destPath.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS,
		FILE_ATTRIBUTE_NORMAL, nullptr);
	if (file == INVALID_HANDLE_VALUE) {
		DebugLog(("DownloadFile: failed to create output file: " + std::to_string(GetLastError())).c_str());
		return false;
	}

	DWORD written = 0;
	bool ok = WriteFile(file, body.data(), (DWORD)body.size(), &written, nullptr) &&
		written == body.size();
	CloseHandle(file);

	if (!ok) {
		DebugLog(("DownloadFile: failed to write output file: " + std::to_string(GetLastError())).c_str());
		DeleteFileA(destPath.c_str());
	}
	return ok;
}

static bool DownloadFile(const std::string& url, const std::string& destPath) {
	ParsedHttpsUrl parsed;
	if (!ParseHttpsUrl(url, parsed)) {
		DebugLog("DownloadFile: invalid HTTPS URL.");
		return false;
	}

	TlsConnection tls;
	tls.sock = ConnectTcp(parsed.host, parsed.port);
	if (tls.sock == INVALID_SOCKET)
		return false;

	bool ok = false;
	std::string response;
	std::string request = "GET " + parsed.path + " HTTP/1.1\r\nHost: " + parsed.host + "\r\nUser-Agent: LunarisUpdater/1.0\r\nAccept: */*\r\nConnection: close\r\n\r\n";
	if (TlsHandshake(tls, parsed.host) &&
		TlsSend(tls, request) &&
		TlsReadAll(tls, response))
		ok = WriteHttpBodyToFile(response, destPath);
	CloseTls(tls);

	if (!ok)
		DeleteFileA(destPath.c_str());
	return ok && GetFileAttributesA(destPath.c_str()) != INVALID_FILE_ATTRIBUTES;
}

static std::string FetchText(const std::string& url) {
	char tempDir[MAX_PATH];
	if (!GetTempPathA(MAX_PATH, tempDir)) return "";
	char tempFile[MAX_PATH];
	if (!GetTempFileNameA(tempDir, "lnr", 0, tempFile)) return "";

	if (!DownloadFile(url, tempFile)) { DeleteFileA(tempFile); return ""; }

	HANDLE hFile = CreateFileA(tempFile, GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
	if (hFile == INVALID_HANDLE_VALUE) { DeleteFileA(tempFile); return ""; }

	std::string content;
	char buf[4096]; DWORD read = 0;
	while (ReadFile(hFile, buf, sizeof(buf) - 1, &read, nullptr) && read > 0) {
		buf[read] = '\0'; content += buf;
	}
	CloseHandle(hFile);
	DeleteFileA(tempFile);
	return content;
}

#else // Linux

static bool DownloadFile(const std::string& url, const std::string& destPath) {
	pid_t pid = fork();
	if (pid == 0) {
		execlp("curl", "curl", "-fsSL", "-o", destPath.c_str(), url.c_str(), (char*)nullptr);
		_exit(127); // curl not found
	}
	if (pid < 0) return false;
	int status = 0;
	waitpid(pid, &status, 0);
	if (WIFEXITED(status) && WEXITSTATUS(status) == 0) return true;

	DebugLog("curl failed, falling back to wget...");
	pid = fork();
	if (pid == 0) {
		execlp("wget", "wget", "-q", "-O", destPath.c_str(), url.c_str(), (char*)nullptr);
		_exit(127);
	}
	if (pid < 0) return false;
	waitpid(pid, &status, 0);
	return WIFEXITED(status) && WEXITSTATUS(status) == 0;
}

static std::string FetchText(const std::string& url) {
	char tempFile[] = "/tmp/lunaris_XXXXXX";
	int fd = mkstemp(tempFile);
	if (fd == -1) return "";
	close(fd);

	if (!DownloadFile(url, tempFile)) { unlink(tempFile); return ""; }

	FILE* f = fopen(tempFile, "r");
	if (!f) { unlink(tempFile); return ""; }
	std::string content;
	char buf[4096];
	while (fgets(buf, sizeof(buf), f)) content += buf;
	fclose(f);
	unlink(tempFile);
	return content;
}

#endif // _WIN32

void CheckAndUpdate() {
	DebugLog("Checking for Lunaris updates...");

	std::string body = FetchText(LUNARIS_VERSION_URL);
	if (body.empty()) {
		DebugLog("Update check failed: could not reach version server.");
		return;
	}

	std::istringstream ss(body);
	std::string versionStr, lunarisUrl, bootUrl, lunarisHash, bootHash;
	if (!std::getline(ss, versionStr) ||
		!std::getline(ss, lunarisUrl) ||
		!std::getline(ss, bootUrl) ||
		!std::getline(ss, lunarisHash) ||
		!std::getline(ss, bootHash)) {
		DebugLog("Update check failed: malformed version file.");
		return;
	}

	auto strip = [](std::string& s) { if (!s.empty() && s.back() == '\r') s.pop_back(); };
	strip(versionStr); strip(lunarisUrl); strip(bootUrl);
	strip(lunarisHash); strip(bootHash);

	if (!IsValidHttpsUrl(lunarisUrl) || !IsValidHttpsUrl(bootUrl)) {
		DebugLog("Update check failed: invalid URL in version file.");
		return;
	}

	if (!IsValidSha256Hex(lunarisHash) || !IsValidSha256Hex(bootHash)) {
		DebugLog("Update check failed: invalid hash in version file.");
		return;
	}

	LunarisVersion remote = ParseVersion(versionStr);
	LunarisVersion local = GetStoredVersion();

	std::string folder = GetDllFolder();
	if (folder.empty()) {
		DebugLog("Update check failed: could not determine DLL folder.");
		return;
	}

	std::string lunarisDest = folder + "Lunaris.dll";
	std::string bootDest = folder + "Lunaris.Boot.dll";

	bool shouldDownload = remote > local;
	if (!shouldDownload) {
		bool missingFile = !FileExists(lunarisDest) || !FileExists(bootDest);
		if (missingFile) {
			DebugLog("Required Lunaris DLLs are missing. Downloading...");
			shouldDownload = true;
		}
	}

	if (!shouldDownload) {
		DebugLog("Lunaris is up to date.");
		return;
	}

	if (remote > local)
		DebugLog(("Update found: " + versionStr + ". Downloading...").c_str());

	bool ok1 = DownloadFile(lunarisUrl, lunarisDest);
	bool ok2 = DownloadFile(bootUrl, bootDest);

	if (!ok1 || !ok2) {
		DebugLog("Update failed: one or more files could not be downloaded.");
		return;
	}

	std::string actualLunarisHash = Sha256File(lunarisDest);
	std::string actualBootHash = Sha256File(bootDest);

	if (actualLunarisHash != lunarisHash || actualBootHash != bootHash) {
		DebugLog("Update failed: Hash mismatch! Files may be corrupted or tampered with.");
#ifdef _WIN32
		DeleteFileA(lunarisDest.c_str());
		DeleteFileA(bootDest.c_str());
#else
		unlink(lunarisDest.c_str());
		unlink(bootDest.c_str());
#endif
		return;
	}

	SetStoredVersion(remote);
	DebugLog(("Updated to " + versionStr).c_str());
}
