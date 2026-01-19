using juego_MVC_bomber.Models;
using Microsoft.Owin.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using juego_MVC_bomber.Helpers;

namespace juego_MVC_bomber.Controllers
{
    public class AccountController : Controller
    {
        // Conexión a la base de datos
        private the_match_leagueEntities db = new the_match_leagueEntities();

        // Máximo de intentos fallidos antes de bloquear
        private const int MAX_INTENTOS = 3;

        // Propiedad para acceder a la autenticación OWIN
        private IAuthenticationManager AuthenticationManager
        {
            get { return HttpContext.GetOwinContext().Authentication; }
        }

        // =============================================
        // LOGIN TRADICIONAL
        // =============================================

        // GET: /Account/Login
        public ActionResult Login()
        {
            if (SecurityHelper.IsAuthenticated())
            {
                return RedirectByRole();
            }
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                ViewBag.Error = "Por favor ingrese email y contraseña";
                return View();
            }

            var usuario = db.USUARIOS.FirstOrDefault(u => u.Email == email);

            if (usuario == null)
            {
                ViewBag.Error = "Email o contraseña incorrectos";
                return View();
            }

            if (usuario.Bloqueado)
            {
                ViewBag.Error = "Tu cuenta está bloqueada. Contacta al administrador.";
                return View();
            }

            if (!usuario.Activo)
            {
                ViewBag.Error = "Tu cuenta está desactivada.";
                return View();
            }

            if (!SecurityHelper.VerifyPassword(password, usuario.Password))
            {
                usuario.IntentosFallidos++;

                if (usuario.IntentosFallidos >= MAX_INTENTOS)
                {
                    usuario.Bloqueado = true;
                    ViewBag.Error = "Cuenta bloqueada por múltiples intentos fallidos.";
                }
                else
                {
                    int intentosRestantes = MAX_INTENTOS - usuario.IntentosFallidos;
                    ViewBag.Error = $"Contraseña incorrecta. Te quedan {intentosRestantes} intento(s).";
                }

                db.SaveChanges();
                return View();
            }

            // LOGIN EXITOSO
            usuario.IntentosFallidos = 0;
            usuario.UltimoAcceso = DateTime.Now;
            db.SaveChanges();

            string rolNombre = usuario.ROLES.Nombre;

            SecurityHelper.CreateUserSession(
                usuario.IdUsuario,
                usuario.Nombre,
                usuario.Email,
                rolNombre
            );

            return RedirectByRole();
        }

        // =============================================
        // LOGIN CON GOOGLE
        // =============================================

        // GET: /Account/GoogleLogin
        [AllowAnonymous]
        public ActionResult GoogleLogin(string returnUrl)
        {
            // Guardar URL de retorno
            Session["ReturnUrl"] = returnUrl;

            // Solicitar autenticación a Google
            var properties = new AuthenticationProperties
            {
                RedirectUri = Url.Action("GoogleCallback", "Account", null, Request.Url.Scheme)
            };

            HttpContext.GetOwinContext().Authentication.Challenge(properties, "Google");
            return new HttpUnauthorizedResult();
        }

        // GET: /Account/GoogleCallback
        [AllowAnonymous]
        public ActionResult GoogleCallback()
        {
            // Obtener la identidad externa del usuario autenticado
            var authenticateResult = AuthenticationManager.AuthenticateAsync("ExternalCookie").Result;

            if (authenticateResult == null || authenticateResult.Identity == null)
            {
                TempData["Error"] = "Error al autenticar con Google. Intenta de nuevo.";
                return RedirectToAction("Login");
            }

            var identity = authenticateResult.Identity;

            // Extraer datos de Google desde los Claims
            var email = identity.FindFirst(ClaimTypes.Email)?.Value;
            var googleName = identity.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var googleId = identity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Buscar foto de perfil
            var googlePhoto = identity.FindFirst("urn:google:picture")?.Value
                           ?? identity.FindFirst("picture")?.Value
                           ?? "";

            // Cerrar la sesión externa
            AuthenticationManager.SignOut("ExternalCookie");

            if (string.IsNullOrEmpty(email))
            {
                TempData["Error"] = "No se pudo obtener el email de Google.";
                return RedirectToAction("Login");
            }

            // Buscar si el usuario ya existe
            var usuario = db.USUARIOS.FirstOrDefault(u => u.Email == email);

            if (usuario != null)
            {
                // Usuario existe - Iniciar sesión directamente
                if (usuario.Bloqueado)
                {
                    TempData["Error"] = "Tu cuenta está bloqueada.";
                    return RedirectToAction("Login");
                }

                usuario.UltimoAcceso = DateTime.Now;
                db.SaveChanges();

                SecurityHelper.CreateUserSession(
                    usuario.IdUsuario,
                    usuario.Nombre,
                    usuario.Email,
                    usuario.ROLES.Nombre
                );

                return RedirectByRole();
            }
            else
            {
                // Usuario nuevo - Guardar datos en sesión y mostrar formulario
                Session["GoogleEmail"] = email;
                Session["GoogleName"] = googleName;
                Session["GooglePhoto"] = googlePhoto;

                return RedirectToAction("CompleteRegistration");
            }
        }

        // =============================================
        // COMPLETAR REGISTRO (después de Google)
        // =============================================

        // GET: /Account/CompleteRegistration
        public ActionResult CompleteRegistration()
        {
            // Verificar que venga de Google
            if (Session["GoogleEmail"] == null)
            {
                return RedirectToAction("Login");
            }

            ViewBag.Email = Session["GoogleEmail"];
            ViewBag.SuggestedName = Session["GoogleName"];
            ViewBag.GooglePhoto = Session["GooglePhoto"];

            return View();
        }

        // POST: /Account/CompleteRegistration
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CompleteRegistration(string nombreUsuario, HttpPostedFileBase fotoPerfil)
        {
            string email = Session["GoogleEmail"]?.ToString();
            string googleName = Session["GoogleName"]?.ToString();

            if (string.IsNullOrEmpty(email))
            {
                return RedirectToAction("Login");
            }

            // Validar nombre de usuario
            if (string.IsNullOrEmpty(nombreUsuario) || nombreUsuario.Length < 3)
            {
                ViewBag.Error = "El nombre de usuario debe tener al menos 3 caracteres";
                ViewBag.Email = email;
                ViewBag.SuggestedName = googleName;
                return View();
            }

            // Procesar foto de perfil
            byte[] fotoBytes = null;

            if (fotoPerfil != null && fotoPerfil.ContentLength > 0)
            {
                // El usuario subió una foto
                using (var binaryReader = new System.IO.BinaryReader(fotoPerfil.InputStream))
                {
                    fotoBytes = binaryReader.ReadBytes(fotoPerfil.ContentLength);
                }
            }
            else
            {
                // Usar foto por defecto
                try
                {
                    string defaultPhotoPath = Server.MapPath("~/Assets/images/default_avatar.jpg");
                    if (System.IO.File.Exists(defaultPhotoPath))
                    {
                        fotoBytes = System.IO.File.ReadAllBytes(defaultPhotoPath);
                    }
                }
                catch
                {
                    // Si falla, dejar sin foto
                    fotoBytes = null;
                }
            }

            // Crear nuevo usuario
            var nuevoUsuario = new USUARIOS
            {
                Nombre = nombreUsuario,
                Email = email,
                Password = SecurityHelper.HashPassword(Guid.NewGuid().ToString()), // Password aleatorio
                FotoPerfil = fotoBytes,
                IdRol = 2, // USUARIO
                Activo = true,
                Bloqueado = false,
                IntentosFallidos = 0,
                FechaCreacion = DateTime.Now,
                UltimoAcceso = DateTime.Now
            };

            db.USUARIOS.Add(nuevoUsuario);
            db.SaveChanges();

            // Limpiar sesión de Google
            Session.Remove("GoogleEmail");
            Session.Remove("GoogleName");
            Session.Remove("GooglePhoto");

            // Crear sesión de usuario
            SecurityHelper.CreateUserSession(
                nuevoUsuario.IdUsuario,
                nuevoUsuario.Nombre,
                nuevoUsuario.Email,
                "USUARIO"
            );

            TempData["Success"] = "¡Bienvenido a The Match League!";
            return RedirectToAction("Index", "Game");
        }

        // =============================================
        // REGISTRO TRADICIONAL
        // =============================================

        // GET: /Account/Register
        public ActionResult Register()
        {
            if (SecurityHelper.IsAuthenticated())
            {
                return RedirectByRole();
            }
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Register(string nombre, string email, string password, string confirmPassword, HttpPostedFileBase fotoPerfil)
        {
            if (string.IsNullOrEmpty(nombre) || string.IsNullOrEmpty(email) ||
                string.IsNullOrEmpty(password) || string.IsNullOrEmpty(confirmPassword))
            {
                ViewBag.Error = "Todos los campos son obligatorios";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Error = "Las contraseñas no coinciden";
                return View();
            }

            if (password.Length < 6)
            {
                ViewBag.Error = "La contraseña debe tener al menos 6 caracteres";
                return View();
            }

            if (db.USUARIOS.Any(u => u.Email == email))
            {
                ViewBag.Error = "Este email ya está registrado";
                return View();
            }

            // Procesar foto de perfil
            byte[] fotoBytes = null;
            if (fotoPerfil != null && fotoPerfil.ContentLength > 0)
            {
                using (var binaryReader = new System.IO.BinaryReader(fotoPerfil.InputStream))
                {
                    fotoBytes = binaryReader.ReadBytes(fotoPerfil.ContentLength);
                }
            }

            var nuevoUsuario = new USUARIOS
            {
                Nombre = nombre,
                Email = email,
                Password = SecurityHelper.HashPassword(password),
                FotoPerfil = fotoBytes,
                IdRol = 2,
                Activo = true,
                Bloqueado = false,
                IntentosFallidos = 0,
                FechaCreacion = DateTime.Now
            };

            db.USUARIOS.Add(nuevoUsuario);
            db.SaveChanges();

            TempData["Success"] = "¡Registro exitoso! Ahora puedes iniciar sesión.";
            return RedirectToAction("Login");
        }

        // =============================================
        // LOGOUT
        // =============================================

        // GET: /Account/Logout
        public ActionResult Logout()
        {
            AuthenticationManager.SignOut("ApplicationCookie", "ExternalCookie");
            SecurityHelper.DestroyUserSession();
            return RedirectToAction("Login");
        }

        // =============================================
        // MÉTODOS AUXILIARES
        // =============================================

        private ActionResult RedirectByRole()
        {
            if (SecurityHelper.IsAdmin())
            {
                return RedirectToAction("Index", "Admin");
            }
            else
            {
                return RedirectToAction("Index", "Game");
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}