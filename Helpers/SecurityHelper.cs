using System;
using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace juego_MVC_bomber.Helpers
{
    public static class SecurityHelper
    {
        // =============================================
        // ENCRIPTACIÓN DE CONTRASEÑAS
        // =============================================

        /// <summary>
        /// Encripta una contraseña usando SHA256
        /// </summary>
        public static string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();

                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        /// <summary>
        /// Verifica si una contraseña coincide con su hash
        /// </summary>
        public static bool VerifyPassword(string password, string hash)
        {
            string passwordHash = HashPassword(password);
            return passwordHash.Equals(hash, StringComparison.OrdinalIgnoreCase);
        }

        // =============================================
        // MANEJO DE SESIONES
        // =============================================

        /// <summary>
        /// Guarda los datos del usuario en sesión después del login
        /// </summary>
        public static void CreateUserSession(int idUsuario, string nombre, string email, string rol)
        {
            HttpContext.Current.Session["IdUsuario"] = idUsuario;
            HttpContext.Current.Session["Nombre"] = nombre;
            HttpContext.Current.Session["Email"] = email;
            HttpContext.Current.Session["Rol"] = rol;
            HttpContext.Current.Session["IsAuthenticated"] = true;
        }

        /// <summary>
        /// Destruye la sesión del usuario (logout)
        /// </summary>
        public static void DestroyUserSession()
        {
            HttpContext.Current.Session.Clear();
            HttpContext.Current.Session.Abandon();
        }

        /// <summary>
        /// Verifica si hay una sesión activa
        /// </summary>
        public static bool IsAuthenticated()
        {
            return HttpContext.Current.Session["IsAuthenticated"] != null
                   && (bool)HttpContext.Current.Session["IsAuthenticated"] == true;
        }

        /// <summary>
        /// Obtiene el ID del usuario actual
        /// </summary>
        public static int GetCurrentUserId()
        {
            if (HttpContext.Current.Session["IdUsuario"] != null)
            {
                return (int)HttpContext.Current.Session["IdUsuario"];
            }
            return 0;
        }

        /// <summary>
        /// Obtiene el nombre del usuario actual
        /// </summary>
        public static string GetCurrentUserName()
        {
            return HttpContext.Current.Session["Nombre"]?.ToString() ?? "";
        }

        /// <summary>
        /// Obtiene el rol del usuario actual
        /// </summary>
        public static string GetCurrentUserRole()
        {
            return HttpContext.Current.Session["Rol"]?.ToString() ?? "";
        }

        /// <summary>
        /// Verifica si el usuario actual es ADMIN
        /// </summary>
        public static bool IsAdmin()
        {
            return GetCurrentUserRole() == "ADMIN";
        }

        /// <summary>
        /// Verifica si el usuario actual es USUARIO
        /// </summary>
        public static bool IsUser()
        {
            return GetCurrentUserRole() == "USUARIO";
        }
    }
}