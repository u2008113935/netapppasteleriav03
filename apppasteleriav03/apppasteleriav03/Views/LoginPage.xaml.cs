using System;
using Microsoft.Maui.Controls;
using apppasteleriav03.Services;

namespace apppasteleriav03.Views
{
    public partial class LoginPage : ContentPage
    {
        readonly string? _returnTo;

        public LoginPage(string? returnTo = null)
        {
            InitializeComponent();
            _returnTo = returnTo;
        }

        // Botón Ingresar
        async void OnLoginClicked(object sender, EventArgs e)
        {
            var email = EmailEntry.Text?.Trim();
            var password = PasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Error", "Ingrese correo y contraseña", "OK");
                return;
            }

            // Intentar iniciar sesión (AuthService debe implementar la lógica real)
            var ok = await AuthService.Instance.SignInAsync(email, password);
            if (!ok)
            {
                await DisplayAlert("Error", "Credenciales inválidas", "OK");
                return;
            }

            // Cargar/mergear carrito local para que el usuario recupere sus productos
            await CartService.Instance.LoadLocalAsync();

            // Volver a la pantalla previa:
            // Si la pagina fue abierta desde CartPage (returnTo == "cart"), al hacer PopAsync
            // regresará al CartPage existente en la pila. Si prefieres navegar de forma distinta,
            // podemos ajustar la navegación.
            await Navigation.PopAsync();
        }

        // Botón Crear cuenta (implementación mínima: mostrar mensaje o navegar a RegisterPage)
        async void OnRegisterClicked(object sender, EventArgs e)
        {
            await DisplayAlert("Crear cuenta", "Funcionalidad de registro pendiente (implementa RegisterPage).", "OK");
            // Si tienes una RegisterPage, haz:
            // await Navigation.PushAsync(new RegisterPage());
        }
    }
}