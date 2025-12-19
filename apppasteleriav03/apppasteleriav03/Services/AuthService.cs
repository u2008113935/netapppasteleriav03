using System;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using System.Diagnostics;

namespace apppasteleriav03.Services
{
    // Modelo simple de usuario autenticado
    public class AuthUser
    {
        public string? Id { get; set; }
        public string? Email { get; set; }
    }
    
    // Servicio de autenticación por llamadas reales a Supabase REST.
    public class AuthService
    {
        public static AuthService Instance { get; } = new AuthService();

        //Claves para el SecureStorage
        const string TokenKey = "auth_token";
        const string RefreshKey = "auth_refresh";
        const string UserIdKey = "auth_user_id";

        //Estado en memoria de la sesión
        public string? AccessToken { get; private set; }

        //Identificador del usuario autenticado
        public string? UserId { get; private set; }

        //Correo electrónico del usuario autenticado
        public string UserEmail { get; set; }

        // Indica si el token esta cargado en memoria 
        public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);

        //Constructor
        private AuthService()
        {
        }

        // Método de ejemplo: reemplazar con SupabaseAuth o tu API
        public async Task<bool> SignInAsync(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                Debug.WriteLine("AuthService.SignInAsync: email o password vacíos");
                return false;
            }

            try
            {

                //Llamar al servicio de Supabase para autenticar
                var res = await SupabaseService.Instance.SignInAsync(email.Trim(), password);
                if (!res.Success)
                {
                    Debug.WriteLine($"AuthService.SignInAsync: supabase error: {res.Error}");
                    return false;
                }

                AccessToken = res.AccessToken;  // Guardar AccessToken en memoria
                UserId = res.UserId?.ToString(); // Guardar UserId como string
                UserEmail = email.Trim(); // Guardar email en memoria


                try
                {
                    // Guardar en almacenamiento seguro 
                    if (!string.IsNullOrEmpty(AccessToken))
                        await SecureStorage.Default.SetAsync(TokenKey, AccessToken);

                    if (!string.IsNullOrEmpty(UserId))
                        await SecureStorage.Default.SetAsync(UserIdKey, UserId);

                    //Refresh token si está disponible
                    if (!string.IsNullOrWhiteSpace(res.RefreshToken))
                        await SecureStorage.Default.SetAsync(RefreshKey, res.RefreshToken);

                    //Informar a supabaservice que use el token recien obtenido
                    SupabaseService.Instance.SetUserToken(AccessToken);

                }
                catch (Exception secEx)
                {
                    Debug.WriteLine($"AuthServive.SignInAsync: SecureStorage error: {secEx.Message}");
                }

                return true;

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AuthService.SignInAsync: exception: {ex.Message}");
                return false;
            }

        }


        public async Task LoadFromStorageAsync()
        {
            try
            {
                AccessToken = await SecureStorage.Default.GetAsync(TokenKey);
                UserId = await SecureStorage.Default.GetAsync(UserIdKey);

                if(!string.IsNullOrWhiteSpace(AccessToken))
                    SupabaseService.Instance.SetUserToken(AccessToken);
            }
            catch
            {
                
            }
        }

        // Simulación mínima de registro. Reemplaza con SupabaseService.SignUp y manejo real.
        public async Task<bool> SignUpAsync(string email, string password, string name, string? phone = null)
        {
            // TODO: Llamar SupabaseService.Instance.SignUp(...) y tratar respuesta
            // En esta versión de desarrollo devolvemos true para permitir flujo.
            // Guarda token/usuario si la API retorna directamente el token.
            await Task.Delay(300);
            return true;
        }

        public void Logout()
        {
            AccessToken = null;
            UserId = null;
            UserEmail = null;

            try
            {
                SecureStorage.Default.Remove(TokenKey);
                SecureStorage.Default.Remove(UserIdKey);
                SecureStorage.Default.Remove(RefreshKey);
            }
            catch
            {
                //ignorar errores de SecureStorage
            }
            // Informar a SupabaseService que no hay token
            SupabaseService.Instance.SetUserToken(null);
        }

        public async Task SignOutAsync()
        {
            Logout();
            await Task.CompletedTask;
        }

        //Devuelve el access token en  memoria o lo carga del SecureStorage
        public async Task<string?> GetAccessTokenAsync()
        {
            //Si ya lo tenemos en memoria, devolverlo
            if (!string.IsNullOrWhiteSpace(AccessToken))
                return AccessToken;

            //Si no, intentar cargarlo del SecureStorage
            try
            {
                //TokenKey debe ser el mismo que usamos en SignInAsync                
                var tokenFromStorage = await SecureStorage.Default.GetAsync(TokenKey);
                return tokenFromStorage;
            }
            catch
            {
                // Falla del SecureStorage
                return null;
            }

        }
    }
}