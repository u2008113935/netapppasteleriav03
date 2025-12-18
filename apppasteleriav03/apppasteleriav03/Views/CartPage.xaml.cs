using System;
using System.Linq;
using Microsoft.Maui.Controls;
using apppasteleriav03.Services;

namespace apppasteleriav03.Views
{
    public partial class CartPage : ContentPage
    {
        readonly CartService _cart = CartService.Instance;

        public CartPage()
        {
            InitializeComponent();

            // Configuro el BindingContext al singleton para que los Bindings en XAML funcionen:
            BindingContext = _cart;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            // Ya no es necesario forzar ItemsSource aquí si usamos BindingContext
            System.Diagnostics.Debug.WriteLine($"CartPage.OnAppearing -> Items.Count={_cart.Items.Count}");
            System.Diagnostics.Debug.WriteLine($"CartCollection.ItemsSource -> {CartCollection?.ItemsSource?.GetType().FullName ?? "(null)"}");

            int i = 0;
            foreach (var it in _cart.Items)
            {
                System.Diagnostics.Debug.WriteLine($"CartItem[{i}] Nombre: {it.Nombre} ImagenPath: {it.ImagenPath} Price:{it.Price} Qty:{it.Quantity}");
                i++;
            }
        }

        void OnIncreaseClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid productId)
            {
                var existing = _cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (existing != null) _cart.UpdateQuantity(productId, existing.Quantity + 1);
            }
        }

        void OnDecreaseClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid productId)
            {
                var existing = _cart.Items.FirstOrDefault(i => i.ProductId == productId);
                if (existing != null) _cart.UpdateQuantity(productId, existing.Quantity - 1);
            }
        }

        void OnRemoveClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is Guid productId)
                _cart.Remove(productId);
        }

        // Handler añadido para Confirmar Compra (implementación de ejemplo)
        async void OnConfirmPurchaseClicked(object sender, EventArgs e)
        {
            if (_cart.Items.Count == 0)
            {
                await DisplayAlert("Carrito vacío", "No hay productos en el carrito.", "OK");
                return;
            }

            // Aquí iría la lógica real de confirmar compra (navegar a pantalla de pago, crear Order, etc.)
            await DisplayAlert("Confirmar Compra", $"Total a pagar: S/ {_cart.Total:N2}", "OK");

            // Ejemplo: limpiar carrito después de confirmar compra
            //_cart.Clear();

            // Guardar carrito local antes de redirigir (para persistencia previa a login)
            try
            {
                await _cart.SaveLocalAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando carrito local: {ex.Message}");
            }

            // Comprobar autenticación
            if (!AuthService.Instance.IsAuthenticated)
            {
                // Navegar a LoginPage; le pasamos returnTo="cart" para que, al autenticarse,
                // pueda volver al carrito (LoginPage debe PopAsync() al finalizar).
                await Navigation.PushAsync(new LoginPage(returnTo: "cart"));
                return;
            }

            // Si ya está autenticado, ir a Checkout (suponiendo que existe CheckoutPage)
            await Navigation.PushAsync(new CheckoutPage());

        }
    }
}