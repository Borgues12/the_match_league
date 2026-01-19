using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using juego_MVC_bomber.Helpers;
using juego_MVC_bomber.Models;

namespace juego_MVC_bomber.Controllers
{
    public class AdminController : Controller
    {
        private the_match_leagueEntities db = new the_match_leagueEntities();

        // =============================================
        // FILTRO: Solo administradores
        // =============================================
        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (!SecurityHelper.IsAuthenticated())
            {
                filterContext.Result = RedirectToAction("Login", "Account");
                return;
            }

            if (!SecurityHelper.IsAdmin())
            {
                filterContext.Result = RedirectToAction("Index", "Game");
                return;
            }

            // Cargar datos del admin para todas las vistas
            int adminId = SecurityHelper.GetCurrentUserId();
            var admin = db.USUARIOS.Find(adminId);

            if (admin != null)
            {
                ViewBag.AdminName = admin.Nombre;
                ViewBag.AdminEmail = admin.Email;

                if (admin.FotoPerfil != null && admin.FotoPerfil.Length > 0)
                {
                    ViewBag.AdminPhoto = $"data:image/jpeg;base64,{Convert.ToBase64String(admin.FotoPerfil)}";
                }
            }

            base.OnActionExecuting(filterContext);
        }

        // =============================================
        // DASHBOARD PRINCIPAL
        // =============================================

        // GET: /Admin/Index
        public ActionResult Index()
        {
            // Estadísticas generales
            ViewBag.TotalUsuarios = db.USUARIOS.Count();
            ViewBag.TotalLotes = db.LOTES.Count(l => l.Estado == "APROBADO");
            ViewBag.LotesPendientes = db.LOTES.Count(l => l.Estado == "PENDIENTE");
            ViewBag.TorneosActivos = db.TORNEOS.Count(t => t.Estado == "ESPERA" || t.Estado == "EN_JUEGO");
            ViewBag.PartidasHoy = db.RESULTADOS_JUEGO.Count(r => DbFunctions.TruncateTime(r.FechaJuego) == DbFunctions.TruncateTime(DateTime.Now));

            // Últimos usuarios registrados
            ViewBag.UltimosUsuarios = db.USUARIOS
                .OrderByDescending(u => u.FechaCreacion)
                .Take(5)
                .ToList();

            // Torneos activos
            ViewBag.TorneosRecientes = db.TORNEOS
                .Where(t => t.Estado == "ESPERA" || t.Estado == "EN_JUEGO")
                .OrderByDescending(t => t.FechaCreacion)
                .Take(5)
                .ToList();

            return View();
        }

        // =============================================
        // CRUD USUARIOS
        // =============================================

        // GET: /Admin/Usuarios
        public ActionResult Usuarios()
        {
            var usuarios = db.USUARIOS
                .Include(u => u.ROLES)
                .OrderByDescending(u => u.FechaCreacion)
                .ToList();

            return View(usuarios);
        }

        // GET: /Admin/UsuarioDetalle/5
        public ActionResult UsuarioDetalle(int id)
        {
            var usuario = db.USUARIOS
                .Include(u => u.ROLES)
                .FirstOrDefault(u => u.IdUsuario == id);

            if (usuario == null)
            {
                return HttpNotFound();
            }

            // Estadísticas del usuario
            ViewBag.PartidasJugadas = db.RESULTADOS_JUEGO.Count(r => r.IdUsuario == id);
            ViewBag.MejorPuntaje = db.RESULTADOS_JUEGO
                .Where(r => r.IdUsuario == id)
                .Max(r => (int?)r.Puntuacion) ?? 0;
            ViewBag.TorneosParticipados = db.TORNEO_PARTICIPANTES.Count(tp => tp.IdUsuario == id);

            return View(usuario);
        }

        // GET: /Admin/UsuarioEditar/5
        public ActionResult UsuarioEditar(int id)
        {
            var usuario = db.USUARIOS.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }

            ViewBag.Roles = new SelectList(db.ROLES.Where(r => r.Activo), "IdRol", "Nombre", usuario.IdRol);
            return View(usuario);
        }

        // POST: /Admin/UsuarioEditar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UsuarioEditar(int id, string nombre, int idRol, HttpPostedFileBase fotoPerfil)
        {
            var usuario = db.USUARIOS.Find(id);
            if (usuario == null)
            {
                return HttpNotFound();
            }

            usuario.Nombre = nombre;
            usuario.IdRol = idRol;

            // Actualizar foto si se subió una nueva
            if (fotoPerfil != null && fotoPerfil.ContentLength > 0)
            {
                using (var binaryReader = new BinaryReader(fotoPerfil.InputStream))
                {
                    usuario.FotoPerfil = binaryReader.ReadBytes(fotoPerfil.ContentLength);
                }
            }

            db.SaveChanges();
            TempData["Success"] = "Perfil actualizado correctamente";
            return RedirectToAction("Usuarios");
        }

        // POST: /Admin/UsuarioBloquear/5
        [HttpPost]
        public ActionResult UsuarioBloquear(int id)
        {
            var usuario = db.USUARIOS.Find(id);
            if (usuario != null)
            {
                usuario.Bloqueado = !usuario.Bloqueado;
                if (!usuario.Bloqueado)
                {
                    usuario.IntentosFallidos = 0;
                }
                db.SaveChanges();
            }
            return RedirectToAction("Usuarios");
        }

        // POST: /Admin/UsuarioEliminar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UsuarioEliminar(int id)
        {
            var usuario = db.USUARIOS.Find(id);
            if (usuario != null && usuario.IdUsuario != SecurityHelper.GetCurrentUserId())
            {
                // No eliminar, solo desactivar
                usuario.Activo = false;
                db.SaveChanges();
                TempData["Success"] = "Usuario desactivado correctamente";
            }
            return RedirectToAction("Usuarios");
        }

        // =============================================
        // CRUD LOTES DE IMÁGENES
        // =============================================

        // GET: /Admin/Lotes
        public ActionResult Lotes()
        {
            var lotes = db.LOTES
                .Include(l => l.USUARIOS)
                .Include(l => l.IMAGENES_LOTE)
                .OrderByDescending(l => l.FechaCreacion)
                .ToList();

            return View(lotes);
        }

        // GET: /Admin/LotesPendientes
        public ActionResult LotesPendientes()
        {
            var lotesPendientes = db.LOTES
                .Include(l => l.USUARIOS)
                .Include(l => l.IMAGENES_LOTE)
                .Where(l => l.Estado == "PENDIENTE")
                .OrderBy(l => l.FechaCreacion)
                .ToList();

            return View(lotesPendientes);
        }

        // GET: /Admin/LoteDetalle/5
        public ActionResult LoteDetalle(int id)
        {
            var lote = db.LOTES
                .Include(l => l.USUARIOS)
                .Include(l => l.IMAGENES_LOTE)
                .FirstOrDefault(l => l.IdLote == id);

            if (lote == null)
            {
                return HttpNotFound();
            }

            return View(lote);
        }

        // GET: /Admin/LoteCrear
        public ActionResult LoteCrear()
        {
            return View();
        }

        // POST: /Admin/LoteCrear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LoteCrear(string nombre, HttpPostedFileBase[] imagenes)
        {
            if (string.IsNullOrEmpty(nombre))
            {
                ViewBag.Error = "El nombre del lote es requerido";
                return View();
            }

            if (imagenes == null || imagenes.Length < 5 || imagenes.Any(i => i == null))
            {
                ViewBag.Error = "Debes subir exactamente 5 imágenes";
                return View();
            }

            // Crear el lote
            var nuevoLote = new LOTES
            {
                IdUsuario = SecurityHelper.GetCurrentUserId(),
                Nombre = nombre,
                Estado = "APROBADO", // Admin crea lotes aprobados directamente
                EsDefault = false,
                FechaCreacion = DateTime.Now,
                FechaAprobacion = DateTime.Now,
                AprobadoPor = SecurityHelper.GetCurrentUserId()
            };

            db.LOTES.Add(nuevoLote);
            db.SaveChanges();

            // Crear carpeta para las imágenes
            string carpetaLote = Server.MapPath($"~/Uploads/Lotes/{nuevoLote.IdLote}");
            if (!Directory.Exists(carpetaLote))
            {
                Directory.CreateDirectory(carpetaLote);
            }

            // Guardar las imágenes
            for (int i = 0; i < 5; i++)
            {
                var imagen = imagenes[i];
                string extension = Path.GetExtension(imagen.FileName);
                string nombreArchivo = $"{i + 1}{extension}";
                string rutaCompleta = Path.Combine(carpetaLote, nombreArchivo);

                imagen.SaveAs(rutaCompleta);

                var nuevaImagen = new IMAGENES_LOTE
                {
                    IdLote = nuevoLote.IdLote,
                    Orden = i + 1,
                    ImagenRuta = $"/Uploads/Lotes/{nuevoLote.IdLote}/{nombreArchivo}",
                    NombreOriginal = imagen.FileName
                };

                db.IMAGENES_LOTE.Add(nuevaImagen);
            }

            db.SaveChanges();
            TempData["Success"] = "Lote creado correctamente";
            return RedirectToAction("Lotes");
        }

        // POST: /Admin/LoteAprobar/5
        [HttpPost]
        public ActionResult LoteAprobar(int id)
        {
            var lote = db.LOTES.Find(id);
            if (lote != null && lote.Estado == "PENDIENTE")
            {
                lote.Estado = "APROBADO";
                lote.FechaAprobacion = DateTime.Now;
                lote.AprobadoPor = SecurityHelper.GetCurrentUserId();

                // Mover imágenes de Temp a Lotes
                string carpetaTemp = Server.MapPath($"~/Uploads/Temp/{lote.IdUsuario}");
                string carpetaLote = Server.MapPath($"~/Uploads/Lotes/{lote.IdLote}");

                if (Directory.Exists(carpetaTemp))
                {
                    if (!Directory.Exists(carpetaLote))
                    {
                        Directory.CreateDirectory(carpetaLote);
                    }

                    // Mover archivos y actualizar rutas
                    foreach (var imagen in lote.IMAGENES_LOTE)
                    {
                        string nombreArchivo = Path.GetFileName(imagen.ImagenRuta);
                        string rutaOrigen = Path.Combine(carpetaTemp, nombreArchivo);
                        string rutaDestino = Path.Combine(carpetaLote, nombreArchivo);

                        if (System.IO.File.Exists(rutaOrigen))
                        {
                            System.IO.File.Move(rutaOrigen, rutaDestino);
                        }

                        imagen.ImagenRuta = $"/Uploads/Lotes/{lote.IdLote}/{nombreArchivo}";
                    }

                    // Limpiar carpeta temporal
                    try { Directory.Delete(carpetaTemp, true); } catch { }
                }

                db.SaveChanges();
                TempData["Success"] = "Lote aprobado correctamente";
            }
            return RedirectToAction("LotesPendientes");
        }

        // POST: /Admin/LoteRechazar/5
        [HttpPost]
        public ActionResult LoteRechazar(int id)
        {
            var lote = db.LOTES
                .Include(l => l.IMAGENES_LOTE)
                .FirstOrDefault(l => l.IdLote == id);

            if (lote != null && lote.Estado == "PENDIENTE" && !lote.EsDefault)
            {
                // Eliminar archivos temporales
                string carpetaTemp = Server.MapPath($"~/Uploads/Temp/{lote.IdUsuario}");
                if (Directory.Exists(carpetaTemp))
                {
                    try { Directory.Delete(carpetaTemp, true); } catch { }
                }

                // Eliminar de la base de datos (CASCADE elimina las imágenes)
                db.LOTES.Remove(lote);
                db.SaveChanges();
                TempData["Success"] = "Lote rechazado y eliminado";
            }
            return RedirectToAction("LotesPendientes");
        }

        // POST: /Admin/LoteEliminar/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LoteEliminar(int id)
        {
            var lote = db.LOTES
                .Include(l => l.IMAGENES_LOTE)
                .FirstOrDefault(l => l.IdLote == id);

            if (lote != null && !lote.EsDefault)
            {
                // Eliminar carpeta de imágenes
                string carpetaLote = Server.MapPath($"~/Uploads/Lotes/{lote.IdLote}");
                if (Directory.Exists(carpetaLote))
                {
                    try { Directory.Delete(carpetaLote, true); } catch { }
                }

                db.LOTES.Remove(lote);
                db.SaveChanges();
                TempData["Success"] = "Lote eliminado correctamente";
            }
            return RedirectToAction("Lotes");
        }

        // POST: /Admin/LoteSetDefault/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LoteSetDefault(int id)
        {
            // Quitar default actual
            var loteActual = db.LOTES.FirstOrDefault(l => l.EsDefault);
            if (loteActual != null)
            {
                loteActual.EsDefault = false;
            }

            // Poner nuevo default
            var nuevoDefault = db.LOTES.Find(id);
            if (nuevoDefault != null && nuevoDefault.Estado == "APROBADO")
            {
                nuevoDefault.EsDefault = true;
                db.SaveChanges();
                TempData["Success"] = $"Lote '{nuevoDefault.Nombre}' seleccionado como activo";
            }

            return RedirectToAction("Lotes");
        }

        // =============================================
        // GESTIÓN DE TORNEOS
        // =============================================

        // GET: /Admin/Torneos
        public ActionResult Torneos()
        {
            var torneos = db.TORNEOS
                .Include(t => t.LOTES)
                .Include(t => t.TORNEO_PARTICIPANTES)
                .OrderByDescending(t => t.FechaCreacion)
                .ToList();

            return View(torneos);
        }

        // GET: /Admin/TorneoCrear
        public ActionResult TorneoCrear()
        {
            ViewBag.Lotes = new SelectList(
                db.LOTES.Where(l => l.Estado == "APROBADO"),
                "IdLote",
                "Nombre"
            );
            return View();
        }

        // POST: /Admin/TorneoCrear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult TorneoCrear(string nombre, int idLote, int maxJugadores = 10)
        {
            if (string.IsNullOrEmpty(nombre))
            {
                ViewBag.Error = "El nombre del torneo es requerido";
                ViewBag.Lotes = new SelectList(db.LOTES.Where(l => l.Estado == "APROBADO"), "IdLote", "Nombre");
                return View();
            }

            // Generar código único
            string codigo = GenerarCodigoTorneo();

            var nuevoTorneo = new TORNEOS
            {
                Codigo = codigo,
                Nombre = nombre,
                IdLote = idLote,
                IdAdmin = SecurityHelper.GetCurrentUserId(),
                Estado = "ESPERA",
                MaxJugadores = maxJugadores,
                FechaCreacion = DateTime.Now
            };

            db.TORNEOS.Add(nuevoTorneo);
            db.SaveChanges();

            TempData["Success"] = $"Torneo creado. Código: {codigo}";
            return RedirectToAction("TorneoDetalle", new { id = nuevoTorneo.IdTorneo });
        }

        // GET: /Admin/TorneoDetalle/5
        public ActionResult TorneoDetalle(int id)
        {
            var torneo = db.TORNEOS
                .Include(t => t.LOTES)
                .Include(t => t.LOTES.IMAGENES_LOTE)
                .Include(t => t.TORNEO_PARTICIPANTES.Select(tp => tp.USUARIOS))
                .FirstOrDefault(t => t.IdTorneo == id);

            if (torneo == null)
            {
                return HttpNotFound();
            }

            return View(torneo);
        }

        // POST: /Admin/TorneoIniciar/5
        [HttpPost]
        public ActionResult TorneoIniciar(int id)
        {
            var torneo = db.TORNEOS.Find(id);
            if (torneo != null && torneo.Estado == "ESPERA")
            {
                torneo.Estado = "EN_JUEGO";
                torneo.FechaInicio = DateTime.Now;
                db.SaveChanges();
                TempData["Success"] = "¡Torneo iniciado!";
            }
            return RedirectToAction("TorneoDetalle", new { id });
        }

        // POST: /Admin/TorneoFinalizar/5
        [HttpPost]
        public ActionResult TorneoFinalizar(int id)
        {
            var torneo = db.TORNEOS
                .Include(t => t.TORNEO_PARTICIPANTES)
                .FirstOrDefault(t => t.IdTorneo == id);

            if (torneo != null && torneo.Estado == "EN_JUEGO")
            {
                // Calcular posiciones
                var participantes = torneo.TORNEO_PARTICIPANTES
                    .OrderByDescending(p => p.Puntuacion ?? 0)
                    .ThenBy(p => p.Tiempo ?? int.MaxValue)
                    .ToList();

                int posicion = 1;
                foreach (var p in participantes)
                {
                    p.Posicion = posicion++;
                }

                torneo.Estado = "FINALIZADO";
                torneo.FechaFin = DateTime.Now;
                db.SaveChanges();
                TempData["Success"] = "Torneo finalizado";
            }
            return RedirectToAction("TorneoDetalle", new { id });
        }

        // POST: /Admin/TorneoCancelar/5
        [HttpPost]
        public ActionResult TorneoCancelar(int id)
        {
            var torneo = db.TORNEOS.Find(id);
            if (torneo != null && (torneo.Estado == "ESPERA" || torneo.Estado == "EN_JUEGO"))
            {
                torneo.Estado = "CANCELADO";
                db.SaveChanges();
                TempData["Success"] = "Torneo cancelado";
            }
            return RedirectToAction("Torneos");
        }

        // =============================================
        // MÉTODOS AUXILIARES
        // =============================================

        private string GenerarCodigoTorneo()
        {
            const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            string codigo;

            do
            {
                codigo = new string(Enumerable.Repeat(caracteres, 6)
                    .Select(s => s[random.Next(s.Length)]).ToArray());
            }
            while (db.TORNEOS.Any(t => t.Codigo == codigo));

            return codigo;
        }

        // GET: /Admin/GetParticipantes/5 (AJAX)
        public JsonResult GetParticipantes(int id)
        {
            var participantes = db.TORNEO_PARTICIPANTES
                .Where(tp => tp.IdTorneo == id)
                .Select(tp => new
                {
                    tp.IdUsuario,
                    Nombre = tp.USUARIOS.Nombre,
                    tp.Listo,
                    tp.Puntuacion,
                    tp.Tiempo,
                    tp.Completado,
                    tp.Posicion
                })
                .ToList();

            return Json(participantes, JsonRequestBehavior.AllowGet);
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
