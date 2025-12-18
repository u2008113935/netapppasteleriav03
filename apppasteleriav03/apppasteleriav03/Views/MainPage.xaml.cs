using apppasteleriav03.Services;
using apppasteleriav03.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.Controls;
using System.Threading.Tasks;
using System;

namespace apppasteleriav03.Views
{
    public partial class MainPage : ContentPage
    {
        // servicio supabase 
        readonly SupabaseService _supabase = new SupabaseService();

        // servicio carrito (singleton)
        readonly CartService _cart = CartService.Instance;

        // lista de todos los productos cargados
        List<Product> _allProducts = new List<Product>();

        public MainPage()
        {
            InitializeComponent(); // carga la interfaz de usuario desde el archivo XAML
            _ = LoadProductsAsync(); // carga los productos desde el servicio Supabase
        }

        async Task LoadProductsAsync()
        {
            try
            {
                LoadingIndicator.IsRunning = true;
                LoadingIndicator.IsVisible = true;

                var products = await _supabase.GetProductsAsync();
                _allProducts = products ?? new List<Product>();

                ProductsCollection.ItemsSource = _allProducts;
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudieron cargar los productos: {ex.Message}", "OK");
            }
            finally
            {
                LoadingIndicator.IsRunning = false;
                LoadingIndicator.IsVisible = false;
            }
        }

        void OnAddToCartClicked(object sender, System.EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is Product product)
            {
                CartService.Instance.Add(product, 1);
                System.Diagnostics.Debug.WriteLine($"Producto agregado al carrito: {product.Nombre}");
                System.Diagnostics.Debug.WriteLine($"Cart items agregado  : {CartService.Instance.Items.Count}");
            }
        }

        async void OnCartClicked(object sender, System.EventArgs e)
        {
            await Shell.Current.GoToAsync("cart");
        }

        void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var text = e.NewTextValue?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                ProductsCollection.ItemsSource = _allProducts;
                return;
            }

            var filteredProducts = _allProducts
                .Where(p =>
                    (!string.IsNullOrEmpty(p.Nombre) && p.Nombre.Contains(text, StringComparison.OrdinalIgnoreCase))
                    ||
                    (!string.IsNullOrEmpty(p.Descripcion) && p.Descripcion.Contains(text, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            ProductsCollection.ItemsSource = filteredProducts;
        }

        async Task DisplayToastAsync(string message)
        {
            await DisplayAlert(null, message, "OK");
        }
    }
}
