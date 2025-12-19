using apppasteleriav03.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace apppasteleriav03.Services
{

 
    public class SupabaseService
    {
        public static SupabaseService Instance { get; } = new SupabaseService();

        readonly HttpClient _http;
        readonly string _url;
        readonly string _anon;
        readonly JsonSerializerOptions _jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        public SupabaseService()
        {
            _url = SupabaseConfig.SUPABASE_URL?.TrimEnd('/') ?? string.Empty;
            _anon = SupabaseConfig.SUPABASE_ANON_KEY ?? string.Empty;

            _http = new HttpClient();

            if (!string.IsNullOrWhiteSpace(_anon))
                _http.DefaultRequestHeaders.Add("apikey", _anon);

            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // Permite setear el token del usuario (Authorization: Bearer ...)
        public void SetUserToken(string? token)
        {
            _http.DefaultRequestHeaders.Authorization = null;
            if (!string.IsNullOrWhiteSpace(token))
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                Debug.WriteLine($"[SupabaseService]: Authorization set: {_http.DefaultRequestHeaders.Authorization}");
            }
            else
            {
                Debug.WriteLine("[SupabaseService]: Authorization header cleared.");
            }
        }

        public AuthUser GetCurrentUser()
        {
            return new AuthUser
            {
                Id = AuthService.Instance.UserId,
                Email = AuthService.Instance.UserEmail
            };
        }

        public async Task<List<Product>> GetProductsAsync()
        {
            try
            {
                var resp = await _http.GetAsync($"{_url}/rest/v1/productos?select=*");
                var json = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetProductsAsync failed: {resp.StatusCode} - {json}");
                    return new List<Product>();
                }

                var products = JsonSerializer.Deserialize<List<Product>>(json, _jsonOpts) ?? new List<Product>();
                NormalizeProductImages(products);
                return products;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("SupabaseService.GetProductsAsync error: " + ex.Message);
                return new List<Product>();
            }
        }

        void NormalizeProductImages(IEnumerable<Product>? products)
        {
            if (products == null) return;
            foreach (var p in products)
            {
                try
                {
                    var raw = p.ImagenPath;
                    if (string.IsNullOrWhiteSpace(raw))
                    {
                        p.ImagenPath = "avatar_placeholder.png"; // local en Resources/Images
                        Debug.WriteLine($"Product '{p.Nombre}' image normalized -> (placeholder)");
                        continue;
                    }

                    var decoded = Uri.UnescapeDataString(raw).Trim();
                    if (Uri.TryCreate(decoded, UriKind.Absolute, out var absolute))
                    {
                        p.ImagenPath = absolute.ToString();
                    }
                    else
                    {
                        var fileName = decoded.TrimStart('/');
                        if (string.IsNullOrWhiteSpace(SupabaseConfig.BUCKET_NAME))
                        {
                            p.ImagenPath = "avatar_placeholder.png";
                            Debug.WriteLine($"NormalizeProductImages: BUCKET_NAME vacío para '{p.Nombre}'");
                        }
                        else
                        {
                            p.ImagenPath = $"{_url}/storage/v1/object/public/{SupabaseConfig.BUCKET_NAME}/{Uri.EscapeDataString(fileName)}";
                        }
                    }

                    Debug.WriteLine($"Product '{p.Nombre}' image normalized -> {p.ImagenPath}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error normalizando imagen para {p?.Nombre}: {ex}");
                    p.ImagenPath = "avatar_placeholder.png";
                }
            }
        }

        public async Task<Order> CreateOrderAsync(Guid userid, List<OrderItem> items)
        {
            var total = 0m;
            foreach (var it in items) total += it.Price * it.Quantity;

            // Usamos user_id por convención; si tu BD usa 'userid' cambia aquí o en la BD.
            var orderPayload = new
            {
                userid = userid,
                total = total,
                status = "pendiente",
                created_at = DateTime.UtcNow
            };

            var orderContent = new StringContent(JsonSerializer.Serialize(orderPayload), Encoding.UTF8, "application/json");
            using var orderReq = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/pedidos") { Content = orderContent };
            orderReq.Headers.Add("Prefer", "return=representation");

            var resp = await _http.SendAsync(orderReq);
            var createdOrderJson = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode)
            {
                Debug.WriteLine($"CreateOrderAsync (order) failed: {resp.StatusCode} - {createdOrderJson}");
                throw new Exception(createdOrderJson);
            }

            var created = JsonSerializer.Deserialize<List<Order>>(createdOrderJson, _jsonOpts);
            if (created == null || created.Count == 0) throw new Exception("No se pudo crear el pedido.");
            var createdOrder = created[0];

            // Asegúrate que la clave que uses ('pedido_id' o 'pedidos_id') coincide con tu tabla pedido_items
            var itemsPayload = new List<object>();
            foreach (var it in items)
            {
                itemsPayload.Add(new
                {
                    pedido_id = createdOrder.Id,
                    producto_id = it.ProductId,
                    cantidad = it.Quantity,
                    precio = it.Price
                });
            }

            var itemsContent = new StringContent(JsonSerializer.Serialize(itemsPayload), Encoding.UTF8, "application/json");
            using var itemsReq = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/pedido_items") { Content = itemsContent };
            itemsReq.Headers.Add("Prefer", "return=representation");

            var respItems = await _http.SendAsync(itemsReq);
            var respItemsBody = await respItems.Content.ReadAsStringAsync();
            if (!respItems.IsSuccessStatusCode)
            {
                Debug.WriteLine($"CreateOrderAsync (items) failed: {respItems.StatusCode} - {respItemsBody}");
                throw new Exception(respItemsBody);
            }

            return createdOrder;
        }

        public async Task<(bool Success, string? AccessToken, string? RefreshToken, Guid? UserId, string? Error, string? Email)> SignInAsync(string email, string password)
        {
            try
            {
                var payload = new { email = email, password = password };
                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var resp = await _http.PostAsync($"{_url}/auth/v1/token?grant_type=password", content);
                var js = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode) return (false, null, null, null, js, null);

                using var doc = JsonDocument.Parse(js);
                var root = doc.RootElement;

                var accessToken = root.GetProperty("access_token").GetString();
                var refreshToken = root.TryGetProperty("refresh_token", out var r) ? r.GetString() : null;
                var userElement = root.GetProperty("user");
                var userIdStr = userElement.GetProperty("id").GetString();

                // Intentar extraer email del objeto user si está presente
                string? userEmail = null;
                if (userElement.TryGetProperty("email", out var eprop))
                    userEmail = eprop.GetString();

                Guid? userId = null;
                if (!string.IsNullOrWhiteSpace(userIdStr) && Guid.TryParse(userIdStr, out var guid)) userId = guid;

                return (true, accessToken, refreshToken, userId, null, userEmail);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SignInAsync error: {ex.Message}");
                return (false, null, null, null, ex.Message, null);
            }
        }

        public async Task<Profile?> GetProfileAsync(Guid id)
        {
            try
            {
                var resp = await _http.GetAsync($"{_url}/rest/v1/profiles?id=eq.{id}&select=*");
                var json = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetProfileAsync failed: {resp.StatusCode} - {json}");
                    return null;
                }
                var list = JsonSerializer.Deserialize<List<Profile>>(json, _jsonOpts);
                return (list != null && list.Count > 0) ? list[0] : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetProfileAsync exception: {ex}");
                return null;
            }
        }
    }
}