using System;
using Microsoft.Maui.Controls;
using apppasteleriav03.Services;

namespace apppasteleriav03.Views
{
    public partial class RegisterPage : ContentPage
    {
        public RegisterPage()
        {
            InitializeComponent();
        }

        async void OnRegisterClicked(object sender, EventArgs e)
        {
            var name = NameEntry.Text?.Trim();
            var email = EmailEntry.Text?.Trim();
            var phone = PhoneEntry.Text?.Trim();
            var password = PasswordEntry.Text;

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Error", "Complete los campos obligatorios", "OK");
                return;
            }

            // TODO: Reemplazar por llamada real a SupabaseService.SignUpAsync(...)
            var ok = await AuthService.Instance.SignUpAsync(email, password, name, phone);
            if (!ok)
            {
                await DisplayAlert("Error", "No se pudo crear la cuenta", "OK");
                return;
            }

            await DisplayAlert("Éxito", "Cuenta creada. Ya puedes iniciar sesión.", "OK");

            // Navegar de vuelta a LoginPage
            await Navigation.PopAsync();
        }

        async void OnLoginClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
    }
}