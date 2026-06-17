using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using SemillerosApp.Models;
using SemillerosApp.Filters;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace SemillerosApp.Controllers
{
    [RoleAuthorize("Admin", "Lider")]
    public class ReporteController : Controller
    {
        private SemillerosContext db = new SemillerosContext();

        private bool EsAdmin() => RoleAuthorizeAttribute.GetUserRole(HttpContext) == "Admin";

        private int GetUsuarioId()
        {
            var identity = User.Identity as System.Web.Security.FormsIdentity;
            string userData = identity?.Ticket.UserData ?? "";
            string[] parts = userData.Split('|');
            return parts.Length >= 1 ? int.Parse(parts[0]) : 0;
        }

        private Semillero GetSemilleroDelLider()
        {
            int uid = GetUsuarioId();
            var inv = db.Investigadores.FirstOrDefault(i =>
                i.Usuario_idUsuario == uid && i.tipoInvestigador == "Principal");
            if (inv == null) return null;

            return db.Semilleros
                .Include("Integrantes")
                .FirstOrDefault(s => s.Investigadores_idInvestigadores == inv.idInvestigadores);
        }

        public ActionResult Index() => View();

        // ── SEMILLEROS (solo Admin) ───────────────────────────────
        public ActionResult VisualizarSemillero()
        {
            if (!EsAdmin()) return new HttpStatusCodeResult(403);
            return GenerarReportePdf("Reporte_Semillero.rpt", "SemillerosTable", db.Semilleros.ToList());
        }

        // ── USUARIOS (solo Admin) ─────────────────────────────────
        public ActionResult VisualizarUsuario()
        {
            if (!EsAdmin()) return new HttpStatusCodeResult(403);
            return GenerarReportePdf("Reporte_Usuario.rpt", "UsuariosTable", db.Usuarios.ToList());
        }

        // ── REUNIONES (Admin: todas | Lider: solo su semillero) ───
        public ActionResult VisualizarReunion()
        {
            List<Reunion> reuniones;

            if (EsAdmin())
            {
                reuniones = db.Reuniones.Include("Semillero").ToList();
            }
            else
            {
                var semillero = GetSemilleroDelLider();
                if (semillero == null) return Content("No tienes un semillero asignado.");

                reuniones = db.Reuniones
                    .Include("Semillero")
                    .Where(r => r.Semillero_idSemillero == semillero.idSemillero)
                    .ToList();
            }

            return GenerarReportePdf("Reporte_Reunio.rpt", "ReunionesTable", reuniones);
        }

        // ── EVENTOS (Admin: todos | Lider: solo vinculados a su semillero) ──
        public ActionResult VisualizarEvento()
        {
            List<Eventos> eventos;

            if (EsAdmin())
            {
                eventos = db.Eventos.ToList();
            }
            else
            {
                var semillero = GetSemilleroDelLider();
                if (semillero == null) return Content("No tienes un semillero asignado.");

                eventos = db.SemilleroEventos
                    .Include("Eventos")
                    .Where(se => se.Semillero_idSemillero == semillero.idSemillero)
                    .Select(se => se.Eventos)
                    .ToList();
            }

            return GenerarReportePdf("Reporte_Evento.rpt", "EventosTable", eventos);
        }

        // --- MÉTODO MAESTRO (PROCESA TODOS) ---
        private ActionResult GenerarReportePdf(string nombreRpt, string nombreTabla, IEnumerable listaDatos)
        {
            ReportDocument reporte = new ReportDocument();
            string rutaReporte = Server.MapPath("~/Report/" + nombreRpt);

            try
            {
                if (!System.IO.File.Exists(rutaReporte))
                    return Content("Error: No se encontró el archivo de reporte en la ruta: " + rutaReporte);

                reporte.Load(rutaReporte);

                DataTable dt = new DataTable(nombreTabla);
                var lista = listaDatos.Cast<object>().ToList();

                if (lista.Any())
                {
                    var props = lista[0].GetType().GetProperties();

                    foreach (var prop in props)
                    {
                        if (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string))
                            dt.Columns.Add(prop.Name, typeof(string));
                    }

                    foreach (var item in lista)
                    {
                        DataRow dr = dt.NewRow();
                        foreach (var prop in props)
                        {
                            if (dt.Columns.Contains(prop.Name))
                            {
                                var val = prop.GetValue(item);
                                if (val is decimal d)
                                    dr[prop.Name] = ((long)d).ToString();
                                else if (val is DateTime dt2)
                                    dr[prop.Name] = dt2.ToString("dd/MM/yyyy");
                                else
                                    dr[prop.Name] = val?.ToString() ?? "";
                            }
                        }
                        dt.Rows.Add(dr);
                    }
                }

                reporte.SetDataSource(dt);

                Stream stream = reporte.ExportToStream(ExportFormatType.PortableDocFormat);
                return File(stream, "application/pdf");
            }
            catch (Exception ex)
            {
                return Content("Error crítico al generar el reporte: " + ex.Message);
            }
            finally
            {
                reporte.Close();
                reporte.Dispose();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { db.Dispose(); }
            base.Dispose(disposing);
        }
    }
}