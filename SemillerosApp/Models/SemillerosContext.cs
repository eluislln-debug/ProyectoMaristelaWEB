using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;

namespace SemillerosApp.Models
{
    public class SemillerosContext : DbContext
    {
        public SemillerosContext() : base("name=SemillerosContext") { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Reporte> Reportes { get; set; }
        public DbSet<Reunion> Reuniones { get; set; }
        public DbSet<Investigadores> Investigadores { get; set; }
        public DbSet<Semillero> Semilleros { get; set; }
        public DbSet<Proyecto> Proyectos { get; set; }
        public DbSet<Fase> Fases { get; set; }
        public DbSet<Actividad> Actividades { get; set; }
        public DbSet<Eventos> Eventos { get; set; }
        public DbSet<Semillero_has_Eventos> SemilleroEventos { get; set; }
        public DbSet<Patrocinadores> Patrocinadores { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Usuario>().ToTable("Usuario");
            modelBuilder.Entity<Reporte>().ToTable("Reporte");
            modelBuilder.Entity<Reunion>().ToTable("Reunion");
            modelBuilder.Entity<Investigadores>().ToTable("Investigadores");
            modelBuilder.Entity<Semillero>().ToTable("Semillero");
            modelBuilder.Entity<Proyecto>().ToTable("Proyecto");
            modelBuilder.Entity<Fase>().ToTable("Fase");
            modelBuilder.Entity<Actividad>().ToTable("Actividad");
            modelBuilder.Entity<Eventos>().ToTable("Eventos");
            modelBuilder.Entity<Patrocinadores>().ToTable("Patrocinadores");
            modelBuilder.Entity<Semillero_has_Eventos>().ToTable("Semillero_has_Eventos");

            // Clave compuesta
            modelBuilder.Entity<Semillero_has_Eventos>()
                .HasKey(s => new { s.Semillero_idSemillero, s.Eventos_idEventos });

            // Mapeo explícito FK Semillero → SemilleroEventos
            modelBuilder.Entity<Semillero_has_Eventos>()
                .HasRequired(s => s.Semillero)
                .WithMany(s => s.SemilleroEventos)
                .HasForeignKey(s => s.Semillero_idSemillero)
                .WillCascadeOnDelete(false);

            // Mapeo explícito FK Eventos → SemilleroEventos
            modelBuilder.Entity<Semillero_has_Eventos>()
                .HasRequired(s => s.Eventos)
                .WithMany(e => e.SemilleroEventos)
                .HasForeignKey(s => s.Eventos_idEventos)
                .WillCascadeOnDelete(false);

            // Relación N:M Semillero ↔ Investigadores (integrantes)
            modelBuilder.Entity<Semillero>()
                .HasMany(s => s.Integrantes)
                .WithMany(i => i.Semilleros)
                .Map(m => {
                    m.ToTable("Semillero_has_Investigadores");
                    m.MapLeftKey("Semillero_idSemillero");
                    m.MapRightKey("Investigadores_idInvestigadores");
                });

            // Relación Reunion → Semillero (un semillero tiene muchas reuniones)
            modelBuilder.Entity<Reunion>()
                .HasOptional(r => r.Semillero)
                .WithMany(s => s.Reuniones)
                .HasForeignKey(r => r.Semillero_idSemillero);

            // Relación N:M Reunion ↔ Investigadores (participantes)
            modelBuilder.Entity<Reunion>()
                .HasMany(r => r.Participantes)
                .WithMany(i => i.Reuniones)
                .Map(m => {
                    m.ToTable("Reunion_has_Investigadores");
                    m.MapLeftKey("Reunion_idReunion");
                    m.MapRightKey("Investigadores_idInvestigadores");
                });

            base.OnModelCreating(modelBuilder);
        }
    }

    // ── USUARIO ───────────────────────────────────────────────────
    public class Usuario
    {
        [Key]
        public int idUsuario { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mínimo 2 caracteres")]
        [Display(Name = "Nombre")]
        public string nombreUsuario { get; set; }

        [Required(ErrorMessage = "El apellido es requerido")]
        [StringLength(50, MinimumLength = 2, ErrorMessage = "Mínimo 2 caracteres")]
        [Display(Name = "Apellido")]
        public string apellidoUsuario { get; set; }

        [Required(ErrorMessage = "La contraseña es requerida")]
        [StringLength(20, MinimumLength = 6, ErrorMessage = "Mínimo 6 caracteres")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string contrasenaUsuario { get; set; }

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [StringLength(35)]
        [Display(Name = "Email")]
        public string emailUsuario { get; set; }

        [Required(ErrorMessage = "El teléfono es requerido")]
        [Display(Name = "Teléfono")]
        public decimal telefonoUsuario { get; set; }

        [StringLength(35)]
        [Display(Name = "Estado")]
        public string estadoUsuario { get; set; } = "Activo";

        // 'Admin' o 'Lider'
        [StringLength(20)]
        [Display(Name = "Rol")]
        public string rolUsuario { get; set; } = "Lider";

        public virtual ICollection<Reporte> Reportes { get; set; }
        public virtual ICollection<Investigadores> Investigadores { get; set; }
    }

    // ── REPORTE ───────────────────────────────────────────────────
    public class Reporte
    {
        [Key]
        public int idReporte { get; set; }

        [Required]
        [ForeignKey("Usuario")]
        public int Usuario_idUsuario { get; set; }

        [Required(ErrorMessage = "El tipo de reporte es requerido")]
        [StringLength(20)]
        [Display(Name = "Tipo de Reporte")]
        public string tipoReporte { get; set; }

        [Required(ErrorMessage = "La fecha es requerida")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime fechaReporte { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Hora")]
        public string horaReporte { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Formato")]
        public string formatoReporte { get; set; }

        public virtual Usuario Usuario { get; set; }
    }

    // ── REUNION ───────────────────────────────────────────────────
    public class Reunion
    {
        [Key]
        public int idReunion { get; set; }

        [ForeignKey("Semillero")]
        public int? Semillero_idSemillero { get; set; }

        [Required(ErrorMessage = "La fecha es requerida")]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime fechaReunion { get; set; }

        [Required(ErrorMessage = "El motivo es requerido")]
        [StringLength(50)]
        [Display(Name = "Motivo")]
        public string motivoReunion { get; set; }

        [StringLength(20)]
        [Display(Name = "Estado")]
        public string estadoReunion { get; set; } = "Programada";

        [Required(ErrorMessage = "El lugar es requerido")]
        [StringLength(40)]
        [Display(Name = "Lugar")]
        public string lugarReunion { get; set; }

        [Required]
        [StringLength(20)]
        public string horaReunion { get; set; }

        [Required(ErrorMessage = "La hora de fin es requerida")]
        [StringLength(20)]
        [Display(Name = "Hora de Fin")]
        public string horaFinReunion { get; set; }

        [StringLength(20)]
        public string creadoPor { get; set; }

        // ── Navegación ────────────────────────────────────────────
        public virtual Semillero Semillero { get; set; }                       
        public virtual ICollection<Investigadores> Participantes { get; set; }
    }

    // ── INVESTIGADORES ────────────────────────────────────────────
    public class Investigadores
    {
        [Key]
        public int idInvestigadores { get; set; }

        [ForeignKey("Usuario")]
        public int? Usuario_idUsuario { get; set; }

        [Required(ErrorMessage = "El tipo es requerido")]
        [StringLength(20)]
        [Display(Name = "Tipo")]
        public string tipoInvestigador { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(50)]
        [Display(Name = "Nombre")]
        public string nombreInvestigador { get; set; }

        [Required(ErrorMessage = "El email es requerido")]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [StringLength(35)]
        [Display(Name = "Email")]
        public string emailInvestigador { get; set; }

        [Required]
        [Display(Name = "Teléfono")]
        public decimal telefonoInvestigador { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Género")]
        public string generoInvestigador { get; set; }

        public virtual Usuario Usuario { get; set; }
        public virtual ICollection<Semillero> Semilleros { get; set; }

        public virtual ICollection<Reunion> Reuniones { get; set; }
    }

    // ── SEMILLERO ─────────────────────────────────────────────────
    public class Semillero
    {
        [Key]
        public int idSemillero { get; set; }

        [ForeignKey("Investigadores")]
        public int Investigadores_idInvestigadores { get; set; }

        [Required(ErrorMessage = "El nombre del semillero es requerido")]
        [StringLength(30)]
        [Display(Name = "Nombre del Semillero")]
        public string nombreSemillero { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Creación")]
        public DateTime fechacreaSemillero { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "La línea de investigación es requerida")]
        [StringLength(50)]
        [Display(Name = "Línea de Investigación")]
        public string lineaInvestigacion { get; set; }

        [StringLength(20)]
        [Display(Name = "Estado")]
        public string estadoSemillero { get; set; } = "Activo";

        [Required(ErrorMessage = "La descripción es requerida")]
        [StringLength(50)]
        [Display(Name = "Descripción")]
        public string descripcionSemillero { get; set; }

        // Navegación
        public virtual Investigadores Investigadores { get; set; }
        public virtual ICollection<Proyecto> Proyectos { get; set; }
        public virtual ICollection<Semillero_has_Eventos> SemilleroEventos { get; set; }
        public virtual ICollection<Investigadores> Integrantes { get; set; }
        public virtual ICollection<Reunion> Reuniones { get; set; }
    }

    // ── PROYECTO ──────────────────────────────────────────────────
    public class Proyecto
    {
        [Key]
        public int idProyecto { get; set; }

        [ForeignKey("Semillero")]
        public int Semillero_idSemillero { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Inicio")]
        public DateTime fechainProyecto { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "La duración es requerida")]
        [StringLength(20)]
        [Display(Name = "Duración")]
        public string duracionProyecto { get; set; }

        [StringLength(20)]
        [Display(Name = "Estado")]
        public string estadoProyecto { get; set; } = "En Proceso";

        [Required(ErrorMessage = "El título es requerido")]
        [StringLength(100)]
        [Display(Name = "Título")]
        public string tituloProyecto { get; set; }

        [Required(ErrorMessage = "La descripción es requerida")]
        [StringLength(300)]
        [Display(Name = "Descripción")]
        public string descripcionProyecto { get; set; }

        public virtual Semillero Semillero { get; set; }
        public virtual ICollection<Fase> Fases { get; set; }
    }

    // ── FASE ──────────────────────────────────────────────────────
    public class Fase
    {
        [Key]
        public int idFase { get; set; }

        [ForeignKey("Proyecto")]
        public int Proyecto_idProyecto { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime fechaFase { get; set; } = DateTime.Now;

        [DataType(DataType.Date)]
        [Display(Name = "Fecha Límite")]
        public DateTime? fechaLimiteFase { get; set; }

        [Required(ErrorMessage = "La descripción es requerida")]
        [StringLength(300)]
        [Display(Name = "Descripción")]
        public string descripcionFase { get; set; }

        [StringLength(20)]
        [Display(Name = "Estado")]
        public string estadoFase { get; set; } = "Pendiente";

        public virtual Proyecto Proyecto { get; set; }
        public virtual ICollection<Actividad> Actividades { get; set; }
    }

    // ── ACTIVIDAD ─────────────────────────────────────────────────
    public class Actividad
    {
        [Key]
        public int idActividad { get; set; }

        [ForeignKey("Fase")]
        public int Fase_idFase { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(100)]
        [Display(Name = "Nombre")]
        public string nombreActividad { get; set; }

        [Required(ErrorMessage = "La descripción es requerida")]
        [StringLength(300)]
        [Display(Name = "Descripción")]
        public string descripActividad { get; set; }

        // Fecha + hora de inicio
        [DataType(DataType.Date)]
        [Display(Name = "Fecha de Inicio")]
        public DateTime fechaActividad { get; set; } = DateTime.Now;

        [StringLength(10)]
        [Display(Name = "Hora de Inicio")]
        public string horaActividad { get; set; }

        // Fecha + hora límite (deadline)
        [DataType(DataType.Date)]
        [Display(Name = "Fecha Límite")]
        public DateTime? fechaLimiteActividad { get; set; }

        [StringLength(10)]
        [Display(Name = "Hora Límite")]
        public string horaLimiteActividad { get; set; }

        [StringLength(30)]
        [Display(Name = "Estado")]
        public string estadoActividad { get; set; } = "Pendiente";

        [Required(ErrorMessage = "El lugar es requerido")]
        [StringLength(40)]
        [Display(Name = "Lugar")]
        public string lugarActividad { get; set; }

        public virtual Fase Fase { get; set; }

        // ── Propiedad calculada (no se mapea a BD) ────────────────
        // Devuelve el estado sugerido según DateTime.Now
        [NotMapped]
        public string EstadoSugerido
        {
            get
            {
                // Si ya tiene un estado final, no sugerir cambio
                var finales = new[] { "Completada", "Con Retraso", "Descartada" };
                if (finales.Contains(estadoActividad)) return estadoActividad;

                DateTime ahora = DateTime.Now;

                // Construir datetime de inicio y límite combinando fecha + hora
                DateTime inicio = fechaActividad;
                if (!string.IsNullOrEmpty(horaActividad) && TimeSpan.TryParse(horaActividad, out var ti))
                    inicio = fechaActividad.Date + ti;

                DateTime? limite = null;
                if (fechaLimiteActividad.HasValue)
                {
                    limite = fechaLimiteActividad.Value.Date;
                    if (!string.IsNullOrEmpty(horaLimiteActividad) && TimeSpan.TryParse(horaLimiteActividad, out var tl))
                        limite = fechaLimiteActividad.Value.Date + tl;
                }

                if (limite.HasValue && ahora > limite.Value)
                    return "Vencida"; // venció sin completarse
                if (ahora >= inicio && (limite == null || ahora <= limite.Value))
                    return "En Proceso";
                if (ahora < inicio)
                    return "Pendiente";

                return estadoActividad;
            }
        }

        [NotMapped]
        public bool EstaVencida =>
            fechaLimiteActividad.HasValue &&
            !new[] { "Completada", "Con Retraso", "Descartada" }.Contains(estadoActividad) &&
            DateTime.Now > (fechaLimiteActividad.Value.Date +
                (TimeSpan.TryParse(horaLimiteActividad, out var t) ? t : TimeSpan.Zero));
    }

    // ── EVENTOS ───────────────────────────────────────────────────
    public class Eventos
    {
        [Key]
        public int idEventos { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(20)]
        [Display(Name = "Nombre del Evento")]
        public string nombreEvento { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Tipo")]
        public string tipoEvento { get; set; }

        [Required]
        [DataType(DataType.Date)]
        [Display(Name = "Fecha")]
        public DateTime fechaEvento { get; set; }

        [Required(ErrorMessage = "El lugar es requerido")]
        [StringLength(40)]
        [Display(Name = "Lugar")]
        public string lugarEvento { get; set; }

        [Required]
        [StringLength(10)]
        [Display(Name = "Hora de Inicio")]
        public string horaEvento { get; set; }

        [StringLength(10)]
        [Display(Name = "Hora de Fin")]
        public string horaFinEvento { get; set; }

        public virtual ICollection<Patrocinadores> Patrocinadores { get; set; }
        public virtual ICollection<Semillero_has_Eventos> SemilleroEventos { get; set; }
    }

    // ── SEMILLERO_HAS_EVENTOS (N:M) ───────────────────────────────
    public class Semillero_has_Eventos
    {
        public int Semillero_idSemillero { get; set; }
        public int Eventos_idEventos { get; set; }

        public virtual Semillero Semillero { get; set; }
        public virtual Eventos Eventos { get; set; }
    }

    // ── PATROCINADORES ────────────────────────────────────────────
    public class Patrocinadores
    {
        [Key]
        public int idPatrocinadores { get; set; }

        [ForeignKey("Eventos")]
        public int Eventos_idEventos { get; set; }

        [Required(ErrorMessage = "El nombre es requerido")]
        [StringLength(40)]
        [Display(Name = "Nombre")]
        public string nombrePatrocinador { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Tipo")]
        public string tipoPatrocinador { get; set; }

        [Required]
        [EmailAddress(ErrorMessage = "Correo inválido")]
        [StringLength(30)]
        [Display(Name = "Correo")]
        public string correoPatrocinador { get; set; }

        [Required]
        [Display(Name = "Teléfono")]
        public decimal telefonoPatrocinador { get; set; }

        [Required(ErrorMessage = "La dirección es requerida")]
        [StringLength(50)]
        [Display(Name = "Dirección")]
        public string direccionPatrocinador { get; set; }

        public virtual Eventos Eventos { get; set; }
    }

    // ── LOGIN VIEWMODEL ───────────────────────────────────────────
    public class LoginViewModel
    {
        [Required(ErrorMessage = "El email es obligatorio")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        [Display(Name = "Correo Electrónico")]
        public string Email { get; set; }

        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [DataType(DataType.Password)]
        [Display(Name = "Contraseña")]
        public string Password { get; set; }

        [Display(Name = "Recordarme")]
        public bool RememberMe { get; set; }
    }

    // ── REUNION_HAS_INVESTIGADORES (N:M) ──────────────────────────
    public class Reunion_has_Investigadores
    {
        public int Reunion_idReunion { get; set; }
        public int Investigadores_idInvestigadores { get; set; }

        public virtual Reunion Reunion { get; set; }
        public virtual Investigadores Investigadores { get; set; }
    }
}