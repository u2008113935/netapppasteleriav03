using apppasteleriav03.Models;
using apppasteleriav03.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using System.Diagnostics;

namespace apppasteleriav03.Views
{
    public partial class OrderPage : ContentPage
    {
        readonly SupabaseService _supabase = SupabaseService.Instance;
        List<Order> _orders = new List<Order>();
        CancellationTokenSource? _cts;

        public OrderPage()
        {
            InitializeComponent();
            // Opcional: llenar Picker de estados (puedes adaptar según tus estados reales)
            StatusPicker.ItemsSource = new List<string> { "Todos", "pendiente", "en_preparacion", "en_reparto", "entregado", "cancelado" };
            StatusPicker.SelectedIndex = 0;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            _ = LoadOrdersAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();
            _cts?.Cancel();
            _cts = null;
        }

        async Task LoadOrdersAsync(bool forceRefresh = false)
        {
            try
            {
                LoadingIndicator.IsVisible = true;
                LoadingIndicator.IsRunning = true;

                _cts?.Cancel();
                _cts = new CancellationTokenSource();

                // Obtener userId desde AuthService; asumo que AuthService.Instance.UserId es string (si es Guid, adapta)
                string? userIdStr = AuthService.Instance?.UserId;
                if (string.IsNullOrWhiteSpace(userIdStr))
                {
                    // intentar leer desde SecureStorage si guardas auth_user_id
                    try
                    {
                        userIdStr = await SecureStorage.Default.GetAsync("auth_user_id");
                    }
                    catch { /* ignora si no existe */ }
                }

                if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out var userGuid))
                {
                    await DisplayAlert("Atención", "No se encontró usuario autenticado.", "OK");
                    _orders = new List<Order>();
                    OrdersCollection.ItemsSource = _orders;
                    UpdateCount();
                    return;
                }

                string? statusFilter = null;
                if (StatusPicker.SelectedIndex > 0)
                    statusFilter = StatusPicker.SelectedItem as string;

                // Llamar al servicio para obtener las órdenes del usuario
                var orders = await _supabase.GetOrdersByUserAsync(userGuid, includeItems: true, cancellationToken: _cts.Token);

                _orders = orders ?? new List<Order>();
                // Asegurar asignación en hilo UI
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    OrdersCollection.ItemsSource = _orders;
                    UpdateCount();
                });
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("LoadOrdersAsync cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoadOrdersAsync error: {ex}");
                await DisplayAlert("Error", "No se pudieron cargar los pedidos. Revisa la conexión.", "OK");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
                RefreshViewControl.IsRefreshing = false;
            }
        }

        void UpdateCount()
        {
            LblCount.Text = $"Pedidos: {_orders?.Count ?? 0}";
        }

        // Pull-to-refresh handler
        private void RefreshView_Refreshing(object sender, EventArgs e)
        {
            _ = LoadOrdersAsync(forceRefresh: true);
        }

        // Botón Actualizar
        private void OnRefreshClicked(object sender, EventArgs e)
        {
            _ = LoadOrdersAsync(forceRefresh: true);
        }

        // Al seleccionar un item (navegar a detalles)
        private async void OrdersCollection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var sel = e.CurrentSelection;
            if (sel == null || sel.Count == 0) return;

            if (sel[0] is Order order)
            {
                // Des-seleccionar para UX
                ((CollectionView)sender).SelectedItem = null;

                // Navegar a página de detalles o seguimiento
                try
                {
                    // Si tienes una página de detalles, úsala aquí:
                    // await Navigation.PushAsync(new OrderDetailsPage(order.Id));

                    // Si prefieres ir a la página de seguimiento en tiempo real (LiveTrackingPage):
                    // Asegúrate de tener LiveTrackingPage implemented and registered or available.
                    try
                    {
                        await Navigation.PushAsync(new LiveTrackingPage(order.Id));
                    }
                    catch (Exception navEx)
                    {
                        Debug.WriteLine($"Navigation to LiveTrackingPage failed: {navEx}");
                        // Fallback: mostrar info básica
                        await DisplayAlert("Pedido", $"Id: {order.Id}\nTotal: S/ {order.Total:N2}\nEstado: {order.Status}", "OK");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OrdersCollection_SelectionChanged error: {ex}");
                }
            }
        }

        // Botón "Ver / Seguir"
        private async void OnViewOrTrackClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid orderId)
            {
                try
                {
                    // Buscar orden en la lista
                    var order = _orders?.Find(o => o.Id == orderId);
                    if (order == null)
                    {
                        // cargar desde servicio si no está en memoria
                        order = await _supabase.GetOrderAsync(orderId);
                    }

                    if (order == null)
                    {
                        await DisplayAlert("Error", "Orden no encontrada.", "OK");
                        return;
                    }

                    // Navegar a LiveTrackingPage (repartidor debe estar asignado y seguimiento habilitado)
                    try
                    {
                        await Navigation.PushAsync(new LiveTrackingPage(order.Id));
                    }
                    catch
                    {
                        // Si LiveTrackingPage no existe, mostrar detalles mínimos
                        await DisplayAlert("Pedido", $"Id: {order.Id}\nTotal: S/ {order.Total:N2}\nEstado: {order.Status}", "OK");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"OnViewOrTrackClicked error: {ex}");
                    await DisplayAlert("Error", "No se pudo abrir la orden.", "OK");
                }
            }
        }
    }
}