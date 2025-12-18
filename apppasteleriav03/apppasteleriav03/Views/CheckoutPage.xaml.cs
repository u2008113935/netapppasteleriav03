using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Essentials;
using apppasteleriav03.Services;
using apppasteleriav03.Models;
//using Kotlin.Jvm.Internal;
using System.Diagnostics;
using Microsoft.Maui.Storage;

namespace apppasteleriav03.Views
{
    /// <summary>
    /// Code-behind para CheckoutPage.xaml
    /// Implementa la lógica de visualización de resumen, validaciones y envío de pedido a Supabase.
    /// Comentarios didácticos incluidos en el código explicando qué hace y por qué.
    /// </summary>
    public partial class CheckoutPage : ContentPage
    {
        readonly CartService _cart = CartService.Instance;           // servicio singleton del carrito
        readonly SupabaseService _supabase = SupabaseService.Instance;  // servicio para llamadas a Supabase

        // Tarifas / constantes simples para el ejemplo
        const decimal DefaultShipping = 1500m;

        public CheckoutPage()
        {
            InitializeComponent();

            // Asignar ItemsSource del CollectionView al carrito (ObservableCollection)
            CartCollection.ItemsSource = _cart.Items;

            // Suscribir para actualizar montos cuando cambie el carrito
            _cart.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_cart.Total) || e.PropertyName == nameof(_cart.Count))
                    UpdateAmounts();
            };

            // Inicializar la vista con los valores actuales
            UpdateAmounts();

            // Inicialmente seleccionar método de pago 0 (Efectivo) si existe el picker
            if (PaymentPicker?.Items?.Count > 0)
                PaymentPicker.SelectedIndex = 0;
        }

        // Actualiza Subtotal, Envío y Total en la UI
        void UpdateAmounts()
        {
            var subtotal = _cart.Total;
            decimal shipping = DeliverySwitch?.IsToggled == true ? DefaultShipping : 0m;
            var total = subtotal + shipping;

            SubtotalLabel.Text = $"{subtotal:N2}";
            ShippingLabel.Text = $"{shipping:N2}";
            TotalLabel.Text = $"{total:N2}";
        }

        // Handler cuando el switch de Delivery cambia: recalcular montos
        void OnDeliveryToggled(object sender, ToggledEventArgs e)
        {
            UpdateAmounts();
        }

        // Handler del picker de métodos de pago: mostrar/ocultar detalles de tarjeta
        void OnPaymentMethodChanged(object sender, EventArgs e)
        {
            if (PaymentPicker == null) return;

            // Mostrar la sección de datos de tarjeta solo para métodos que lo requieran
            var idx = PaymentPicker.SelectedIndex;
            if (idx == 1 || idx == 2) // 1 = Tarjeta (POS), 2 = Tarjeta (online)
                CardDetailsStack.IsVisible = true;
            else
                CardDetailsStack.IsVisible = false;
        }

        // Volver a la página anterior
        async void OnBackClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("..");
        }

        // Lógica principal: validar inputs y crear el pedido en Supabase
        async void OnPlaceOrderClicked(object sender, EventArgs e)
        {
            // Validaciones básicas antes de proceder
            if (!_cart.Items.Any())
            {
                await DisplayAlert("Carrito vacío", "Agrega productos antes de continuar.", "OK");
                return;
            }

            // Validar dirección si se eligió entrega a domicilio
            if (DeliverySwitch.IsToggled && string.IsNullOrWhiteSpace(AddressEditor.Text))
            {
                await DisplayAlert("Dirección requerida", "Por favor ingresa la dirección de entrega.", "OK");
                return;
            }

            // Si el método de pago requiere tarjeta, validar campos mínimos
            var paymentIndex = PaymentPicker.SelectedIndex;
            if ((paymentIndex == 1 || paymentIndex == 2) && 
                (string.IsNullOrWhiteSpace(CardNumberEntry.Text) || string.IsNullOrWhiteSpace(CardHolderEntry.Text)))
            {
                await DisplayAlert("Pago", "Ingresa los datos de la tarjeta.", "OK");
                return;
            }

            // Recuperar user_id y token de SecureStorage para identificar al usuario
            Guid userId;
            try
            {
                // Obtener userId desde el servicio de autenticación
                var userIdStr = AuthService.Instance.UserId;

                // Si no esta en memoria, intentar desde SecureStorage
                //mantener la misma clave en AuthService y aquí
                if (string.IsNullOrWhiteSpace(userIdStr)) 
                    userIdStr = await SecureStorage.Default.GetAsync("auth_user_id");

                //Validar que userId es un GUID válido y parsearlo
                if (string.IsNullOrWhiteSpace(userIdStr) || !Guid.TryParse(userIdStr, out userId))
                {
                    // Si no hay sesión, redirigir a login
                    await DisplayAlert("Sesión requerida", "Debes iniciar sesión para completar el pedido.", "OK");
                    await Shell.Current.GoToAsync("//login");
                    return;
                }

                //Obetner el token desde AuthService
                var token = await AuthService.Instance.GetAccessTokenAsync();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    _supabase.SetUserToken(token);
                    Debug.WriteLine($"[Checkout] Token set: " +
                        $"{token?.Substring(0, Math.Min(8, token.Length))}...");
                }
                   

            }
            catch (Exception ex)
            {
                // SecureStorage puede fallar en algunos entornos (emuladores). Informar al usuario.
                await DisplayAlert("Error", $"No se pudo verificar la sesión: {ex.Message}", "OK");
                return;
            }

            // Preparar payload de items para enviar al servicio
            var itemsPayload = _cart.Items.Select(i => new OrderItem
            {
                // Id y OrderId no son necesarios en la inserción de pedido_items (será asignado por el server),
                // pero el CreateOrderAsync utiliza ProductId, Quantity y Price.
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                Price = i.Price
            }).ToList();

            // Mostrar indicador de actividad y deshabilitar botón para evitar dobles envíos
            PlaceOrderButton.IsEnabled = false;
            CheckoutActivity.IsVisible = true;
            CheckoutActivity.IsRunning = true;

            try
            {
                // Llamar al servicio que crea la orden (este método hace POST a pedidos + pedido_items)
                var createdOrder = await _supabase.CreateOrderAsync(userId, itemsPayload);

                if (createdOrder == null)
                    throw new Exception("El servicio no devolvió información del pedido creado.");
                // Si todo fue bien: limpiar carrito y mostrar confirmación
                _cart.Clear();

                // Mensaje de éxito con información básica del pedido
                var msg = $"Pedido {createdOrder?.Id} creado correctamente.\nTotal: {createdOrder?.Total:N2}";
                await DisplayAlert("Pedido confirmado", msg, "OK");

                // Navegar a página de historial o página principal según tu flujo
                await Shell.Current.GoToAsync("//main"); // ejemplo: navegar al root "main"
            }
            catch (Exception ex)
            {
                // Mostrar error amigable; en producción loggear detalles
                await DisplayAlert("Error", $"No se pudo crear el pedido: {ex.Message}", "OK");
            }
            finally
            {
                // Restaurar estado de UI
                PlaceOrderButton.IsEnabled = true;
                CheckoutActivity.IsRunning = false;
                CheckoutActivity.IsVisible = false;
            }
        }
    }
}