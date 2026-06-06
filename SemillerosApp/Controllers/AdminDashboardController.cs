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

        public ActionResult Semilleros()
        {
            var semilleros = db.Semilleros
                .Include("Investigadores")
                .OrderBy(s => s.nombreSemillero)
                .ToList();

            ViewBag.Title = "Semilleros";
            return View(semilleros);
        }

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
            return RedirectToAction("Semilleros");
        }

        // POST: Eliminar semillero
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarSemillero(int id)
        {
            var semillero = db.Semilleros
                              .Include("Integrantes")
                              .FirstOrDefault(s => s.idSemillero == id);

            if (semillero == null) return HttpNotFound();

            string nombre = semillero.nombreSemillero;

            // Limpiar relación N:M antes de eliminar
            semillero.Integrantes.Clear();
            db.SaveChanges();

            db.Semilleros.Remove(semillero);
            db.SaveChanges();

            TempData["Mensaje"] = $"Semillero '{nombre}' eliminado correctamente.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Semilleros");
        }

        public ActionResult EditarSemillero(int id)
        {
            var semillero = db.Semilleros.Find(id);
            if (semillero == null) return HttpNotFound();

            ViewBag.Lideres = db.Investigadores
                .Where(i => i.tipoInvestigador == "Principal")
                .Select(i => new SelectListItem
                {
                    Value = i.idInvestigadores.ToString(),
                    Text = i.nombreInvestigador
                }).ToList();

            return View(semillero);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarSemillero(int idSemillero, string nombreSemillero,
            string lineaInvestigacion, string descripcionSemillero,
            int Investigadores_idInvestigadores)
        {
            var semillero = db.Semilleros.Find(idSemillero);
            if (semillero == null) return HttpNotFound();

            semillero.nombreSemillero = nombreSemillero;
            semillero.lineaInvestigacion = lineaInvestigacion;
            semillero.descripcionSemillero = descripcionSemillero;
            semillero.Investigadores_idInvestigadores = Investigadores_idInvestigadores;

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = $"Semillero '{nombreSemillero}' actualizado correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("Semilleros");
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

        // GET: Vista detalle de un semillero
        public ActionResult DetalleSemillero(int id)
        {
            var semillero = db.Semilleros
                .Include("Investigadores")
                .Include("Investigadores.Usuario")
                .Include("Proyectos")
                .Include("Proyectos.Fases")
                .Include("Proyectos.Fases.Actividades")
                .Include("Reunion")
                .Include("SemilleroEventos")
                .Include("SemilleroEventos.Eventos")
                .Include("Integrantes")
                .FirstOrDefault(s => s.idSemillero == id);

            if (semillero == null) return HttpNotFound();

            var vm = new DetalleSemilleroViewModel
            {
                Semillero = semillero,
                Integrantes = semillero.Integrantes?.ToList() ?? new List<Investigadores>(),
                TotalProyectos = semillero.Proyectos?.Count ?? 0,
                ProyectosActivos = semillero.Proyectos?.Count(p => p.estadoProyecto == "En Proceso") ?? 0,
                TotalFases = semillero.Proyectos?.SelectMany(p => p.Fases).Count() ?? 0,
                TotalActividades = semillero.Proyectos?.SelectMany(p => p.Fases)
                                                      .SelectMany(f => f.Actividades).Count() ?? 0,
                ActividadesPendientes = semillero.Proyectos?.SelectMany(p => p.Fases)
                                                           .SelectMany(f => f.Actividades)
                                                           .Count(a => a.estadoActividad == "Pendiente") ?? 0,
                PorcentajeActividad = CalcularPorcentaje(semillero)
            };

            return View(vm);
        }

        private int CalcularPorcentaje(Semillero s)
        {
            if (s.Proyectos == null || !s.Proyectos.Any()) return 0;
            var actividades = s.Proyectos.SelectMany(p => p.Fases ?? new List<Fase>())
                                         .SelectMany(f => f.Actividades ?? new List<Actividad>())
                                         .ToList();
            if (!actividades.Any()) return 0;
            int completadas = actividades.Count(a => a.estadoActividad == "Completada");
            return (int)Math.Round((double)completadas / actividades.Count * 100);
        }

        // ────────────────────────────────────────────────────────
        //  GESTIÓN DE USUARIOS (LÍDERES)
        // ────────────────────────────────────────────────────────

        public ActionResult Usuarios()
        {
            var lista = db.Usuarios
                .Where(u => u.rolUsuario == "Lider")
                .OrderBy(u => u.nombreUsuario)
                .ToList();
            return View(lista);
        }

        public ActionResult AgregarUsuario()
        {
            return View(new Usuario { estadoUsuario = "Activo", rolUsuario = "Lider" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AgregarUsuario(Usuario usuario)
        {
            if (db.Usuarios.Any(u => u.emailUsuario == usuario.emailUsuario))
                ModelState.AddModelError("emailUsuario", "Ya existe un usuario con ese email.");

            ModelState.Remove("estadoUsuario");
            ModelState.Remove("rolUsuario");

            if (ModelState.IsValid)
            {
                usuario.rolUsuario = "Lider";
                usuario.estadoUsuario = "Activo";
                db.Usuarios.Add(usuario);
                db.SaveChanges();

                // Crear automáticamente el Investigador Principal
                var investigador = new Investigadores
                {
                    Usuario_idUsuario = usuario.idUsuario,
                    tipoInvestigador = "Principal",
                    nombreInvestigador = usuario.nombreUsuario + " " + usuario.apellidoUsuario,
                    emailInvestigador = usuario.emailUsuario,
                    telefonoInvestigador = usuario.telefonoUsuario,
                    generoInvestigador = "Masculino" // valor por defecto, el lider puede cambiarlo en su perfil
                };
                db.Investigadores.Add(investigador);
                db.SaveChanges();

                TempData["Mensaje"] = $"Usuario '{usuario.nombreUsuario}' creado correctamente.";
                TempData["TipoMensaje"] = "success";
                return RedirectToAction("Usuarios");
            }
            return View(usuario);
        }

        public ActionResult EditarUsuario(int id)
        {
            var usuario = db.Usuarios.Find(id);
            if (usuario == null) return HttpNotFound();
            return View(usuario);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarUsuario(int idUsuario, string nombreUsuario, string apellidoUsuario,
            string emailUsuario, decimal telefonoUsuario, string nuevaContrasena)
        {
            var usuario = db.Usuarios.Find(idUsuario);
            if (usuario == null) return HttpNotFound();

            usuario.nombreUsuario = nombreUsuario;
            usuario.apellidoUsuario = apellidoUsuario;
            usuario.emailUsuario = emailUsuario;
            usuario.telefonoUsuario = telefonoUsuario;

            if (!string.IsNullOrWhiteSpace(nuevaContrasena))
                usuario.contrasenaUsuario = nuevaContrasena;

            // Sincronizar datos en el Investigador Principal
            var investigador = db.Investigadores
                .FirstOrDefault(i => i.Usuario_idUsuario == idUsuario && i.tipoInvestigador == "Principal");

            if (investigador != null)
            {
                investigador.nombreInvestigador = nombreUsuario + " " + apellidoUsuario;
                investigador.emailInvestigador = emailUsuario;
                investigador.telefonoInvestigador = telefonoUsuario;
            }

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = $"Usuario '{nombreUsuario}' actualizado correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("Usuarios");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult InhabilitarUsuario(int id)
        {
            var usuario = db.Usuarios.Find(id);
            if (usuario == null) return HttpNotFound();

            // Inhabilitar usuario
            usuario.estadoUsuario = usuario.estadoUsuario == "Activo" ? "Inactivo" : "Activo";

            // Inhabilitar o activar su semillero también
            var investigador = db.Investigadores
                .FirstOrDefault(i => i.Usuario_idUsuario == id && i.tipoInvestigador == "Principal");

            if (investigador != null)
            {
                var semillero = db.Semilleros
                    .FirstOrDefault(s => s.Investigadores_idInvestigadores == investigador.idInvestigadores);

                if (semillero != null)
                    semillero.estadoSemillero = usuario.estadoUsuario == "Inactivo" ? "Inactivo" : "Activo";
            }

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            string accion = usuario.estadoUsuario == "Inactivo" ? "inhabilitado" : "habilitado";
            TempData["Mensaje"] = $"Usuario '{usuario.nombreUsuario}' {accion} correctamente.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Usuarios");
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

        private void RecalcularEstadoFaseYProyecto(int idFase)
        {
            var fase = db.Fases
                .Include("Actividades")
                .FirstOrDefault(f => f.idFase == idFase);

            if (fase == null) return;

            var acts = fase.Actividades.ToList();
            var estadosFinales = new[] { "Completada", "Completada con Retraso", "Descartada" };

            if (!acts.Any())
                fase.estadoFase = "Pendiente";
            else if (acts.All(a => estadosFinales.Contains(a.estadoActividad)))
                fase.estadoFase = "Completada";
            else if (acts.Any(a => estadosFinales.Contains(a.estadoActividad) || a.estadoActividad == "En Proceso"))
                fase.estadoFase = "En Proceso";
            else
                fase.estadoFase = "Pendiente";

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            var proyecto = db.Proyectos
                .Include("Fases")
                .FirstOrDefault(p => p.idProyecto == fase.Proyecto_idProyecto);

            if (proyecto == null) return;
            if (proyecto.estadoProyecto == "Suspendido") return;

            var fases = proyecto.Fases.ToList();

            if (!fases.Any())
                proyecto.estadoProyecto = "En Proceso";
            else if (fases.All(f => f.estadoFase == "Completada"))
                proyecto.estadoProyecto = "Finalizado";
            else if (fases.Any(f => f.estadoFase == "En Proceso" || f.estadoFase == "Completada"))
                proyecto.estadoProyecto = "En Proceso";
            else
                proyecto.estadoProyecto = "Pendiente";

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }

        // ────────────────────────────────────────────────────────
        //  PROYECTOS
        // ────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearProyecto(int idSemillero, string tituloProyecto,
            string descripcionProyecto, string duracionProyecto)
        {
            var proyecto = new Proyecto
            {
                Semillero_idSemillero = idSemillero,
                tituloProyecto = tituloProyecto,
                descripcionProyecto = descripcionProyecto,
                duracionProyecto = duracionProyecto,
                fechainProyecto = DateTime.Now,
                estadoProyecto = "En Proceso"
            };
            db.Proyectos.Add(proyecto);
            db.SaveChanges();

            TempData["Mensaje"] = $"Proyecto '{tituloProyecto}' creado correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("DetalleSemillero", new { id = idSemillero });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarProyecto(int idProyecto, int idSemillero)
        {
            var proyecto = db.Proyectos.Find(idProyecto);
            if (proyecto == null) return HttpNotFound();

            string titulo = proyecto.tituloProyecto;
            db.Proyectos.Remove(proyecto);
            db.SaveChanges();

            TempData["Mensaje"] = $"Proyecto '{titulo}' eliminado.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("DetalleSemillero", new { id = idSemillero });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SuspenderProyecto(int idProyecto, int idSemillero)
        {
            var proyecto = db.Proyectos.Find(idProyecto);
            if (proyecto == null) return HttpNotFound();

            proyecto.estadoProyecto = proyecto.estadoProyecto == "Suspendido" ? "En Proceso" : "Suspendido";
            db.SaveChanges();

            TempData["Mensaje"] = $"Proyecto '{proyecto.tituloProyecto}' {proyecto.estadoProyecto}.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("DetalleSemillero", new { id = idSemillero });
        }

        // ────────────────────────────────────────────────────────
        //  FASES
        // ────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearFase(int idProyecto, int idSemillero,
        string descripcionFase, string fechaLimiteFase)
        {
            var fase = new Fase
            {
                Proyecto_idProyecto = idProyecto,
                descripcionFase = descripcionFase,
                fechaFase = DateTime.Now,
                fechaLimiteFase = DateTime.TryParse(fechaLimiteFase, out var fl) ? fl : (DateTime?)null,
                estadoFase = "Pendiente"
            };
            db.Fases.Add(fase);

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = "Fase agregada correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("DetalleSemillero", new { id = idSemillero });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarFase(int idFase, int idSemillero)
        {
            var fase = db.Fases.Find(idFase);
            if (fase == null) { return HttpNotFound(); }

            // Eliminar actividades relacionadas primero
            var actividades = db.Actividades.Where(a => a.Fase_idFase == idFase).ToList();
            db.Actividades.RemoveRange(actividades);

            db.Fases.Remove(fase);
            db.SaveChanges();

            TempData["Mensaje"] = "Fase eliminada.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("DetalleSemillero", new { id = idSemillero });
        }

        // ────────────────────────────────────────────────────────
        //  ACTIVIDADES
        // ────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearActividad(int idFase, int idSemillero, string nombreActividad,
            string descripActividad, string lugarActividad,
            string fechaActividad, string horaActividad,
            string fechaLimiteActividad, string horaLimiteActividad)
        {
            var actividad = new Actividad
            {
                Fase_idFase = idFase,
                nombreActividad = nombreActividad,
                descripActividad = descripActividad,
                lugarActividad = lugarActividad,
                horaActividad = horaActividad,
                horaLimiteActividad = horaLimiteActividad,
                fechaActividad = DateTime.TryParse(fechaActividad, out var fi) ? fi : DateTime.Now,
                fechaLimiteActividad = DateTime.TryParse(fechaLimiteActividad, out var fl) ? fl : (DateTime?)null,
                estadoActividad = "Pendiente"
            };
            db.Actividades.Add(actividad);

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = $"Actividad '{nombreActividad}' creada.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("DetalleSemillero", new { id = idSemillero });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstadoActividad(int idActividad, int idSemillero, string nuevoEstado)
        {
            var actividad = db.Actividades.Find(idActividad);
            if (actividad == null) return HttpNotFound();

            actividad.estadoActividad = nuevoEstado;

            // ── Deshabilitar validación para no fallar en campos Required ──
            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            RecalcularEstadoFaseYProyecto(actividad.Fase_idFase);

            TempData["Mensaje"] = $"Actividad actualizada a '{nuevoEstado}'.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("DetalleSemillero", new { id = idSemillero });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarActividad(int idActividad, int idSemillero)
        {
            var actividad = db.Actividades.Find(idActividad);
            if (actividad == null) return HttpNotFound();

            string nombre = actividad.nombreActividad;
            int idFase = actividad.Fase_idFase; // guardar ANTES de eliminar

            db.Actividades.Remove(actividad);
            db.SaveChanges();

            RecalcularEstadoFaseYProyecto(idFase); // recalcular después de eliminar

            TempData["Mensaje"] = $"Actividad '{nombre}' eliminada.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("DetalleSemillero", new { id = idSemillero });
        }
    }
}