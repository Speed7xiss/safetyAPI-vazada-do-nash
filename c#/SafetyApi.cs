using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Management;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class AuthResult
{
    public int StatusCode { get; set; }
    public string Message { get; set; }
    public string RequestId { get; set; }
    public string RawBody { get; set; }

    public bool Success => StatusCode >= 200 && StatusCode < 300;
}

public class LoginResult : AuthResult
{
    public string DiscordId { get; set; }
    public string LicenseKey { get; set; }

    public DateTime? ExpiresAt { get; set; }
    public int? DaysLeft { get; set; }

    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public string ProductHash { get; set; }

    public string ConfigVersion { get; set; }
    public string DownloadLink { get; set; }

    // 👇 agora pega do fetch da API
    public string DiscordUsername { get; set; }
    public string DiscordGlobalName { get; set; }
    public string DiscordAvatarUrl { get; set; }
    public string DiscordTag { get; set; }
}

public sealed class SafetyAPI : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private bool _initialized;
    private string _configuredProductHash;

    // controla logs no console
    public bool DebugEnabled { get; set; } = false;

    public SafetyAPI(string baseUrl = "https://safetyapi.squareweb.app/", int timeoutSeconds = 15, string productHash = null)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("baseUrl inválido.");

        _baseUrl = NormalizeBaseUrl(baseUrl);

        _http = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(Math.Max(3, timeoutSeconds))
        };

        _http.DefaultRequestHeaders.Accept.Clear();
        _http.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json")
        );

        if (!string.IsNullOrWhiteSpace(productHash))
        {
            if (!TrySetProductHash(productHash, out var err))
                throw new ArgumentException(err, nameof(productHash));
        }
    }

    public string GetConfiguredProductHash()
    {
        return _configuredProductHash;
    }

    public bool TrySetProductHash(string productHash, out string error)
    {
        var clean = (productHash ?? "").Trim().ToLowerInvariant();
        if (!IsHex64(clean))
        {
            error = "productHash inválido. Esperado SHA-256 em hex (64 chars).";
            return false;
        }

        _configuredProductHash = clean;
        error = null;
        return true;
    }

    public void ClearConfiguredProductHash()
    {
        _configuredProductHash = null;
    }

    private string ResolveProductHash(string productHash)
    {
        string effective = string.IsNullOrWhiteSpace(productHash)
            ? _configuredProductHash
            : productHash;

        if (string.IsNullOrWhiteSpace(effective))
            effective = Environment.GetEnvironmentVariable("SAFETY_PRODUCT_HASH");

        return (effective ?? "").Trim().ToLowerInvariant();
    }

    public void Dispose()
    {
        _http?.Dispose();
    }

    private void Log(string msg)
    {
        if (!DebugEnabled) return;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SafetyAPI] {msg}");
    }

    private void LogJson(string title, string json, int maxLen = 800)
    {
        if (!DebugEnabled) return;
        if (string.IsNullOrEmpty(json))
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SafetyAPI] {title}: <empty>");
            return;
        }

        string safe = json.Length > maxLen ? json.Substring(0, maxLen) + " ... (truncado)" : json;
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [SafetyAPI] {title}: {safe}");
    }

    public async Task<AuthResult> InitAsync()
    {
        Log("InitAsync() -> checando /v1/health...");
        var res = await HealthAsync();
        _initialized = res.Success;

        Log($"Init result: success={res.Success} status={res.StatusCode} requestId={res.RequestId} msg={res.Message}");
        return res;
    }

    public async Task<bool> CheckApiAvailabilityAsync()
    {
        var res = await HealthAsync();
        return res.Success;
    }

    public async Task<AuthResult> HealthAsync()
    {
        var r = await SafeGetAsync("/v1/health");
        if (r.Success) return r;

        var legacy = await SafeGetAsync("/status");
        return legacy;
    }

    public async Task<LoginResult> LoginAsync(
        string discordId,
        string licenseKey,
        string hwid = null,
        string clientName = "loader",
        string clientVersion = "1.0.0",
        string platform = "win",
        string productHash = null)
    {
        if (!_initialized)
        {
            return new LoginResult
            {
                StatusCode = 0,
                Message = "SafetyAPI não foi inicializada. Chame InitAsync() antes de LoginAsync()."
            };
        }

        if (string.IsNullOrWhiteSpace(discordId))
            return new LoginResult { StatusCode = 0, Message = "Discord ID vazio." };

        if (string.IsNullOrWhiteSpace(licenseKey))
            return new LoginResult { StatusCode = 0, Message = "LicenseKey vazia." };

        discordId = discordId.Trim();
        licenseKey = licenseKey.Trim().ToUpperInvariant();


        if (string.IsNullOrWhiteSpace(hwid))
            hwid = GenerateHwid();

        hwid = hwid.Trim();

        string effectiveProductHash = ResolveProductHash(productHash);

        if (!IsHex64(effectiveProductHash))
        {
            return new LoginResult
            {
                StatusCode = 0,
                Message = "productHash inválido/ausente. Passe productHash no LoginAsync, use TrySetProductHash(...) ou defina SAFETY_PRODUCT_HASH."
            };
        }

        var payload = new
        {
            discordId = discordId,
            licenseKey = licenseKey,
            hwid = hwid,
            productHash = effectiveProductHash,
            client = new
            {
                name = SafeSlice(clientName, 40),
                version = SafeSlice(clientVersion, 40),
                platform = SafeSlice(platform, 20)
            }
        };

        string json = JsonConvert.SerializeObject(payload);

        // Logs do request
        Log("LoginAsync() -> POST /v1/auth/login");
        Log($"discordId={discordId}");
        Log($"licenseKey={Mask(licenseKey, 4, 3)}");
        Log($"hwid(sha256)={Mask(hwid, 6, 6)}");
        Log($"productHash={Mask(effectiveProductHash, 8, 8)}");
        LogJson("payload", json);

        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage resp = null;
        string body = null;

        var sw = Stopwatch.StartNew();

        try
        {
            resp = await _http.PostAsync("/v1/auth/login", content);
            body = await resp.Content.ReadAsStringAsync();
            sw.Stop();

            var result = new LoginResult
            {
                StatusCode = (int)resp.StatusCode,
                RawBody = body,
                DiscordId = discordId,
                LicenseKey = licenseKey
            };

            result.RequestId = TryGetHeader(resp, "x-request-id") ?? TryGetJsonField(body, "requestId");

            // Logs do response
            Log($"Response status={(int)resp.StatusCode} time={sw.ElapsedMilliseconds}ms requestId={result.RequestId}");
            LogJson("responseBody", body);

            if (!resp.IsSuccessStatusCode)
            {
                result.Message = ParseApiMessage(body, $"Login falhou. Status: {(int)resp.StatusCode}");
                Log($"Login FAILED -> msg={result.Message}");
                return result;
            }

            result.Message = ParseApiMessage(body, "Authorized");

            TryFillLoginData(result, body);

            Log("Login OK ✅");
            Log($"expiresAt={result.ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "null"} daysLeft={result.DaysLeft?.ToString() ?? "null"}");
            Log($"product={result.ProductName} ({result.ProductId})");
            Log($"discord.user.username={result.DiscordUsername ?? "null"} globalName={result.DiscordGlobalName ?? "null"} tag={result.DiscordTag ?? "null"}");
            Log($"discord.user.avatarUrl={result.DiscordAvatarUrl ?? "null"}");

            return result;
        }
        catch (HttpRequestException ex)
        {
            sw.Stop();
            Log($"HTTP ERROR after {sw.ElapsedMilliseconds}ms -> {ex.Message}");
            return new LoginResult
            {
                StatusCode = 0,
                Message = $"Erro de conexão HTTP: {ex.Message}",
                RawBody = body
            };
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            Log($"TIMEOUT after {sw.ElapsedMilliseconds}ms");
            return new LoginResult
            {
                StatusCode = 0,
                Message = "Timeout: a API demorou para responder.",
                RawBody = body
            };
        }
        catch (JsonException ex)
        {
            sw.Stop();
            Log($"JSON ERROR -> {ex.Message}");
            return new LoginResult
            {
                StatusCode = resp != null ? (int)resp.StatusCode : 0,
                Message = $"Resposta JSON inválida: {ex.Message}",
                RawBody = body
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            Log($"UNEXPECTED ERROR -> {ex.Message}");
            return new LoginResult
            {
                StatusCode = 0,
                Message = $"Erro inesperado: {ex.Message}",
                RawBody = body
            };
        }
    }

    private async Task<AuthResult> SafeGetAsync(string path)
    {
        HttpResponseMessage resp = null;
        string body = null;

        try
        {
            Log($"GET {path}");
            resp = await _http.GetAsync(path);
            body = await resp.Content.ReadAsStringAsync();

            var r = new AuthResult
            {
                StatusCode = (int)resp.StatusCode,
                RawBody = body,
                RequestId = TryGetHeader(resp, "x-request-id") ?? TryGetJsonField(body, "requestId"),
            };

            Log($"GET status={(int)resp.StatusCode} requestId={r.RequestId}");
            LogJson("healthBody", body, 500);

            if (!resp.IsSuccessStatusCode)
            {
                r.Message = ParseApiMessage(body, $"Erro no GET {path}. Status: {(int)resp.StatusCode}");
                return r;
            }

            r.Message = ParseApiMessage(body, "OK");
            return r;
        }
        catch (HttpRequestException ex)
        {
            Log($"GET HTTP ERROR -> {ex.Message}");
            return new AuthResult { StatusCode = 0, Message = $"Erro de conexão HTTP: {ex.Message}", RawBody = body };
        }
        catch (TaskCanceledException)
        {
            Log("GET TIMEOUT");
            return new AuthResult { StatusCode = 0, Message = "Timeout ao chamar a API.", RawBody = body };
        }
        catch (Exception ex)
        {
            Log($"GET UNEXPECTED ERROR -> {ex.Message}");
            return new AuthResult { StatusCode = 0, Message = $"Erro inesperado: {ex.Message}", RawBody = body };
        }
    }

    private static string ParseApiMessage(string responseBody, string fallback)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return fallback;

        try
        {
            var j = JObject.Parse(responseBody);

            var msg = j.SelectToken("message")?.ToString();
            if (!string.IsNullOrWhiteSpace(msg))
                return msg;

            var code = j.SelectToken("error.code")?.ToString();
            if (!string.IsNullOrWhiteSpace(code))
                return code;

            return fallback;
        }
        catch
        {
            return responseBody.Length > 200 ? responseBody.Substring(0, 200) + "..." : responseBody;
        }
    }

    private static string TryGetJsonField(string responseBody, string field)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            var j = JObject.Parse(responseBody);
            return j.SelectToken(field)?.ToString();
        }
        catch { return null; }
    }

    private static void TryFillLoginData(LoginResult result, string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return;

        JObject j;
        try { j = JObject.Parse(responseBody); }
        catch { return; }

        // data.expiresAt
        var expiresAtStr = j.SelectToken("data.expiresAt")?.ToString();
        if (!string.IsNullOrWhiteSpace(expiresAtStr) && DateTime.TryParse(expiresAtStr, out var dt))
            result.ExpiresAt = dt;

        // data.daysLeft
        var daysLeftTok = j.SelectToken("data.daysLeft");
        if (daysLeftTok != null && int.TryParse(daysLeftTok.ToString(), out var days))
            result.DaysLeft = days;

        // product
        result.ProductId = j.SelectToken("data.product.id")?.ToString();
        result.ProductName = j.SelectToken("data.product.name")?.ToString();
        result.ProductHash = j.SelectToken("data.product.hash")?.ToString();

        // config
        result.ConfigVersion = j.SelectToken("data.config.version")?.ToString();
        result.DownloadLink = j.SelectToken("data.config.downloadLink")?.ToString();

        // user (fetch do Discord)
        result.DiscordUsername = j.SelectToken("data.user.username")?.ToString();
        result.DiscordGlobalName = j.SelectToken("data.user.globalName")?.ToString();
        result.DiscordAvatarUrl = j.SelectToken("data.user.avatarUrl")?.ToString();
        result.DiscordTag = j.SelectToken("data.user.tag")?.ToString();
    }

    private static string TryGetHeader(HttpResponseMessage resp, string headerName)
    {
        try
        {
            if (resp.Headers.TryGetValues(headerName, out var values))
            {
                foreach (var v in values) return v;
            }
        }
        catch { }
        return null;
    }

    public static string GenerateHwid()
    {
        try
        {
            string motherboardSerial = "";
            string cpuId = "";
            string biosSerial = "";
            string diskSerial = "";

            using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
            {
                foreach (var obj in searcher.Get())
                {
                    motherboardSerial = (obj["SerialNumber"]?.ToString() ?? "").Trim();
                    break;
                }
            }

            using (var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                {
                    cpuId = (obj["ProcessorId"]?.ToString() ?? "").Trim();
                    break;
                }
            }

            using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BIOS"))
            {
                foreach (var obj in searcher.Get())
                {
                    biosSerial = (obj["SerialNumber"]?.ToString() ?? "").Trim();
                    break;
                }
            }

            using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_PhysicalMedia"))
            {
                foreach (var obj in searcher.Get())
                {
                    diskSerial = (obj["SerialNumber"]?.ToString() ?? "").Trim();
                    if (!string.IsNullOrEmpty(diskSerial)) break;
                }
            }

            var raw = $"{motherboardSerial}-{cpuId}-{biosSerial}-{diskSerial}";
            return Sha256Hex(raw).ToLowerInvariant();
        }
        catch
        {
            return Guid.NewGuid().ToString("N");
        }
    }

    private static string Sha256Hex(string input)
    {
        using (var sha = SHA256.Create())
        {
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    private static string NormalizeBaseUrl(string url)
    {
        url = url.Trim();
        while (url.EndsWith("/")) url = url.Substring(0, url.Length - 1);
        return url;
    }

    private static bool IsDiscordId(string s)
    {
        if (string.IsNullOrEmpty(s)) return false;
        if (s.Length < 17 || s.Length > 19) return false;
        for (int i = 0; i < s.Length; i++)
            if (s[i] < '0' || s[i] > '9') return false;
        return true;
    }

    private static bool IsHex64(string s)
    {
        if (string.IsNullOrWhiteSpace(s) || s.Length != 64) return false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            bool isDigit = c >= '0' && c <= '9';
            bool isLowerHex = c >= 'a' && c <= 'f';
            bool isUpperHex = c >= 'A' && c <= 'F';
            if (!isDigit && !isLowerHex && !isUpperHex) return false;
        }
        return true;
    }

    private static string SafeSlice(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Trim();
        if (s.Length <= max) return s;
        return s.Substring(0, max);
    }

    private static string Mask(string value, int keepStart, int keepEnd)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Length <= keepStart + keepEnd) return "***";
        return value.Substring(0, keepStart) + "***" + value.Substring(value.Length - keepEnd);
    }

    public static void Fatal(string message, bool exit = false)
    {
        try
        {
            var folder = "Logs";
            var file = Path.Combine(folder, "ErrorLogs.txt");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} > {message}{Environment.NewLine}");
        }
        catch { }

        if (exit)
        {
            try
            {
                Process.Start(new ProcessStartInfo("cmd.exe", $"/c start cmd /C \"color c && title SafetyAPI Error && echo {message} && timeout /t 5\"")
                {
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                });
            }
            catch { }
            Environment.Exit(0);
        }
    }
}
