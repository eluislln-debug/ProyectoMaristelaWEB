using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using SemillerosApp.Models;


namespace SemillerosApp.Models
{
    public class AdminDashboardViewModel
    {
        // Estadísticas
        public int TotalSemilleros { get; set; }
        public int SemillerosActivos { get; set; }
        public int SemillerosInactivos { get; set; }
        public int TotalInvestigadores { get; set; }
        public int TotalUsuarios { get; set; }
        public int TotalProyectos { get; set; }

        // Listas
        public List<Semillero> Semilleros { get; set; }

        // Gráficas
        public List<ChartItem> ChartEstados { get; set; }
        public List<ChartItem> ChartLineas { get; set; }
    }

    public class ChartItem
    {
        public string Label { get; set; }
        public int Value { get; set; }
    }

    // ── VIEWMODELS DEL LÍDER ──────────────────────────────────────

    public class LiderDashboardViewModel
    {
        public Semillero Semillero { get; set; }
        public string NombreLider { get; set; }
        public List<Investigadores> Integrantes { get; set; } = new List<Investigadores>();
        public int TotalIntegrantes { get; set; }
        public int TotalProyectos { get; set; }
        public int ProyectosActivos { get; set; }
        public List<ChartItem> ChartGeneros { get; set; } = new List<ChartItem>();
        public List<ChartItem> ChartTipos { get; set; } = new List<ChartItem>();
    }

    public class AgregarIntegranteViewModel
    {
        public int Semillero_idSemillero { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El nombre es requerido")]
        [System.ComponentModel.DataAnnotations.StringLength(50, MinimumLength = 3, ErrorMessage = "Mínimo 3 caracteres")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Nombre completo")]
        public string nombreInvestigador { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El email es requerido")]
        [System.ComponentModel.DataAnnotations.EmailAddress(ErrorMessage = "Email inválido")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Correo electrónico")]
        public string emailInvestigador { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El teléfono es requerido")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Teléfono")]
        public decimal telefonoInvestigador { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El género es requerido")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Género")]
        public string generoInvestigador { get; set; }

        [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "El tipo es requerido")]
        [System.ComponentModel.DataAnnotations.Display(Name = "Tipo de investigador")]
        public string tipoInvestigador { get; set; }
    }
}