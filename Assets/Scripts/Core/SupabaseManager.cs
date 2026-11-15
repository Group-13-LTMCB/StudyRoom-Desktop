#nullable enable
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Supabase;
using CozyFocus.Models;

namespace CozyFocus.Core
{
    /// <summary>
    /// Cấu trúc dữ liệu cấu hình đọc từ Resources/Config/AppConfig.json.
    /// </summary>
    [Serializable]
    public class AppConfigData
    {
        public string supabaseUrl = string.Empty;
        public string supabaseAnonKey = string.Empty;

        // Dự phòng các định dạng naming convention khác nhau
        public string url = string.Empty;
        public string anon_key = string.Empty;
        public string supabase_url = string.Empty;
        public string supabase_anon_key = string.Empty;

        public string GetUrl()
        {
            if (!string.IsNullOrWhiteSpace(supabaseUrl)) return supabaseUrl.Trim();
            if (!string.IsNullOrWhiteSpace(url)) return url.Trim();
            if (!string.IsNullOrWhiteSpace(supabase_url)) return supabase_url.Trim();
            return string.Empty;
        }

        public string GetAnonKey()
        {
            if (!string.IsNullOrWhiteSpace(supabaseAnonKey)) return supabaseAnonKey.Trim();
            if (!string.IsNullOrWhiteSpace(anon_key)) return anon_key.Trim();
            if (!string.IsNullOrWhiteSpace(supabase_anon_key)) return supabase_anon_key.Trim();
            return string.Empty;
        }
    }

    /// <summary>
    /// Singleton quản lý kết nối Supabase toàn cục.
    /// Kế thừa MonoBehaviour, tồn tại xuyên suốt giữa các Scene (DontDestroyOnLoad).
    /// Khởi tạo bất đồng bộ an toàn, không block Main Thread và chống crash khi cấu hình rỗng.
    /// </summary>
    public class SupabaseManager : MonoBehaviour
    {
        public static SupabaseManager? Instance { get; private set; }

        [Header("Runtime Status")]
        [SerializeField] private bool isInitialized = false;

        private Supabase.Client? _client;
        private AppConfigData? _config;

        public Supabase.Client? Client => _client;
        public bool IsInitialized => isInitialized;
        public AppConfigData? Config => _config;

        public event Action<bool>? OnInitializationCompleted;

        private const string ConfigResourcePath = "Config/AppConfig";

        private void Awake()
        {
            // Thiết lập Singleton bền vững
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Bắt đầu khởi tạo bất đồng bộ mà không block Main Thread
            _ = InitializeAsync();
        }

        /// <summary>
        /// Đọc cấu hình từ Resources/Config/AppConfig.json và khởi tạo client Supabase.
        /// </summary>
        public async Task<bool> InitializeAsync()
        {
            try
            {
                // 1. Đọc file cấu hình JSON
                _config = LoadConfig();

                if (_config == null)
                {
                    Debug.LogError("[SupabaseManager] Không thể tải AppConfig.json từ Resources/Config/AppConfig.");
                    OnInitializationCompleted?.Invoke(false);
                    return false;
                }

                string url = _config.GetUrl();
                string anonKey = _config.GetAnonKey();

                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(anonKey))
                {
                    Debug.LogWarning("[SupabaseManager] URL hoặc Anon Key trong AppConfig.json đang trống. Tạm dừng kết nối mạng.");
                    OnInitializationCompleted?.Invoke(false);
                    return false;
                }

                // 2. Thiết lập cấu hình SupabaseOptions (Bảo toàn nguyên tắc Zero-Cost)
                var options = new SupabaseOptions
                {
                    AutoRefreshToken = true,
                    AutoConnectRealtime = true
                };

                // 3. Khởi tạo Client Supabase
                _client = new Supabase.Client(url, anonKey, options);
                await _client.InitializeAsync();

                isInitialized = true;
                Debug.Log($"[SupabaseManager] Khởi tạo Supabase Client thành công tới endpoint: {url}");
                OnInitializationCompleted?.Invoke(true);
                return true;
            }
            catch (Exception ex)
            {
                // Xử lý ngoại lệ an toàn, không để crash Main Thread
                Debug.LogError($"[SupabaseManager] Lỗi trong quá trình khởi tạo Supabase Client: {ex.Message}\n{ex.StackTrace}");
                isInitialized = false;
                OnInitializationCompleted?.Invoke(false);
                return false;
            }
        }

        /// <summary>
        /// Nạp cấu hình từ Unity Resources hoặc fallback sang file hệ thống.
        /// </summary>
        public static AppConfigData? LoadConfig()
        {
            try
            {
                // Ưu tiên đọc qua Unity Resources
                TextAsset? configText = Resources.Load<TextAsset>(ConfigResourcePath);
                string jsonString = string.Empty;

                if (configText != null && !string.IsNullOrWhiteSpace(configText.text))
                {
                    jsonString = configText.text;
                }
                else
                {
                    // Fallback đọc trực tiếp file khi chạy test hoặc trong editor
                    string fallbackPath = Path.Combine(Application.dataPath, "Resources", "Config", "AppConfig.json");
                    if (File.Exists(fallbackPath))
                    {
                        jsonString = File.ReadAllText(fallbackPath);
                    }
                }

                if (string.IsNullOrWhiteSpace(jsonString))
                {
                    return null;
                }

                return JsonUtility.FromJson<AppConfigData>(jsonString);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SupabaseManager] Lỗi khi nạp AppConfig: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Helper: Đăng nhập ẩn danh (Anonymous Auth) nếu có nhu cầu.
        /// </summary>
        public async Task<bool> SignInAnonymouslyAsync()
        {
            if (_client == null || !isInitialized)
            {
                Debug.LogWarning("[SupabaseManager] Client chưa được khởi tạo để thực hiện đăng nhập.");
                return false;
            }

            try
            {
                var session = await _client.Auth.SignInAnonymously();
                return session != null && session.User != null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SupabaseManager] Đăng nhập ẩn danh thất bại: {ex.Message}");
                return false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }
}
