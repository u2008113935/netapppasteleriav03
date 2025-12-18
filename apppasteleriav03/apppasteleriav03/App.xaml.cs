using apppasteleriav03.Services;
using Microsoft.Maui;              
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;

namespace apppasteleriav03
{
    public partial class App : Application
    {
        public static SupabaseService Database { get; } = new SupabaseService();

        public App()
        {
            InitializeComponent();

       
            // Inicialización asíncrona (por ejemplo: cargar token)
            InitializeAsync();

            MainPage = new AppShell();
        }

        async void InitializeAsync()
        {
            try
            {
                await AuthService.Instance.LoadFromStorageAsync();
                var token = await AuthService.Instance.GetAccessTokenAsync();
                if (!string.IsNullOrWhiteSpace(token))
                    SupabaseService.Instance.SetUserToken(token);
            }
            catch { }
        }
    }
}