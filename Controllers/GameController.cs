using System;
using System.Linq;
using System.Web.Mvc;
using juego_MVC_bomber.Helpers;
using juego_MVC_bomber.Models;

namespace juego_MVC_bomber.Controllers
{
    public class GameController : Controller
    {
        private the_match_leagueEntities db = new the_match_leagueEntities();

        // =============================================
        // PÁGINA PRINCIPAL DEL JUEGO (Welcome)
        // =============================================

        // GET: /Game/Index
        public ActionResult Index()
        {
            if (!SecurityHelper.IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = SecurityHelper.GetCurrentUserId();
            var usuario = db.USUARIOS.Find(userId);

            if (usuario == null)
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.PlayerName = usuario.Nombre;
            ViewBag.PlayerEmail = usuario.Email;
            ViewBag.PlayerId = usuario.IdUsuario;

            if (usuario.FotoPerfil != null && usuario.FotoPerfil.Length > 0)
            {
                ViewBag.PlayerPhoto = $"data:image/jpeg;base64,{Convert.ToBase64String(usuario.FotoPerfil)}";
            }

            return View();
        }

        // =============================================
        // MODO SOLO
        // =============================================

        // GET: /Game/SoloGame
        public ActionResult SoloGame()
        {
            if (!SecurityHelper.IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = SecurityHelper.GetCurrentUserId();
            var usuario = db.USUARIOS.Find(userId);

            ViewBag.PlayerName = usuario.Nombre;
            ViewBag.PlayerId = usuario.IdUsuario;

            if (usuario.FotoPerfil != null && usuario.FotoPerfil.Length > 0)
            {
                ViewBag.PlayerPhoto = $"data:image/jpeg;base64,{Convert.ToBase64String(usuario.FotoPerfil)}";
            }

            return View();
        }

        // =============================================
        // API: Obtener imágenes del lote default
        // =============================================

        // GET: /Game/GetDefaultLoteImages
        [HttpGet]
        public JsonResult GetDefaultLoteImages()
        {
            try
            {
                // Buscar el lote default
                var loteDefault = db.LOTES
                    .Where(l => l.EsDefault == true && l.Estado == "APROBADO")
                    .FirstOrDefault();

                if (loteDefault == null)
                {
                    // Si no hay lote default, buscar cualquier lote aprobado
                    loteDefault = db.LOTES
                        .Where(l => l.Estado == "APROBADO")
                        .FirstOrDefault();
                }

                if (loteDefault == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = "No hay lotes disponibles",
                        images = new object[] { }
                    }, JsonRequestBehavior.AllowGet);
                }

                // Obtener las imágenes del lote
                var imagenes = db.IMAGENES_LOTE
                    .Where(i => i.IdLote == loteDefault.IdLote)
                    .OrderBy(i => i.Orden)
                    .Select(i => new
                    {
                        id = i.IdImagen,
                        orden = i.Orden,
                        url = i.ImagenRuta,
                        nombre = i.NombreOriginal
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    loteId = loteDefault.IdLote,
                    loteName = loteDefault.Nombre,
                    images = imagenes
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    images = new object[] { }
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // =============================================
        // API: Obtener imágenes de un lote específico
        // =============================================

        // GET: /Game/GetLoteImages/5
        [HttpGet]
        public JsonResult GetLoteImages(int id)
        {
            try
            {
                var lote = db.LOTES.Find(id);

                if (lote == null || lote.Estado != "APROBADO")
                {
                    return Json(new
                    {
                        success = false,
                        message = "Lote no encontrado o no aprobado"
                    }, JsonRequestBehavior.AllowGet);
                }

                var imagenes = db.IMAGENES_LOTE
                    .Where(i => i.IdLote == id)
                    .OrderBy(i => i.Orden)
                    .Select(i => new
                    {
                        id = i.IdImagen,
                        orden = i.Orden,
                        url = i.ImagenRuta,
                        nombre = i.NombreOriginal
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    loteId = lote.IdLote,
                    loteName = lote.Nombre,
                    images = imagenes
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message
                }, JsonRequestBehavior.AllowGet);
            }
        }

        // =============================================
        // API: Guardar resultado del juego
        // =============================================

        // POST: /Game/SaveResult
        [HttpPost]
        public JsonResult SaveResult(int puntuacion, int tiempo, int movimientos, bool completado, int? idLote)
        {
            try
            {
                if (!SecurityHelper.IsAuthenticated())
                {
                    return Json(new { success = false, message = "No autenticado" });
                }

                int userId = SecurityHelper.GetCurrentUserId();

                // Buscar lote default si no se especifica
                int loteId = idLote ?? db.LOTES
                    .Where(l => l.EsDefault == true)
                    .Select(l => l.IdLote)
                    .FirstOrDefault();

                if (loteId == 0)
                {
                    loteId = db.LOTES
                        .Where(l => l.Estado == "APROBADO")
                        .Select(l => l.IdLote)
                        .FirstOrDefault();
                }

                var resultado = new RESULTADOS_JUEGO
                {
                    IdUsuario = userId,
                    IdLote = loteId,
                    Puntuacion = puntuacion,
                    Tiempo = tiempo,
                    Movimientos = movimientos,
                    Completado = completado,
                    FechaJuego = DateTime.Now
                };

                db.RESULTADOS_JUEGO.Add(resultado);
                db.SaveChanges();

                // Obtener posición en ranking
                var posicion = db.RESULTADOS_JUEGO
                    .Where(r => r.Puntuacion > puntuacion)
                    .Count() + 1;

                return Json(new
                {
                    success = true,
                    resultId = resultado.IdResultado,
                    ranking = posicion,
                    message = "Resultado guardado"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // =============================================
        // API: Obtener ranking
        // =============================================

        // GET: /Game/GetRanking
        [HttpGet]
        public JsonResult GetRanking(int top = 10)
        {
            try
            {
                var ranking = db.RESULTADOS_JUEGO
                    .Where(r => r.Completado)
                    .OrderByDescending(r => r.Puntuacion)
                    .ThenBy(r => r.Tiempo)
                    .Take(top)
                    .Select(r => new
                    {
                        nombre = r.USUARIOS.Nombre,
                        puntuacion = r.Puntuacion,
                        tiempo = r.Tiempo,
                        fecha = r.FechaJuego
                    })
                    .ToList();

                return Json(new { success = true, ranking = ranking }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // =============================================
        // API: Estadísticas del usuario
        // =============================================

        // GET: /Game/GetUserStats
        [HttpGet]
        public JsonResult GetUserStats()
        {
            try
            {
                if (!SecurityHelper.IsAuthenticated())
                {
                    return Json(new { success = false }, JsonRequestBehavior.AllowGet);
                }

                int userId = SecurityHelper.GetCurrentUserId();

                var stats = new
                {
                    gamesPlayed = db.RESULTADOS_JUEGO.Count(r => r.IdUsuario == userId),
                    bestScore = db.RESULTADOS_JUEGO
                        .Where(r => r.IdUsuario == userId)
                        .Max(r => (int?)r.Puntuacion) ?? 0,
                    totalScore = db.RESULTADOS_JUEGO
                        .Where(r => r.IdUsuario == userId)
                        .Sum(r => (int?)r.Puntuacion) ?? 0,
                    wins = db.TORNEO_PARTICIPANTES
                        .Count(tp => tp.IdUsuario == userId && tp.Posicion == 1)
                };

                return Json(new { success = true, stats = stats }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // =============================================
        // MODO CAMPEONATO
        // =============================================

        // GET: /Game/Championship
        public ActionResult Championship()
        {
            if (!SecurityHelper.IsAuthenticated())
            {
                return RedirectToAction("Login", "Account");
            }

            int userId = SecurityHelper.GetCurrentUserId();
            var usuario = db.USUARIOS.Find(userId);

            ViewBag.PlayerName = usuario.Nombre;
            ViewBag.PlayerId = usuario.IdUsuario;

            if (usuario.FotoPerfil != null && usuario.FotoPerfil.Length > 0)
            {
                ViewBag.PlayerPhoto = $"data:image/jpeg;base64,{Convert.ToBase64String(usuario.FotoPerfil)}";
            }

            return View();
        }

        // =============================================
        // LIBERAR RECURSOS
        // =============================================

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