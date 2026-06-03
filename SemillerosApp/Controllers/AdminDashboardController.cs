using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SemillerosApp.Models;
using SemillerosApp.Filters;

namespace SemillerosApp.Controllers
{
    [RoleAuthorize("Admin")]
    public class AdminDashboardController : Controller
    {
        private SemillerosContext db = new SemillerosContext();

        // Helper para obtener el ID del usuario logueado desde el ticket
        private int GetUsuarioId()
        {
            var identity = User.Identity as System.Web.Security.FormsIdentity;
            string userData = identity?.Ticket.UserData ?? "";
            string[] parts = userData.Split('|');
            return parts.Length >= 1 ? int.Parse(parts[0]) : 0;
        }

        // GET: /AdminDashboard/
        public ActionResult Index()
        {
            var vm = new AdminDashboardViewModel();

            // ── Estadísticas generales ────────────────────────────
            vm.TotalSemilleros = db.Semilleros.Count();
            vm.SemillerosActivos = db.Semilleros.Count(s => s.estadoSemillero == "Activo");
            vm.SemillerosInactivos = db.Semilleros.Count(s => s.estadoSemillero == "Inactivo");
            vm.TotalInvestigadores = db.Investigadores.Count();
            vm.TotalUsuarios = db.Usuarios.Count();
            vm.TotalProyectos = db.Proyectos.Count();

            // ── Lista de semilleros con su líder ──────────────────
            vm.Semilleros = db.Semilleros
                .Include("Investigadores")
                .Include("Investigadores.Usuario")
                .OrderByDescending(s => s.fechacreaSemillero)
                .ToList();

            // ── Datos para gráfica: semilleros por estado ─────────
            vm.ChartEstados = db.Semilleros
                .GroupBy(s => s.estadoSemillero)
                .Select(g => new ChartItem { Label = g.Key, Value = g.Count() })
                .ToList();

            // ── Datos para gráfica: semilleros por línea de investigación ──
            vm.ChartLineas = db.Semilleros
                .GroupBy(s => s.lineaInvestigacion)
                .Select(g => new ChartItem { Label = g.Key, Value = g.Count() })
                .ToList();

            return View(vm);
        }

        // ────────────────────────────────────────────────────────
        //  GESTIÓN DE SEMILLEROS
        // ────────────────────────────────────────────────────────

        // POST: Activar/Desactivar semillero
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleEstadoSemillero(int id)
        {
            var semillero = db.Semilleros.Find(id);
            if (semillero == null) return HttpNotFound();

            semillero.estadoSemillero = semillero.estadoSemillero == "Activo" ? "Inactivo" : "Activo";
            db.SaveChanges();

            TempData["Mensaje"] = $"Semillero '{semillero.nombreSemillero}' ahora está {semillero.estadoSemillero}.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("Index");
        }

        // POST: Eliminar semillero
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarSemillero(int id)
        {
            var semillero = db.Semilleros.Find(id);
            if (semillero == null) return HttpNotFound();

            string nombre = semillero.nombreSemillero;
            db.Semilleros.Remove(semillero);
            db.SaveChanges();

            TempData["Mensaje"] = $"Semillero '{nombre}' eliminado correctamente.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Index");
        }

        // GET: Formulario para crear semillero (con asignación de líder)
        public ActionResult CrearSemillero()
        {
            // Trae los investigadores que son líderes (tipo Principal)
            ViewBag.Lideres = db.Investigadores
                .Where(i => i.tipoInvestigador == "Principal")
                .Select(i => new { i.idInvestigadores, i.nombreInvestigador })
                .ToList()
                .Select(i => new SelectListItem
                {
                    Value = i.idInvestigadores.ToString(),
                    Text = i.nombreInvestigador
                }).ToList();

            return View(new Semillero { estadoSemillero = "Activo", fechacreaSemillero = DateTime.Now });
        }

        // POST: Crear semillero
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearSemillero(Semillero semillero)
        {
            if (ModelState.IsValid)
            {
                semillero.fechacreaSemillero = DateTime.Now;
                semillero.estadoSemillero = "Activo";
                db.Semilleros.Add(semillero);
                db.SaveChanges();

                TempData["Mensaje"] = "Semillero creado correctamente.";
                TempData["TipoMensaje"] = "success";
                return RedirectToAction("Index");
            }

            ViewBag.Lideres = db.Investigadores
                .Where(i => i.tipoInvestigador == "Principal")
                .Select(i => new SelectListItem
                {
                    Value = i.idInvestigadores.ToString(),
                    Text = i.nombreInvestigador
                }).ToList();

            return View(semillero);
        }

        // ────────────────────────────────────────────────────────
        //  GESTIÓN DE INVESTIGADORES
        // ────────────────────────────────────────────────────────

        // GET: Listado de investigadores
        public ActionResult Investigadores()
        {
            var lista = db.Investigadores
                .Include("Usuario")
                .OrderBy(i => i.nombreInvestigador)
                .ToList();
            return View(lista);
        }

        // GET: Editar investigador
        public ActionResult EditarInvestigador(int id)
        {
            var inv = db.Investigadores.Find(id);
            if (inv == null) return HttpNotFound();

            ViewBag.Tipos = new List<SelectListItem>
            {
                new SelectListItem { Value = "Principal",   Text = "Principal (Líder)" },
                new SelectListItem { Value = "Semillerista", Text = "Semillerista" }
            };
            ViewBag.Generos = new List<SelectListItem>
            {
                new SelectListItem { Value = "Masculino",  Text = "Masculino" },
                new SelectListItem { Value = "Femenino",   Text = "Femenino" },
                new SelectListItem { Value = "Otro",       Text = "Otro" }
            };
            return View(inv);
        }

        // POST: Editar investigador
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarInvestigador(Investigadores inv)
        {
            if (ModelState.IsValid)
            {
                db.Entry(inv).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();

                TempData["Mensaje"] = "Investigador actualizado correctamente.";
                TempData["TipoMensaje"] = "success";
                return RedirectToAction("Investigadores");
            }

            ViewBag.Tipos = new List<SelectListItem>
            {
                new SelectListItem { Value = "Principal",    Text = "Principal (Líder)" },
                new SelectListItem { Value = "Semillerista", Text = "Semillerista" }
            };
            ViewBag.Generos = new List<SelectListItem>
            {
                new SelectListItem { Value = "Masculino", Text = "Masculino" },
                new SelectListItem { Value = "Femenino",  Text = "Femenino" },
                new SelectListItem { Value = "Otro",      Text = "Otro" }
            };
            return View(inv);
        }

        // ────────────────────────────────────────────────────────
        //  PERFIL DEL ADMIN
        // ────────────────────────────────────────────────────────
        public ActionResult MiPerfil()
        {
            int id = GetUsuarioId();
            var usuario = db.Usuarios.Find(id);
            if (usuario == null) return HttpNotFound();
            return View(usuario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult MiPerfil(Usuario usuario)
        {
            // Ignorar validaciones de campos que no se editan en el perfil
            ModelState.Remove("estadoUsuario");
            ModelState.Remove("rolUsuario");

            if (ModelState.IsValid)
            {
                var original = db.Usuarios.Find(usuario.idUsuario);
                original.nombreUsuario = usuario.nombreUsuario;
                original.apellidoUsuario = usuario.apellidoUsuario;
                original.emailUsuario = usuario.emailUsuario;
                original.telefonoUsuario = usuario.telefonoUsuario;
                if (!string.IsNullOrWhiteSpace(usuario.contrasenaUsuario))
                    original.contrasenaUsuario = usuario.contrasenaUsuario;

                db.SaveChanges();
                TempData["Mensaje"] = "Perfil actualizado correctamente.";
                TempData["TipoMensaje"] = "success";
                return RedirectToAction("MiPerfil");
            }
            return View(usuario);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}