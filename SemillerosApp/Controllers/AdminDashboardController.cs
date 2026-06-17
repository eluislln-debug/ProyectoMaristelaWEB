using SemillerosApp.Filters;
using SemillerosApp.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;

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
            // IDs de investigadores que ya son líderes de algún semillero
            var conSemillero = db.Semilleros
                .Select(s => s.Investigadores_idInvestigadores)
                .ToList();

            // Solo mostrar líderes que NO tienen semillero aún
            ViewBag.Lideres = db.Investigadores
                .Where(i => i.tipoInvestigador == "Principal"
                         && !conSemillero.Contains(i.idInvestigadores))
                .Select(i => new SelectListItem
                {
                    Value = i.idInvestigadores.ToString(),
                    Text = i.nombreInvestigador
                }).ToList();

            // Si no hay líderes disponibles, avisar
            var lideresList = db.Investigadores
            .Where(i => i.tipoInvestigador == "Principal"
                     && !conSemillero.Contains(i.idInvestigadores))
            .Select(i => new SelectListItem
            {
                Value = i.idInvestigadores.ToString(),
                Text = i.nombreInvestigador
            }).ToList();

            ViewBag.Lideres = lideresList;
            ViewBag.SinLideresDisponibles = !lideresList.Any();

            return View(new Semillero { estadoSemillero = "Activo", fechacreaSemillero = DateTime.Now });
        }

        // POST: Crear semillero
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearSemillero(Semillero semillero)
        {
            // Verificar si el investigador ya es líder de otro semillero
            bool yaEsLider = db.Semilleros
                .Any(s => s.Investigadores_idInvestigadores == semillero.Investigadores_idInvestigadores);

            if (yaEsLider)
            {
                var investigador = db.Investigadores.Find(semillero.Investigadores_idInvestigadores);
                ModelState.AddModelError("Investigadores_idInvestigadores",
                    $"{investigador?.nombreInvestigador} ya es líder de otro semillero activo. Un líder solo puede tener un semillero.");
            }

            if (ModelState.IsValid)
            {
                semillero.fechacreaSemillero = DateTime.Now;
                semillero.estadoSemillero = "Activo";
                db.Semilleros.Add(semillero);
                db.SaveChanges();

                TempData["Mensaje"] = $"Semillero '{semillero.nombreSemillero}' creado correctamente.";
                TempData["TipoMensaje"] = "success";
                return RedirectToAction("Semilleros");
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
                .Include("SemilleroEventos")
                .Include("SemilleroEventos.Eventos")
                .Include("Integrantes")
                .FirstOrDefault(s => s.idSemillero == id);

            if (semillero == null) return HttpNotFound();

            var reunionesDelSemillero = db.Reuniones
                .Where(r => r.Semillero_idSemillero == id)
                .OrderByDescending(r => r.fechaReunion)
                .ToList();

            var vm = new DetalleSemilleroViewModel
            {
                Semillero = semillero,
                Integrantes = semillero.Integrantes?.ToList() ?? new List<Investigadores>(),
                Reuniones = reunionesDelSemillero,
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
        //  GESTIÓN DE EVENTOS
        // ────────────────────────────────────────────────────────

        public ActionResult Eventos()
        {
            var eventos = db.Eventos
                .Include("Patrocinadores")
                .Include("SemilleroEventos")
                .Include("SemilleroEventos.Semillero")
                .AsNoTracking()
                .OrderByDescending(e => e.fechaEvento)
                .ToList();

            return View(eventos);
        }

        public ActionResult DetalleEvento(int id)
        {
            var evento = db.Eventos
                .Include("Patrocinadores")
                .Include("SemilleroEventos.Semillero")
                .FirstOrDefault(e => e.idEventos == id);

            if (evento == null) return HttpNotFound();

            ViewBag.Semilleros = db.Semilleros
                .Where(s => s.estadoSemillero == "Activo")
                .OrderBy(s => s.nombreSemillero)
                .ToList();

            return View(evento);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearEvento(string nombreEvento, string tipoEvento,
        string fechaEvento, string lugarEvento, string horaEvento, string horaFinEvento)
        {
            if (!DateTime.TryParse(fechaEvento, out var fecha))
            {
                TempData["Mensaje"] = "Fecha inválida.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("Eventos");
            }

            // Validar que hora fin sea posterior a hora inicio
            if (!string.IsNullOrEmpty(horaEvento) && !string.IsNullOrEmpty(horaFinEvento))
            {
                if (TimeSpan.TryParse(horaEvento, out var tIni) &&
                    TimeSpan.TryParse(horaFinEvento, out var tFin) &&
                    tFin <= tIni)
                {
                    TempData["Mensaje"] = "La hora de fin debe ser posterior a la hora de inicio.";
                    TempData["TipoMensaje"] = "danger";
                    return RedirectToAction("Eventos");
                }
            }

            var evento = new Eventos
            {
                nombreEvento = nombreEvento,
                tipoEvento = tipoEvento,
                fechaEvento = fecha,
                lugarEvento = lugarEvento,
                horaEvento = horaEvento,
                horaFinEvento = horaFinEvento
            };
            db.Eventos.Add(evento);
            db.SaveChanges();

            TempData["Mensaje"] = $"Evento '{nombreEvento}' creado correctamente.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("Eventos");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarEvento(int idEvento, string nombreEvento, string tipoEvento,
        string fechaEvento, string lugarEvento, string horaEvento, string horaFinEvento)
        {
            var evento = db.Eventos.Find(idEvento);
            if (evento == null) return HttpNotFound();

            if (!DateTime.TryParse(fechaEvento, out var fecha))
            {
                TempData["Mensaje"] = "Fecha inválida.";
                TempData["TipoMensaje"] = "danger";
                return RedirectToAction("DetalleEvento", new { id = idEvento });
            }

            if (!string.IsNullOrEmpty(horaEvento) && !string.IsNullOrEmpty(horaFinEvento))
            {
                if (TimeSpan.TryParse(horaEvento, out var tIni) &&
                    TimeSpan.TryParse(horaFinEvento, out var tFin) &&
                    tFin <= tIni)
                {
                    TempData["Mensaje"] = "La hora de fin debe ser posterior a la hora de inicio.";
                    TempData["TipoMensaje"] = "danger";
                    return RedirectToAction("DetalleEvento", new { id = idEvento });
                }
            }

            evento.nombreEvento = nombreEvento;
            evento.tipoEvento = tipoEvento;
            evento.fechaEvento = fecha;
            evento.lugarEvento = lugarEvento;
            evento.horaEvento = horaEvento;
            evento.horaFinEvento = horaFinEvento;

            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = $"Evento '{nombreEvento}' actualizado.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("DetalleEvento", new { id = idEvento });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarEvento(int idEvento)
        {
            var evento = db.Eventos
                .Include("Patrocinadores")
                .Include("SemilleroEventos")
                .FirstOrDefault(e => e.idEventos == idEvento);

            if (evento == null) return HttpNotFound();

            string nombre = evento.nombreEvento;

            // Eliminar patrocinadores y vínculos primero
            db.Patrocinadores.RemoveRange(evento.Patrocinadores);
            db.SemilleroEventos.RemoveRange(evento.SemilleroEventos);
            db.SaveChanges();

            db.Eventos.Remove(evento);
            db.SaveChanges();

            TempData["Mensaje"] = $"Evento '{nombre}' eliminado.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Eventos");
        }

        // ── PATROCINADORES ────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AgregarPatrocinador(int idEvento, string nombrePatrocinador,
            string tipoPatrocinador, string correoPatrocinador,
            decimal telefonoPatrocinador, string direccionPatrocinador)
        {
            var evento = db.Eventos.Find(idEvento);
            if (evento == null) return HttpNotFound();

            var patrocinador = new Patrocinadores
            {
                Eventos_idEventos = idEvento,
                nombrePatrocinador = nombrePatrocinador,
                tipoPatrocinador = tipoPatrocinador,
                correoPatrocinador = correoPatrocinador,
                telefonoPatrocinador = telefonoPatrocinador,
                direccionPatrocinador = direccionPatrocinador
            };
            db.Patrocinadores.Add(patrocinador);
            db.SaveChanges();

            TempData["Mensaje"] = $"Patrocinador '{nombrePatrocinador}' agregado.";
            TempData["TipoMensaje"] = "success";
            return RedirectToAction("DetalleEvento", new { id = idEvento });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarPatrocinador(int idPatrocinador, int idEvento)
        {
            var p = db.Patrocinadores.Find(idPatrocinador);
            if (p == null) return HttpNotFound();

            string nombre = p.nombrePatrocinador;
            db.Patrocinadores.Remove(p);
            db.SaveChanges();

            TempData["Mensaje"] = $"Patrocinador '{nombre}' eliminado.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("DetalleEvento", new { id = idEvento });
        }

        // ── VINCULAR SEMILLEROS ───────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult VincularSemillero(int idEvento, int idSemillero)
        {
            // Verificar que no esté ya vinculado
            bool yaExiste = db.SemilleroEventos.Any(se =>
                se.Eventos_idEventos == idEvento &&
                se.Semillero_idSemillero == idSemillero);

            if (!yaExiste)
            {
                db.SemilleroEventos.Add(new Semillero_has_Eventos
                {
                    Eventos_idEventos = idEvento,
                    Semillero_idSemillero = idSemillero
                });
                db.SaveChanges();

                TempData["Mensaje"] = "Semillero vinculado al evento.";
                TempData["TipoMensaje"] = "success";
            }
            else
            {
                TempData["Mensaje"] = "Ese semillero ya está vinculado a este evento.";
                TempData["TipoMensaje"] = "warning";
            }

            return RedirectToAction("DetalleEvento", new { id = idEvento });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DesvincularSemillero(int idEvento, int idSemillero)
        {
            var vinculo = db.SemilleroEventos.FirstOrDefault(se =>
                se.Eventos_idEventos == idEvento &&
                se.Semillero_idSemillero == idSemillero);

            if (vinculo != null)
            {
                db.SemilleroEventos.Remove(vinculo);
                db.SaveChanges();
                TempData["Mensaje"] = "Semillero desvinculado del evento.";
                TempData["TipoMensaje"] = "warning";
            }

            return RedirectToAction("DetalleEvento", new { id = idEvento });
        }

        // ────────────────────────────────────────────────────────
        //  GESTIÓN DE REUNIONES
        // ────────────────────────────────────────────────────────

        public ActionResult Reuniones()
        {
            var reuniones = db.Reuniones
                .Include("Semillero")
                .OrderBy(r => r.fechaReunion)
                .ToList();

            // Actualizar estados automáticamente
            ActualizarEstadosReuniones(reuniones);

            return View(reuniones);
        }

        private void ActualizarEstadosReuniones(List<Reunion> reuniones)
        {
            var ahora = DateTime.Now;
            bool hubocambios = false;

            foreach (var r in reuniones)
            {
                if (r.estadoReunion == "Cancelada" || r.estadoReunion == "Suspendida") continue;

                DateTime fechaHoraInicio;
                if (!DateTime.TryParse(r.fechaReunion.ToString("yyyy-MM-dd") + " " + r.horaReunion, out fechaHoraInicio))
                    continue;

                DateTime fechaHoraFin;
                bool tieneFin = DateTime.TryParse(r.fechaReunion.ToString("yyyy-MM-dd") + " " + r.horaFinReunion, out fechaHoraFin);

                string nuevoEstado;
                if (fechaHoraInicio > ahora.AddMinutes(30))
                    nuevoEstado = "Programada";
                else if (tieneFin && ahora >= fechaHoraInicio && ahora <= fechaHoraFin)
                    nuevoEstado = "En Curso";
                else if (tieneFin && ahora > fechaHoraFin)
                    nuevoEstado = "Realizada";
                else
                    nuevoEstado = "En Curso";

                if (r.estadoReunion != nuevoEstado)
                {
                    r.estadoReunion = nuevoEstado;
                    hubocambios = true;
                }
            }

            if (hubocambios)
            {
                db.Configuration.ValidateOnSaveEnabled = false;
                db.SaveChanges();
                db.Configuration.ValidateOnSaveEnabled = true;
            }
        }

        public ActionResult CrearReunion()
        {
            ViewBag.Semilleros = db.Semilleros
                .Where(s => s.estadoSemillero == "Activo")
                .Select(s => new SelectListItem
                {
                    Value = s.idSemillero.ToString(),
                    Text = s.nombreSemillero
                }).ToList();

            return View(new Reunion { estadoReunion = "Programada" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CrearReunion(Reunion reunion, int Semillero_idSemillero)
        {
            ModelState.Remove("estadoReunion");
            ModelState.Remove("creadoPor");
            ModelState.Remove("Semillero");
            ModelState.Remove("Semillero_idSemillero");
            ModelState.Remove("Participantes");

            if (ModelState.IsValid)
            {
                reunion.estadoReunion = "Programada";
                reunion.creadoPor = "Admin";
                reunion.Semillero_idSemillero = Semillero_idSemillero;   // ← directo, ya no hay que tocar Semillero

                db.Reuniones.Add(reunion);
                db.SaveChanges();

                TempData["Mensaje"] = $"Reunión '{reunion.motivoReunion}' creada correctamente.";
                TempData["TipoMensaje"] = "success";
                return RedirectToAction("Reuniones");
            }

            ViewBag.Semilleros = db.Semilleros
                .Where(s => s.estadoSemillero == "Activo")
                .Select(s => new SelectListItem
                {
                    Value = s.idSemillero.ToString(),
                    Text = s.nombreSemillero
                }).ToList();
            return View(reunion);
        }

        public ActionResult EditarReunion(int id)
        {
            var reunion = db.Reuniones.Find(id);
            if (reunion == null) return HttpNotFound();

            ViewBag.Semilleros = db.Semilleros
                .Where(s => s.estadoSemillero == "Activo")
                .Select(s => new SelectListItem
                {
                    Value = s.idSemillero.ToString(),
                    Text = s.nombreSemillero,
                    Selected = reunion.Semillero_idSemillero.HasValue && s.idSemillero == reunion.Semillero_idSemillero.Value
                }).ToList();

            return View(reunion);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditarReunion(int idReunion, string motivoReunion, DateTime fechaReunion,
            string horaReunion, string horaFinReunion, string lugarReunion, int Semillero_idSemillero)
        {
            var reunion = db.Reuniones.Find(idReunion);
            if (reunion == null) return HttpNotFound();

            reunion.motivoReunion = motivoReunion;
            reunion.fechaReunion = fechaReunion;
            reunion.horaReunion = horaReunion;
            reunion.horaFinReunion = horaFinReunion;
            reunion.lugarReunion = lugarReunion;
            reunion.Semillero_idSemillero = Semillero_idSemillero;   // ← simple reasignación directa

            // Recalcular estado
            DateTime fechaHoraInicio;
            if (DateTime.TryParse(fechaReunion.ToString("yyyy-MM-dd") + " " + horaReunion, out fechaHoraInicio))
            {
                var ahora = DateTime.Now;
                if (reunion.estadoReunion != "Cancelada" && reunion.estadoReunion != "Suspendida")
                {
                    DateTime fechaHoraFin;
                    bool tieneFin = DateTime.TryParse(
                        fechaReunion.ToString("yyyy-MM-dd") + " " + horaFinReunion, out fechaHoraFin);

                    if (fechaHoraInicio > ahora.AddMinutes(30))
                        reunion.estadoReunion = "Programada";
                    else if (tieneFin && ahora >= fechaHoraInicio && ahora <= fechaHoraFin)
                        reunion.estadoReunion = "En Curso";
                    else if (tieneFin && ahora > fechaHoraFin)
                        reunion.estadoReunion = "Realizada";
                    else
                        reunion.estadoReunion = "En Curso";
                }
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
        public ActionResult CancelarReunion(int id)
        {
            var reunion = db.Reuniones.Find(id);
            if (reunion == null) return HttpNotFound();

            reunion.estadoReunion = "Cancelada";
            db.Configuration.ValidateOnSaveEnabled = false;
            db.SaveChanges();
            db.Configuration.ValidateOnSaveEnabled = true;

            TempData["Mensaje"] = $"Reunión '{reunion.motivoReunion}' cancelada.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Reuniones");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EliminarReunion(int id)
        {
            var reunion = db.Reuniones
                .Include("Participantes")
                .FirstOrDefault(r => r.idReunion == id);

            if (reunion == null) return HttpNotFound();

            string motivo = reunion.motivoReunion;

            // Limpiar participantes (tabla intermedia) antes de eliminar
            reunion.Participantes.Clear();
            db.SaveChanges();

            db.Reuniones.Remove(reunion);
            db.SaveChanges();

            TempData["Mensaje"] = $"Reunión '{motivo}' eliminada.";
            TempData["TipoMensaje"] = "warning";
            return RedirectToAction("Reuniones");
        }

        [HttpGet]
        public JsonResult VerificarChoqueReunion(string fecha, string horaInicio, string horaFin, int idExcluir = 0)
        {
            DateTime nuevaInicio, nuevaFin;
            if (!DateTime.TryParse(fecha + " " + horaInicio, out nuevaInicio))
                return Json(new { choque = false }, JsonRequestBehavior.AllowGet);

            if (!DateTime.TryParse(fecha + " " + horaFin, out nuevaFin))
                nuevaFin = nuevaInicio.AddMinutes(1);

            var reunionesDelDia = db.Reuniones
                .Where(r => r.idReunion != idExcluir
                         && r.estadoReunion != "Cancelada"
                         && r.estadoReunion != "Suspendida"
                         && r.fechaReunion == nuevaInicio.Date)
                .ToList();

            var choque = reunionesDelDia.FirstOrDefault(r => {
                DateTime rInicio, rFin;
                if (!DateTime.TryParse(r.fechaReunion.ToString("yyyy-MM-dd") + " " + r.horaReunion, out rInicio)) return false;
                if (!DateTime.TryParse(r.fechaReunion.ToString("yyyy-MM-dd") + " " + r.horaFinReunion, out rFin)) return false;

                return (nuevaInicio >= rInicio && nuevaInicio < rFin) ||
                       (nuevaFin > rInicio && nuevaFin <= rFin) ||
                       (nuevaInicio <= rInicio && nuevaFin >= rFin);
            });

            if (choque != null)
                return Json(new
                {
                    choque = true,
                    fechaExistente = choque.fechaReunion.ToString("dd/MM/yyyy"),
                    horaExistente = choque.horaReunion + " - " + choque.horaFinReunion,
                    motivo = choque.motivoReunion
                }, JsonRequestBehavior.AllowGet);

            return Json(new { choque = false }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}