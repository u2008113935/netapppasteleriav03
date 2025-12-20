using apppasteleriav03.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

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


        public async Task<List<Product>> GetProductsAsync(int? limit = null, CancellationToken cancellationToken = default)
        {
            try
            {
                // Construir la URL (permitir limit para pruebas)
                var url = $"{_url}/rest/v1/productos?select=*";
                if (limit.HasValue && limit.Value > 0)
                    url += $"&limit={limit.Value}";

                Debug.WriteLine($"GetProductsAsync: requesting {url}");

                using var resp = await _http.GetAsync(url, cancellationToken);
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);

                Debug.WriteLine($"GetProductsAsync: status={resp.StatusCode}; bodyLength={(json?.Length ?? 0)}");

                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetProductsAsync failed: {resp.StatusCode} - {json}");
                    return new List<Product>();
                }

                var products = JsonSerializer.Deserialize<List<Product>>(json, _jsonOpts) ?? new List<Product>();
                Debug.WriteLine($"GetProductsAsync: deserialized products count = {products.Count}");

                // Validar y filtrar productos inválidos
                var validProducts = new List<Product>();
                foreach (var p in products)
                {
                    try
                    {
                        var errs = p.Validate();
                        if (errs == null || errs.Count == 0)
                        {
                            validProducts.Add(p);
                        }
                        else
                        {
                            Debug.WriteLine($"SupabaseService: producto inválido (id={p?.Id}): {string.Join("; ", errs)}");
                        }
                    }
                    catch (Exception vEx)
                    {
                        Debug.WriteLine($"SupabaseService: error validando producto (maybe malformed JSON item): {vEx}");
                    }
                }

                // Normalizar las URLs de imagen (ImageHelper.Normalize + fallback)
                NormalizeProductImages(validProducts);

                // Logear unos ejemplos para depuración
                int logged = 0;
                foreach (var p in validProducts)
                {
                    Debug.WriteLine($"Product ready: Id={p.Id} Nombre='{p.Nombre}' Imagen='{p.ImagenPath}' Precio={p.Precio}");
                    if (++logged >= 5) break;
                }

                return validProducts;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("GetProductsAsync: cancelled");
                return new List<Product>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SupabaseService.GetProductsAsync error: {ex}");
                return new List<Product>();
            }
        }


        public async Task<Product?> GetProductAsync(Guid id)
        {
            try
            {
                var resp = await _http.GetAsync($"{_url}/rest/v1/productos?id=eq.{id}&select=*");
                var json = await resp.Content.ReadAsStringAsync();

                Debug.WriteLine($"GetProductAsync: status={resp.StatusCode}; bodyLength={(json?.Length ?? 0)}");

                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetProductAsync failed: {resp.StatusCode} - {json}");
                    return null;
                }

                var list = JsonSerializer.Deserialize<List<Product>>(json, _jsonOpts);
                var product = (list != null && list.Count > 0) ? list[0] : null;

                if (product != null)
                {
                    // Normalizar imagen del producto
                    var normalized = ImageHelper.Normalize(product.ImagenPath);
                    product.ImagenPath = string.IsNullOrWhiteSpace(normalized) ? ImageHelper.DefaultPlaceholder : normalized;
                }

                return product;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetProductAsync error: {ex}");
                return null;
            }
        }

        // Reemplaza el método NormalizeProductImages en SupabaseService.cs por este:
        void NormalizeProductImages(IEnumerable<Product>? products)
        {
            try
            {
                Debug.WriteLine($"NormalizeProductImages: start - products count = {(products == null ? 0 : products.Count())}");
            }
            catch
            {
                // si products no es un IEnumerable que implementa Count, ignoramos el Count() y seguimos
                Debug.WriteLine("NormalizeProductImages: start - (could not read count)");
            }

            if (products == null) return;

            foreach (var p in products)
            {
                try
                {
                    Debug.WriteLine($"NormalizeProductImages: processing product id={p?.Id} " +
                        $"nombre='{p?.Nombre}' " +
                        $"rawImagenLen={(p?.ImagenPath?.Length ?? 0)} " +
                        $"rawPreview='{(p?.ImagenPath != null && 
                        p.ImagenPath.Length > 120 ? 
                        p.ImagenPath.Substring(0, 120) : 
                        p?.ImagenPath)}'");

                    var normalized = ImageHelper.Normalize(p.ImagenPath);

                    Debug.WriteLine($"NormalizeProductImages: normalized for id={p?.Id} " +
                        $"=> {(normalized == null ? "(null)" : 
                        (normalized.Length > 200 ? normalized.Substring(0, 200) : normalized))}");

                    if (string.IsNullOrWhiteSpace(normalized))
                    {
                        // Usar placeholder definido en ImageHelper para evitar mismatch de nombres
                        p.ImagenPath = ImageHelper.DefaultPlaceholder; // local en Resources/Images
                        Debug.WriteLine($"NormalizeProductImages: assigned placeholder for '{p?.Nombre}'");
                    }
                    else
                    {
                        p.ImagenPath = normalized;
                        Debug.WriteLine($"NormalizeProductImages: assigned normalized for '{p?.Nombre}' " +
                            $"-> {p.ImagenPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error normalizando imagen para {p?.Nombre}: {ex}");
                    p.ImagenPath = ImageHelper.DefaultPlaceholder;
                }
            }

            Debug.WriteLine("NormalizeProductImages: end");
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