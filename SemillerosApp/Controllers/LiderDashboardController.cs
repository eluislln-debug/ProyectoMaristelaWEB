using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using SemillerosApp.Models;
using SemillerosApp.Filters;

namespace SemillerosApp.Controllers
{
    [RoleAuthorize("Lider")]
    public class LiderDashboardController : Controller
    {
        private SemillerosContext db = new SemillerosContext();

        // Obtiene el idUsuario del ticket de sesión
        private int GetUsuarioId()
        {
            var identity = User.Identity as System.Web.Security.FormsIdentity;
            string userData = identity?.Ticket.UserData ?? "";
            string[] parts = userData.Split('|');
            return parts.Length >= 1 ? int.Parse(parts[0]) : 0;
        }

        // Obtiene el Investigador (lider) asociado al usuario logueado
        private Investigadores GetInvestigadorLider()
        {
            int uid = GetUsuarioId();
            return db.Investigadores.FirstOrDefault(i =>
                i.Usuario_idUsuario == uid && i.tipoInvestigador == "Principal");
        }

        // Obtiene el Semillero del lider logueado
        private Semillero GetSemilleroDelLider()
        {
            var inv = GetInvestigadorLider();
            if (inv == null) return null;
            return db.Semilleros
                     .Include("Investigadores")
                     .FirstOrDefault(s => s.Investigadores_idInvestigadores == inv.idInvestigadores);
        }

        // ────────────────────────────────────────────────────────
        // GET: /LiderDashboard/  — Panel principal
        // ────────────────────────────────────────────────────────
        public ActionResult Index()
        {
            var lider = GetInvestigadorLider();
            var semillero = GetSemilleroDelLider();

            if (lider == null || semillero == null)
            {
                TempData["Mensaje"] = "No tienes un semillero asignado aún.";
                TempData["TipoMensaje"] = "warning";
                return View(new LiderDashboardViewModel());
            }

            var vm = new LiderDashboardViewModel();
            vm.Semillero = semillero;
            vm.NombreLider = lider.nombreInvestigador;

            var semilleroConIntegrantes = db.Semilleros
            .Include("Integrantes")
            .FirstOrDefault(s => s.idSemillero == semillero.idSemillero);

            var integrantes = semilleroConIntegrantes?.Integrantes
            .Where(i => i.tipoInvestigador != "Principal")
            .ToList() ?? new List<Investigadores>();

            vm.Integrantes = integrantes;
            vm.TotalIntegrantes = integrantes.Count;
            vm.TotalProyectos = db.Proyectos.Count(p => p.Semillero_idSemillero == semillero.idSemillero);
            vm.ProyectosActivos = db.Proyectos.Count(p => p.Semillero_idSemillero == semillero.idSemillero
                                                          && p.estadoProyecto == "En Proceso");

            vm.ChartGeneros = integrantes
                .GroupBy(i => i.generoInvestigador)
                .Select(g => new ChartItem { Label = g.Key, Value = g.Count() })
                .ToList();

            vm.ChartTipos = integrantes
                .GroupBy(i => i.tipoInvestigador)
                .Select(g => new ChartItem { Label = g.Key, Value = g.Count() })
                .ToList();

            return View(vm);
        }

        // ────────────────────────────────────────────────────────
        // GESTIÓN DE INTEGRANTES
        // ────────────────────────────────────────────────────────

        // GET: Listado de integrantes
        public ActionResult Integrantes()
        {
            var semillero = GetSemilleroDelLider();
            if (semillero == null) return RedirectToAction("Index");

            var semilleroConIntegrantes = db.Semilleros
            .Include("Integrantes")
            .FirstOrDefault(s => s.idSemillero == semillero.idSemillero);

            var lista = semilleroConIntegrantes?.Integrantes
                .Where(i => i.tipoInvestigador != "Principal")
                .OrderBy(i => i.nombreInvestigador)
                .ToList() ?? new List<Investigadores>();

            ViewBag.NombreSemillero = semillero.nombreSemillero;
            ViewBag.IdSemillero = semillero.idSemillero;
            return View(lista);
        }

        // GET: Formulario agregar integrante
        public ActionResult AgregarIntegrante()
        {
            var semillero = GetSemilleroDelLider();
            if (semillero == null) return RedirectToAction("Index");

            ViewBag.NombreSemillero = semillero.nombreSemillero;
            ViewBag.IdSemillero = semillero.idSemillero;
            CargarDropdowns();
            return View(new AgregarIntegranteViewModel { Semillero_idSemillero = semillero.idSemillero });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AgregarIntegrante(AgregarIntegranteViewModel vm)
        {
            if (db.Investigadores.Any(i => i.emailInvestigador == vm.emailInvestigador))
                ModelState.AddModelError("emailInvestigador", "Ya existe un investigador con ese email.");

            if (ModelState.IsValid)
            {
                int uid = GetUsuarioId();

                var integrante = new Investigadores
                {
                    Usuario_idUsuario = uid,
                    tipoInvestigador = vm.tipoInvestigador,
                    nombreInvestigador = vm.nombreInvestigador,
                    emailInvestigador = vm.emailInvestigador,
                    telefonoInvestigador = vm.telefonoInvestigador,
                    generoInvestigador = vm.generoInvestigador
                };
                db.Investigadores.Add(integrante);
                db.SaveChanges(); // guarda el investigador y obtiene su ID

                // Cargar el semillero con sus Integrantes para poder añadir
                var semillero = db.Semilleros
                    .Include("Integrantes")
                    .FirstOrDefault(s => s.idSemillero == vm.Semillero_idSemillero);

                if (semillero != null)
                {
                    semillero.Integrantes.Add(integrante);
                    db.SaveChanges(); // inserta en Semillero_has_Investigadores
                }

                TempData["Mensaje"] = $"Integrante '{vm.nombreInvestigador}' agregado correctamente.";
                TempData["TipoMensaje"] = "success";
                return RedirectToAction("Integrantes");
            }

            var sem = GetSemilleroDelLider();
            ViewBag.NombreSemillero = sem?.nombreSemillero;
            ViewBag.IdSemillero = sem?.idSemillero;
            CargarDropdowns();
            return View(vm);
        }

        // GET: Editar integrante
        public ActionResult EditarIntegrante(int id)
        {
            var semillero = GetSemilleroDelLider();
            if (semillero == null) return RedirectToAction("Index");

            // Verificar que el integrante pertenece a este semillero
            var inv = db.Semilleros
                .Include("Integrantes")
                .FirstOrDefault(s => s.idSemillero == semillero.idSemillero)
                ?.Integrantes
                .FirstOrDefault(i => i.idInvestigadores == id);

            if (inv == null) return HttpNotFound();

            CargarDropdowns();
            return View(inv);
        }

        // POST: Editar integrante
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarIntegrante(int idInvestigadores, string tipoInvestigador,
        string nombreInvestigador, string emailInvestigador,
        decimal telefonoInvestigador, string generoInvestigador)
        {
            var inv = db.Investigadores.Find(idInvestigadores);
            if (inv == null) return HttpNotFound();

            inv.tipoInvestigador = tipoInvestigador;
            inv.nombreInvestigador = nombreInvestigador;
            inv.emailInvestigador = emailInvestigador;
            inv.telefonoInvestigador = telefonoInvestigador;
            inv.generoInvestigador = generoInvestigador;

            db.Configuration.ValidateOnSaveEnabled = false; // <-- evita la excepción de validación
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = "Integrante actualizado correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("Integrantes");
        }

        // POST: Retirar integrante del semillero
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RetirarIntegrante(int id)
        {
            var inv = db.Investigadores
                .Include("Semilleros")
                .FirstOrDefault(i => i.idInvestigadores == id);

            if (inv == null) return HttpNotFound();

            if (inv.tipoInvestigador == "Principal")
            {
                TempData["Mensaje"] = "No puedes retirar al líder del semillero.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("Integrantes");
            }

            string nombre = inv.nombreInvestigador;

            // Primero eliminar vínculos en Semillero_has_Investigadores
            inv.Semilleros.Clear();
            db.SaveChanges();

            // Luego eliminar el investigador
            db.Investigadores.Remove(inv);
            db.SaveChanges();

            TempData["Mensaje"] = $"'{nombre}' fue retirado del semillero.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Integrantes");
        }

        // ────────────────────────────────────────────────────────
        // MI PERFIL
        // ────────────────────────────────────────────────────────
        // GET: Mi Perfil
        public ActionResult MiPerfil()
        {
            int id = GetUsuarioId();
            var usuario = db.Usuarios.Find(id);
            if (usuario == null) return HttpNotFound();
            return View(usuario);
        }

        // POST: Actualizar datos personales
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ActualizarDatos(string nombreUsuario, string apellidoUsuario,
            string emailUsuario, decimal telefonoUsuario)
        {
            int idSesion = GetUsuarioId();
            var original = db.Usuarios.Find(idSesion);
            if (original == null) return HttpNotFound();

            original.nombreUsuario = nombreUsuario;
            original.apellidoUsuario = apellidoUsuario;
            original.emailUsuario = emailUsuario;
            original.telefonoUsuario = telefonoUsuario;

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = "Datos actualizados correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("MiPerfil");
        }

        // POST: Cambiar contraseña
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarContrasena(string contrasenaActual, string nuevaContrasena, string confirmarContrasena)
        {
            int idSesion = GetUsuarioId();
            var original = db.Usuarios.Find(idSesion);
            if (original == null) return HttpNotFound();

            if (string.IsNullOrWhiteSpace(contrasenaActual) ||
                string.IsNullOrWhiteSpace(nuevaContrasena) ||
                string.IsNullOrWhiteSpace(confirmarContrasena))
            {
                TempData["Mensaje"] = "Todos los campos de contraseña son obligatorios.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("MiPerfil");
            }

            if (original.contrasenaUsuario.Trim() != contrasenaActual.Trim())
            {
                TempData["Mensaje"] = "La contraseña actual no es correcta.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("MiPerfil");
            }

            if (nuevaContrasena != confirmarContrasena)
            {
                TempData["Mensaje"] = "La nueva contraseña y la confirmación no coinciden.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("MiPerfil");
            }

            if (nuevaContrasena.Length < 6)
            {
                TempData["Mensaje"] = "La nueva contraseña debe tener al menos 6 caracteres.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("MiPerfil");
            }

            original.contrasenaUsuario = nuevaContrasena;
            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = "Contraseña cambiada correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("MiPerfil");
        }

        // Helper dropdowns
        private void CargarDropdowns()
        {
            ViewBag.Tipos = new List<SelectListItem>
            {
                new SelectListItem { Value = "Semillerista", Text = "Semillerista" },
                new SelectListItem { Value = "Auxiliar",     Text = "Auxiliar" },
                new SelectListItem { Value = "Asesor",       Text = "Asesor" }
            };
            ViewBag.Generos = new List<SelectListItem>
            {
                new SelectListItem { Value = "Masculino", Text = "Masculino" },
                new SelectListItem { Value = "Femenino",  Text = "Femenino" },
                new SelectListItem { Value = "Otro",      Text = "Otro" }
            };
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
            // Verificar que el semillero pertenece al líder logueado
            var lider = GetInvestigadorLider();
            var semillero = db.Semilleros
                .FirstOrDefault(s => s.idSemillero == idSemillero
                                  && s.Investigadores_idInvestigadores == lider.idInvestigadores);

            if (semillero == null) return HttpNotFound();

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
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarProyecto(int idProyecto)
        {
            var lider = GetInvestigadorLider();
            var semillero = GetSemilleroDelLider();
            var proyecto = db.Proyectos
                .FirstOrDefault(p => p.idProyecto == idProyecto
                                  && p.Semillero_idSemillero == semillero.idSemillero);

            if (proyecto == null) return HttpNotFound();

            string titulo = proyecto.tituloProyecto;
            db.Proyectos.Remove(proyecto);
            db.SaveChanges();

            TempData["Mensaje"] = $"Proyecto '{titulo}' eliminado.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Index");
        }

        // ────────────────────────────────────────────────────────
        //  FASES
        // ────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearFase(int idProyecto, string descripcionFase)
        {
            var semillero = GetSemilleroDelLider();
            var proyecto = db.Proyectos
                .FirstOrDefault(p => p.idProyecto == idProyecto
                                  && p.Semillero_idSemillero == semillero.idSemillero);

            if (proyecto == null) return HttpNotFound();

            var fase = new Fase
            {
                Proyecto_idProyecto = idProyecto,
                descripcionFase = descripcionFase,
                fechaFase = DateTime.Now,
                estadoFase = "Pendiente"
            };
            db.Fases.Add(fase);
            db.SaveChanges();

            TempData["Mensaje"] = "Fase agregada correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarFase(int idFase)
        {
            var semillero = GetSemilleroDelLider();
            var fase = db.Fases
                .Include("Proyecto")
                .FirstOrDefault(f => f.idFase == idFase
                                  && f.Proyecto.Semillero_idSemillero == semillero.idSemillero);

            if (fase == null) return HttpNotFound();

            db.Fases.Remove(fase);
            db.SaveChanges();

            TempData["Mensaje"] = "Fase eliminada.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Index");
        }

        // ────────────────────────────────────────────────────────
        //  ACTIVIDADES
        // ────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearActividad(int idFase, string nombreActividad,
            string descripActividad, string lugarActividad)
        {
            var semillero = GetSemilleroDelLider();
            var fase = db.Fases
                .Include("Proyecto")
                .FirstOrDefault(f => f.idFase == idFase
                                  && f.Proyecto.Semillero_idSemillero == semillero.idSemillero);

            if (fase == null) return HttpNotFound();

            var actividad = new Actividad
            {
                Fase_idFase = idFase,
                nombreActividad = nombreActividad,
                descripActividad = descripActividad,
                lugarActividad = lugarActividad,
                fechaActividad = DateTime.Now,
                estadoActividad = "Pendiente"
            };
            db.Actividades.Add(actividad);
            db.SaveChanges();

            TempData["Mensaje"] = $"Actividad '{nombreActividad}' creada.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CambiarEstadoActividad(int idActividad, string nuevoEstado)
        {
            var semillero = GetSemilleroDelLider();
            var actividad = db.Actividades
                .Include("Fase")
                .Include("Fase.Proyecto")
                .FirstOrDefault(a => a.idActividad == idActividad
                                  && a.Fase.Proyecto.Semillero_idSemillero == semillero.idSemillero);

            if (actividad == null) return HttpNotFound();

            actividad.estadoActividad = nuevoEstado;
            db.SaveChanges();

            // ── Recalcular Fase automáticamente ───────────────────
            var fase = db.Fases
                .Include("Actividades")
                .FirstOrDefault(f => f.idFase == actividad.Fase_idFase);

            if (fase != null)
            {
                var acts = fase.Actividades.ToList();
                if (acts.Any() && acts.All(a => a.estadoActividad == "Completada"))
                    fase.estadoFase = "Completada";
                else if (acts.Any(a => a.estadoActividad == "Completada" || a.estadoActividad == "En Proceso"))
                    fase.estadoFase = "En Proceso";
                else
                    fase.estadoFase = "Pendiente";

                db.SaveChanges();

                // ── Recalcular Proyecto automáticamente ───────────
                var proyecto = db.Proyectos
                    .Include("Fases")
                    .FirstOrDefault(p => p.idProyecto == fase.Proyecto_idProyecto);

                if (proyecto != null)
                {
                    var fases = proyecto.Fases.ToList();
                    if (fases.Any() && fases.All(f => f.estadoFase == "Completada"))
                        proyecto.estadoProyecto = "Finalizado";
                    else if (fases.Any(f => f.estadoFase == "En Proceso" || f.estadoFase == "Completada"))
                        proyecto.estadoProyecto = "En Proceso";
                    else
                        proyecto.estadoProyecto = "Pendiente";

                    db.SaveChanges();
                }
            }

            TempData["Mensaje"] = $"Actividad actualizada a '{nuevoEstado}'.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarActividad(int idActividad)
        {
            var semillero = GetSemilleroDelLider();
            var actividad = db.Actividades
                .Include("Fase")
                .Include("Fase.Proyecto")
                .FirstOrDefault(a => a.idActividad == idActividad
                                  && a.Fase.Proyecto.Semillero_idSemillero == semillero.idSemillero);

            if (actividad == null) return HttpNotFound();

            string nombre = actividad.nombreActividad;
            db.Actividades.Remove(actividad);
            db.SaveChanges();

            TempData["Mensaje"] = $"Actividad '{nombre}' eliminada.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Index");
        }
    }
}