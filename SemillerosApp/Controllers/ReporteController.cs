using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using SemillerosApp.Models;
using SemillerosApp.Report;
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
    public class ReporteController : Controller
    {
        private SemillerosContext db = new SemillerosContext();

        public ActionResult Index() => View();

        // --- MÉTODOS DE ACCIÓN ---

        public ActionResult VisualizarSemillero() =>
            GenerarReportePdf("Reporte_Semillero.rpt", "SemillerosTable", db.Semilleros.ToList());

        public ActionResult VisualizarUsuario() =>
            GenerarReportePdf("Reporte_Usuario.rpt", "UsuariosTable", db.Usuarios.ToList());

        public ActionResult VisualizarReunion() =>
            GenerarReportePdf("Reporte_Reunio.rpt", "ReunionesTable", db.Reuniones.ToList());

        public ActionResult VisualizarEvento() =>
            GenerarReportePdf("Reporte_Evento.rpt", "EventosTable", db.Eventos.ToList());

        // --- MÉTODO MAESTRO (PROCESA TODOS) ---
        private ActionResult GenerarReportePdf(string nombreRpt, string nombreTabla, IEnumerable listaDatos)
        {
            ReportDocument reporte = new ReportDocument();
            string rutaReporte = Server.MapPath("~/Report/" + nombreRpt);

            try
            {
                // Validación crítica: verificar si el archivo existe físicamente
                if (!System.IO.File.Exists(rutaReporte))
                {
                    return Content("Error: No se encontró el archivo de reporte en la ruta: " + rutaReporte);
                }

                reporte.Load(rutaReporte);

                DataTable dt = new DataTable(nombreTabla);
                var lista = listaDatos.Cast<object>().ToList();

                if (lista.Any())
                {
                    var props = lista[0].GetType().GetProperties();

                    // Crear columnas
                    foreach (var prop in props)
                    {
                        if (prop.PropertyType.IsValueType || prop.PropertyType == typeof(string))
                        {
                            dt.Columns.Add(prop.Name, Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType);
                        }
                    }

                    // Llenar datos
                    foreach (var item in lista)
                    {
                        DataRow dr = dt.NewRow();
                        foreach (var prop in props)
                        {
                            if (dt.Columns.Contains(prop.Name))
                                dr[prop.Name] = prop.GetValue(item) ?? DBNull.Value;
                        }
                        dt.Rows.Add(dr);
                    }
                }

                reporte.SetDataSource(dt);

                // Exportar a stream
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