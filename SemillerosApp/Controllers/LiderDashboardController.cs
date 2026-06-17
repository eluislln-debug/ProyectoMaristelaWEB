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

        // ────────────────────────────────────────────────────────
        // REUNIONES
        // ────────────────────────────────────────────────────────

        public ActionResult Reuniones()
        {
            var semillero = GetSemilleroDelLider();
            if (semillero == null) return RedirectToAction("Index");

            var lider = GetInvestigadorLider();

            var reuniones = db.Reuniones
                .Include("Semillero")
                .Include("Participantes")
                .Where(r => r.Semillero_idSemillero == semillero.idSemillero)
                .ToList();

            var ahora = DateTime.Now;
            bool huboCambios = false;

            foreach (var r in reuniones)
            {
                if (r.estadoReunion == "Suspendida" || r.estadoReunion == "Cancelada") continue;

                DateTime fechaHoraInicio, fechaHoraFin;
                if (!DateTime.TryParse(r.fechaReunion.ToString("yyyy-MM-dd") + " " + r.horaReunion, out fechaHoraInicio)) continue;
                bool tieneFin = DateTime.TryParse(r.fechaReunion.ToString("yyyy-MM-dd") + " " + r.horaFinReunion, out fechaHoraFin);

                string nuevoEstado;
                if (fechaHoraInicio > ahora.AddMinutes(30))
                    nuevoEstado = "Programada";
                else if (tieneFin && ahora >= fechaHoraInicio && ahora <= fechaHoraFin)
                    nuevoEstado = "En Curso";
                else if (tieneFin && ahora > fechaHoraFin)
                    nuevoEstado = "Finalizada";
                else
                    nuevoEstado = "En Curso";

                if (r.estadoReunion != nuevoEstado)
                {
                    r.estadoReunion = nuevoEstado;
                    huboCambios = true;
                }
            }

            if (huboCambios)
            {
                db.Configuration.ValidateOnSaveEnabled = false;
                db.SaveChanges();
                db.Configuration.ValidateOnSaveEnabled = true;
            }

            ViewBag.NombreSemillero = semillero.nombreSemillero;
            ViewBag.IdSemillero = semillero.idSemillero;
            ViewBag.Investigadores = GetInvestigadoresDisponibles(semillero, lider);
            ViewBag.IdLider = lider.idInvestigadores;

            return View(reuniones);
        }

        // ── Helper: lista de investigadores disponibles para elegir ──────
        // (integrantes del semillero + el propio líder)
        private List<Investigadores> GetInvestigadoresDisponibles(Semillero semillero, Investigadores lider)
        {
            var semilleroConIntegrantes = db.Semilleros
                .Include("Integrantes")
                .FirstOrDefault(s => s.idSemillero == semillero.idSemillero);

            var lista = semilleroConIntegrantes?.Integrantes?.ToList() ?? new List<Investigadores>();

            // Asegurar que el líder esté incluido siempre
            if (!lista.Any(i => i.idInvestigadores == lider.idInvestigadores))
                lista.Insert(0, lider);

            return lista.OrderBy(i => i.nombreInvestigador).ToList();
        }

        public ActionResult CrearReunion()
        {
            var semillero = GetSemilleroDelLider();
            var lider = GetInvestigadorLider();
            if (semillero == null || lider == null) return RedirectToAction("Index");

            ViewBag.NombreSemillero = semillero.nombreSemillero;
            ViewBag.IdSemillero = semillero.idSemillero;
            ViewBag.Investigadores = GetInvestigadoresDisponibles(semillero, lider);
            ViewBag.IdLider = lider.idInvestigadores;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearReunion(Reunion model, int idSemillero, int[] participantes)
        {
            var semillero = GetSemilleroDelLider();
            var lider = GetInvestigadorLider();
            if (semillero == null || lider == null) return RedirectToAction("Index");

            ModelState.Remove("creadoPor");
            ModelState.Remove("estadoReunion");
            ModelState.Remove("Participantes");
            ModelState.Remove("Semillero");
            ModelState.Remove("Semillero_idSemillero");

            if (participantes == null || participantes.Length == 0)
                ModelState.AddModelError("", "Debes seleccionar al menos un participante para la reunión.");

            if (ModelState.IsValid)
            {
                var conflicto = VerificarChoqueInterno(model.fechaReunion, model.horaReunion, model.horaFinReunion,
                                                         participantes, idExcluir: 0);
                if (conflicto != null)
                {
                    TempData["Mensaje"] = conflicto;
                    TempData["TipoMensaje"] = "danger";
                    return RedirectToAction("Reuniones");
                }

                model.estadoReunion = "Programada";
                model.creadoPor = "Lider";
                model.Semillero_idSemillero = idSemillero;   // ← directo, ya no hay que tocar Semillero
                db.Reuniones.Add(model);
                db.SaveChanges();

                var reunionConParticipantes = db.Reuniones
                    .Include("Participantes")
                    .FirstOrDefault(r => r.idReunion == model.idReunion);

                if (reunionConParticipantes != null && participantes != null)
                {
                    foreach (var idInv in participantes)
                    {
                        var inv = db.Investigadores.Find(idInv);
                        if (inv != null) reunionConParticipantes.Participantes.Add(inv);
                    }
                    db.SaveChanges();
                }

                TempData["Mensaje"] = $"Reunión '{model.motivoReunion}' creada correctamente.";
                TempData["TipoMensaje"] = "success";
                return RedirectToAction("Reuniones");
            }

            var primerError = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault();

            TempData["Mensaje"] = string.IsNullOrEmpty(primerError)
                ? "No se pudo crear la reunión. Verifica los datos."
                : primerError;
            TempData["TipoMensaje"] = "danger";
            return RedirectToAction("Reuniones");
        }

        public ActionResult EditarReunion(int id)
        {
            var semillero = GetSemilleroDelLider();
            var lider = GetInvestigadorLider();
            if (semillero == null || lider == null) return RedirectToAction("Index");

            var reunion = db.Reuniones
                .Include("Semillero")
                .Include("Participantes")
                .FirstOrDefault(r => r.idReunion == id &&
                                r.Semillero_idSemillero == semillero.idSemillero);

            if (reunion == null) return HttpNotFound();

            if (string.Equals(reunion.creadoPor?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Mensaje"] = "No puedes editar una reunión creada por el administrador.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("Reuniones");
            }

            if (reunion.estadoReunion == "Finalizada")
            {
                TempData["Mensaje"] = "No puedes editar una reunión finalizada.";
                TempData["TipoMensaje"] = "warning";
                return RedirectToAction("Reuniones");
            }

            ViewBag.Investigadores = GetInvestigadoresDisponibles(semillero, lider);
            ViewBag.ParticipantesActuales = reunion.Participantes.Select(p => p.idInvestigadores).ToList();

            return View(reunion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarReunion(Reunion model, int[] participantes)
        {
            var semillero = GetSemilleroDelLider();
            var lider = GetInvestigadorLider();
            if (semillero == null || lider == null) return RedirectToAction("Index");

            var reunion = db.Reuniones
                .Include("Semillero")
                .Include("Participantes")
                .FirstOrDefault(r => r.idReunion == model.idReunion &&
                                r.Semillero_idSemillero == semillero.idSemillero);

            if (reunion == null) return HttpNotFound();

            if (string.Equals(reunion.creadoPor?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Mensaje"] = "No puedes editar una reunión creada por el administrador.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("Reuniones");
            }

            if (reunion.estadoReunion == "Finalizada")
            {
                TempData["Mensaje"] = "No puedes editar una reunión finalizada.";
                TempData["TipoMensaje"] = "warning";
                return RedirectToAction("Reuniones");
            }

            ModelState.Remove("creadoPor");
            ModelState.Remove("estadoReunion");
            ModelState.Remove("Participantes");
            ModelState.Remove("Semillero");
            ModelState.Remove("Semillero_idSemillero");

            if (participantes == null || participantes.Length == 0)
                ModelState.AddModelError("", "Debes seleccionar al menos un participante.");

            if (!ModelState.IsValid)
            {
                var primerError = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .FirstOrDefault();

                TempData["Mensaje"] = string.IsNullOrEmpty(primerError)
                    ? "No se pudo actualizar la reunión. Verifica los datos."
                    : primerError;
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("Reuniones");
            }

            var conflicto = VerificarChoqueInterno(model.fechaReunion, model.horaReunion, model.horaFinReunion,
                                                     participantes, idExcluir: model.idReunion);
            if (conflicto != null)
            {
                TempData["Mensaje"] = conflicto;
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("Reuniones");
            }

            reunion.fechaReunion = model.fechaReunion;
            reunion.motivoReunion = model.motivoReunion;
            reunion.lugarReunion = model.lugarReunion;
            reunion.horaReunion = model.horaReunion;
            reunion.horaFinReunion = model.horaFinReunion;

            reunion.Participantes.Clear();
            foreach (var idInv in participantes)
            {
                var inv = db.Investigadores.Find(idInv);
                if (inv != null) reunion.Participantes.Add(inv);
            }

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = "Reunión actualizada correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("Reuniones");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SuspenderReunion(int id)
        {
            var semillero = GetSemilleroDelLider();
            var reunion = db.Reuniones
                .FirstOrDefault(r => r.idReunion == id && r.Semillero_idSemillero == semillero.idSemillero);

            if (reunion == null) return HttpNotFound();

            if (string.Equals(reunion.creadoPor?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Mensaje"] = "No puedes suspender una reunión creada por el administrador.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("Reuniones");
            }

            if (reunion.estadoReunion == "Finalizada")
            {
                TempData["Mensaje"] = "No puedes suspender una reunión ya finalizada.";
                TempData["TipoMensaje"] = "warning";
                return RedirectToAction("Reuniones");
            }

            reunion.estadoReunion = "Suspendida";
            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = $"Reunión '{reunion.motivoReunion}' suspendida.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Reuniones");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarReunion(int id)
        {
            var semillero = GetSemilleroDelLider();
            var reunion = db.Reuniones
                .Include("Participantes")
                .FirstOrDefault(r => r.idReunion == id && r.Semillero_idSemillero == semillero.idSemillero);

            if (reunion == null) return HttpNotFound();

            if (string.Equals(reunion.creadoPor?.Trim(), "Admin", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Mensaje"] = "No puedes eliminar una reunión creada por el administrador.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("Reuniones");
            }

            reunion.Participantes.Clear();
            db.SaveChanges();

            db.Reuniones.Remove(reunion);
            db.SaveChanges();

            TempData["Mensaje"] = "Reunión eliminada correctamente.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Reuniones");
        }

        // ── Verificación de choque en SERVIDOR (fecha+hora+participantes) ─
        // Devuelve null si no hay conflicto, o un mensaje describiendo el choque.
        private string VerificarChoqueInterno(DateTime fecha, string horaInicio, string horaFin,
            int[] participantes, int idExcluir)
        {
            DateTime nuevaInicio, nuevaFin;
            if (!DateTime.TryParse(fecha.ToString("yyyy-MM-dd") + " " + horaInicio, out nuevaInicio))
                return null;
            if (!DateTime.TryParse(fecha.ToString("yyyy-MM-dd") + " " + horaFin, out nuevaFin))
                nuevaFin = nuevaInicio.AddMinutes(1);

            if (participantes == null || participantes.Length == 0) return null;

            var reunionesDelDia = db.Reuniones
                .Include("Participantes")
                .Where(r => r.idReunion != idExcluir
                         && r.estadoReunion != "Cancelada"
                         && r.estadoReunion != "Suspendida"
                         && r.fechaReunion == nuevaInicio.Date)
                .ToList();

            foreach (var r in reunionesDelDia)
            {
                DateTime rInicio, rFin;
                if (!DateTime.TryParse(r.fechaReunion.ToString("yyyy-MM-dd") + " " + r.horaReunion, out rInicio)) continue;
                if (!DateTime.TryParse(r.fechaReunion.ToString("yyyy-MM-dd") + " " + r.horaFinReunion, out rFin)) continue;

                bool seSolapan = nuevaInicio < rFin && nuevaFin > rInicio;
                if (!seSolapan) continue;

                var idsParticipantesExistentes = r.Participantes.Select(p => p.idInvestigadores).ToHashSet();
                var choque = participantes.FirstOrDefault(idsParticipantesExistentes.Contains);

                if (choque != 0 || (choque == 0 && idsParticipantesExistentes.Contains(0)))
                {
                    var invChoque = db.Investigadores.Find(choque);
                    string nombreInv = invChoque != null ? invChoque.nombreInvestigador : "Un participante";
                    return $"{nombreInv} ya tiene la reunión \"{r.motivoReunion}\" de {r.horaReunion} a {r.horaFinReunion} ese día.";
                }
            }

            return null;
        }

        // ── AJAX: verificar choque desde el formulario (tiempo real) ──────
        [HttpGet]
        public JsonResult VerificarChoqueReunionLider(string fecha, string horaInicio, string horaFin,
            string participantesCsv, int idExcluir = 0)
        {
            DateTime fechaParsed;
            if (!DateTime.TryParse(fecha, out fechaParsed))
                return Json(new { choque = false }, JsonRequestBehavior.AllowGet);

            int[] participantes = (participantesCsv ?? "")
                .Split(',')
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(int.Parse)
                .ToArray();

            string conflicto = VerificarChoqueInterno(fechaParsed, horaInicio, horaFin, participantes, idExcluir);

            if (conflicto != null)
                return Json(new { choque = true, mensaje = conflicto }, JsonRequestBehavior.AllowGet);

            return Json(new { choque = false }, JsonRequestBehavior.AllowGet);
        }

        // GET: Eventos del semillero del líder
        public ActionResult Eventos()
        {
            var investigador = GetInvestigadorLider();

            if (investigador == null)
            {
                TempData["Mensaje"] = "No tienes un semillero asignado.";
                TempData["TipoMensaje"] = "warning";
                return RedirectToAction("Index");
            }

            var semillero = db.Semilleros
                .Include("SemilleroEventos")
                .Include("SemilleroEventos.Eventos")
                .Include("SemilleroEventos.Eventos.Patrocinadores")
                .FirstOrDefault(s => s.Investigadores_idInvestigadores == investigador.idInvestigadores);

            if (semillero == null)
            {
                TempData["Mensaje"] = "No se encontró tu semillero.";
                TempData["TipoMensaje"] = "warning";
                return RedirectToAction("Index");
            }

            var eventos = semillero.SemilleroEventos?
                .Where(se => se.Eventos != null)
                .Select(se => se.Eventos)
                .OrderBy(e => e.fechaEvento)
                .ToList() ?? new List<Eventos>();

            ViewBag.NombreSemillero = semillero.nombreSemillero;
            return View(eventos);
        }

        public ActionResult Reportes()
        {
            var semillero = GetSemilleroDelLider();
            if (semillero == null) return RedirectToAction("Index");

            ViewBag.NombreSemillero = semillero.nombreSemillero;
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}