using apppasteleriav03.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

namespace apppasteleriav03.Services
{
    /// <summary>
    /// Servicio ligero para llamadas REST a Supabase (tables: productos, pedidos, pedido_items, order_locations, profiles).
    /// Implementa métodos comunes usados por MainPage, OrderPage y LiveTrackingPage.
    /// </summary>
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

            _http = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

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

        #region Products

        public async Task<List<Product>> GetProductsAsync(int? limit = null, CancellationToken cancellationToken = default)
        {
            try
            {
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

                // Validar y filtrar productos inválidos
                var validProducts = new List<Product>();
                foreach (var p in products)
                {
                    try
                    {
                        var errs = p.Validate();
                        if (errs == null || errs.Count == 0)
                            validProducts.Add(p);
                        else
                            Debug.WriteLine($"SupabaseService: producto inválido (id={p?.Id}): {string.Join("; ", errs)}");
                    }
                    catch (Exception vEx)
                    {
                        Debug.WriteLine($"SupabaseService: error validando producto: {vEx}");
                    }
                }

                // Normalizar imagenes y fallback
                NormalizeProductImages(validProducts);

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

        public async Task<Product?> GetProductAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_url}/rest/v1/productos?id=eq.{id}&select=*";
                using var resp = await _http.GetAsync(url, cancellationToken);
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);

                Debug.WriteLine($"GetProductAsync: status={resp.StatusCode}; bodyLength={(json?.Length ?? 0)}");
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetProductAsync failed: {resp.StatusCode} - {json}");
                    return null;
                }

                var list = JsonSerializer.Deserialize<List<Product>>(json, _jsonOpts);
                var product = (list != null && list.Count > 0) ? list[0] : null;
                if (product != null)
                    product.ImagenPath = string.IsNullOrWhiteSpace(ImageHelper.Normalize(product.ImagenPath)) ? ImageHelper.DefaultPlaceholder : ImageHelper.Normalize(product.ImagenPath);

                return product;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetProductAsync error: {ex}");
                return null;
            }
        }

        void NormalizeProductImages(IEnumerable<Product>? products)
        {
            if (products == null) return;

            foreach (var p in products)
            {
                try
                {
                    var normalized = ImageHelper.Normalize(p.ImagenPath);
                    p.ImagenPath = string.IsNullOrWhiteSpace(normalized) ? ImageHelper.DefaultPlaceholder : normalized;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"NormalizeProductImages: error for {p?.Nombre}: {ex}");
                    p.ImagenPath = ImageHelper.DefaultPlaceholder;
                }
            }
        }

        #endregion

        #region Orders & Items

        // Crear pedido + items (ya existía, se mantiene con algunas validaciones)
        public async Task<Order> CreateOrderAsync(Guid userid, List<OrderItem> items, CancellationToken cancellationToken = default)
        {
            var total = items.Sum(it => it.Price * it.Quantity);

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

            var resp = await _http.SendAsync(orderReq, cancellationToken);
            var createdOrderJson = await resp.Content.ReadAsStringAsync(cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                Debug.WriteLine($"CreateOrderAsync (order) failed: {resp.StatusCode} - {createdOrderJson}");
                throw new Exception(createdOrderJson);
            }

            var created = JsonSerializer.Deserialize<List<Order>>(createdOrderJson, _jsonOpts);
            if (created == null || created.Count == 0) throw new Exception("No se pudo crear el pedido.");
            var createdOrder = created[0];

            // Insertar items en batch
            var itemsPayload = items.Select(it => new
            {
                pedido_id = createdOrder.Id,
                producto_id = it.ProductId,
                cantidad = it.Quantity,
                precio = it.Price
            }).ToList();

            var itemsContent = new StringContent(JsonSerializer.Serialize(itemsPayload), Encoding.UTF8, "application/json");
            using var itemsReq = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/pedido_items") { Content = itemsContent };
            itemsReq.Headers.Add("Prefer", "return=representation");

            var respItems = await _http.SendAsync(itemsReq, cancellationToken);
            var respItemsBody = await respItems.Content.ReadAsStringAsync(cancellationToken);
            if (!respItems.IsSuccessStatusCode)
            {
                Debug.WriteLine($"CreateOrderAsync (items) failed: {respItems.StatusCode} - {respItemsBody}");
                throw new Exception(respItemsBody);
            }

            return createdOrder;
        }

        // Obtener órdenes con filtros generales
        public async Task<List<Order>> GetOrdersAsync(Guid? userId = null, string? status = null, bool includeItems = false, int? limit = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var sb = new StringBuilder();
                sb.Append($"{_url}/rest/v1/pedidos?select=*");

                if (userId.HasValue)
                    sb.Append($"&userid=eq.{userId.Value}");

                if (!string.IsNullOrWhiteSpace(status))
                    sb.Append($"&status=eq.{Uri.EscapeDataString(status)}");

                sb.Append("&order=created_at.desc");

                if (limit.HasValue && limit.Value > 0)
                    sb.Append($"&limit={limit.Value}");

                var url = sb.ToString();
                Debug.WriteLine($"GetOrdersAsync: requesting {url}");

                using var resp = await _http.GetAsync(url, cancellationToken);
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"GetOrdersAsync: status={resp.StatusCode}; bodyLength={(json?.Length ?? 0)}");

                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetOrdersAsync failed: {resp.StatusCode} - {json}");
                    return new List<Order>();
                }

                var orders = JsonSerializer.Deserialize<List<Order>>(json, _jsonOpts) ?? new List<Order>();

                if (includeItems && orders.Count > 0)
                {
                    foreach (var o in orders)
                    {
                        try
                        {
                            o.Items = await GetOrderItemsAsync(o.Id, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"GetOrdersAsync: error getting items for order {o.Id}: {ex}");
                        }
                    }
                }

                return orders;
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("GetOrdersAsync: cancelled");
                return new List<Order>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetOrdersAsync error: {ex}");
                return new List<Order>();
            }
        }

        // Obtener órdenes de un usuario (nombre claro)
        public async Task<List<Order>> GetOrdersByUserAsync(Guid userId, bool includeItems = false, CancellationToken cancellationToken = default)
        {
            return await GetOrdersAsync(userId: userId, includeItems: includeItems, cancellationToken: cancellationToken);
        }

        // Obtener una orden específica
        public async Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_url}/rest/v1/pedidos?idpedido=eq.{orderId}&select=*", cancellationToken);
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"GetOrderAsync: status={resp.StatusCode}; bodyLength={(json?.Length ?? 0)}");

                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetOrderAsync failed: {resp.StatusCode} - {json}");
                    return null;
                }

                var list = JsonSerializer.Deserialize<List<Order>>(json, _jsonOpts);
                var order = (list != null && list.Count > 0) ? list[0] : null;

                if (order != null)
                {
                    try
                    {
                        order.Items = await GetOrderItemsAsync(order.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"GetOrderAsync: error loading items for order {order.Id}: {ex}");
                    }
                }

                return order;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetOrderAsync error: {ex}");
                return null;
            }
        }

        // Obtener items de una orden específica
        public async Task<List<OrderItem>> GetOrderItemsAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_url}/rest/v1/pedido_items?pedido_id=eq.{orderId}&select=*", cancellationToken);
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"GetOrderItemsAsync: status={resp.StatusCode}; bodyLength={(json?.Length ?? 0)}");

                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetOrderItemsAsync failed: {resp.StatusCode} - {json}");
                    return new List<OrderItem>();
                }

                var items = JsonSerializer.Deserialize<List<OrderItem>>(json, _jsonOpts) ?? new List<OrderItem>();
                return items;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetOrderItemsAsync error: {ex}");
                return new List<OrderItem>();
            }
        }

        #endregion

        #region Locations (tracking)

        public async Task<List<OrderLocation>> GetOrderLocationsAsync(Guid orderId, int limit = 100, CancellationToken cancellationToken = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_url}/rest/v1/order_locations?pedido_id=eq.{orderId}&order=registrado_en.desc&limit={limit}", cancellationToken);
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                Debug.WriteLine($"GetOrderLocationsAsync: status={resp.StatusCode}; bodyLength={(json?.Length ?? 0)}");

                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"GetOrderLocationsAsync failed: {resp.StatusCode} - {json}");
                    return new List<OrderLocation>();
                }

                var locs = JsonSerializer.Deserialize<List<OrderLocation>>(json, _jsonOpts) ?? new List<OrderLocation>();
                return locs;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetOrderLocationsAsync error: {ex}");
                return new List<OrderLocation>();
            }
        }

        // Insertar registro de ubicación (histórico)
        public async Task<bool> InsertOrderLocationAsync(OrderLocation loc, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new[] { new
                {
                    pedido_id = loc.PedidoId,
                    latitud = loc.Latitud,
                    longitud = loc.Longitud,
                    registrado_en = loc.RegistradoEn,
                    dispositivo_id = loc.DispositivoId,
                    velocidad = loc.Velocidad,
                    rumb = loc.Rumbo
                } };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var req = new HttpRequestMessage(HttpMethod.Post, $"{_url}/rest/v1/order_locations") { Content = content };
                req.Headers.Add("Prefer", "return=representation");

                var resp = await _http.SendAsync(req, cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"InsertOrderLocationAsync failed: {resp.StatusCode} - {body}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"InsertOrderLocationAsync error: {ex}");
                return false;
            }
        }

        // Actualizar ubicación actual en la fila pedidos (PATCH)
        public async Task<bool> UpdateOrderLocationAsync(Guid orderId, double latitude, double longitude, bool enableTracking = true, CancellationToken cancellationToken = default)
        {
            try
            {
                var payload = new
                {
                    latitud_actual = latitude,
                    longitud_actual = longitude,
                    seguimiento_habilitado = enableTracking
                };

                var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                using var req = new HttpRequestMessage(HttpMethod.Patch, $"{_url}/rest/v1/pedidos?id=eq.{orderId}") { Content = content };
                req.Headers.Add("Prefer", "return=representation");

                var resp = await _http.SendAsync(req, cancellationToken);
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"UpdateOrderLocationAsync failed: {resp.StatusCode} - {body}");
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"UpdateOrderLocationAsync error: {ex}");
                return false;
            }
        }

        #endregion

        #region Profiles / Auth helpers

        public async Task<Profile?> GetProfileAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var resp = await _http.GetAsync($"{_url}/rest/v1/profiles?id=eq.{id}&select=*", cancellationToken);
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
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

        #endregion
    }
}