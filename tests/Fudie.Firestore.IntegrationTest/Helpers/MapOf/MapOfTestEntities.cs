namespace Fudie.Firestore.IntegrationTest.Helpers.MapOf;

// ============================================================================
// TIPOS DE CLAVE PARA MAPOF
// ============================================================================

public enum DiaSemana
{
    Lunes,
    Martes,
    Miercoles,
    Jueves,
    Viernes,
    Sabado,
    Domingo
}

public enum TipoHabitacion
{
    Individual,
    Doble,
    Suite,
    Presidencial
}

// ============================================================================
// ELEMENTOS EMBEBIDOS PARA MAPOF
// ============================================================================

/// <summary>
/// Horario de un día específico
/// </summary>
public class HorarioDia
{
    public bool Cerrado { get; set; }
    public string? HoraApertura { get; set; }
    public string? HoraCierre { get; set; }
}

/// <summary>
/// Configuración de precios para una habitación
/// </summary>
public class ConfiguracionPrecio
{
    public decimal PrecioBase { get; set; }
    public decimal? PrecioTemporadaAlta { get; set; }
    public int CapacidadMaxima { get; set; }
}

/// <summary>
/// Configuración simple de valor
/// </summary>
public class ConfiguracionValor
{
    public required string Nombre { get; set; }
    public required string Valor { get; set; }
    public bool Activo { get; set; }
}

// ============================================================================
// ENTIDADES PRINCIPALES PARA MAPOF
// ============================================================================

/// <summary>
/// Restaurante con MapOf de horarios por día de la semana
/// IReadOnlyDictionary<DiaSemana, HorarioDia>
/// </summary>
public class RestauranteConHorarios
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Direccion { get; set; }

    private readonly Dictionary<DiaSemana, HorarioDia> _horariosSemanal = new();
    public IReadOnlyDictionary<DiaSemana, HorarioDia> HorariosSemanal => _horariosSemanal;

    public void SetHorario(DiaSemana dia, HorarioDia horario)
    {
        _horariosSemanal[dia] = horario;
    }
}

/// <summary>
/// Hotel con MapOf de configuraciones de precio por tipo de habitación
/// IReadOnlyDictionary<TipoHabitacion, ConfiguracionPrecio>
/// </summary>
public class HotelConPrecios
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public int Estrellas { get; set; }

    private readonly Dictionary<TipoHabitacion, ConfiguracionPrecio> _preciosHabitaciones = new();
    public IReadOnlyDictionary<TipoHabitacion, ConfiguracionPrecio> PreciosHabitaciones => _preciosHabitaciones;

    public void SetPrecio(TipoHabitacion tipo, ConfiguracionPrecio config)
    {
        _preciosHabitaciones[tipo] = config;
    }
}

/// <summary>
/// Aplicación con configuraciones por clave string
/// IReadOnlyDictionary<string, ConfiguracionValor>
/// </summary>
public class AplicacionConConfiguraciones
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Version { get; set; }

    private readonly Dictionary<string, ConfiguracionValor> _configuraciones = new();
    public IReadOnlyDictionary<string, ConfiguracionValor> Configuraciones => _configuraciones;

    public void SetConfiguracion(string clave, ConfiguracionValor config)
    {
        _configuraciones[clave] = config;
    }
}

/// <summary>
/// Almacén con inventario por sección (clave int)
/// IReadOnlyDictionary<int, ConfiguracionValor>
/// </summary>
public class AlmacenConSecciones
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Ubicacion { get; set; }

    private readonly Dictionary<int, ConfiguracionValor> _secciones = new();
    public IReadOnlyDictionary<int, ConfiguracionValor> Secciones => _secciones;

    public void SetSeccion(int numero, ConfiguracionValor config)
    {
        _secciones[numero] = config;
    }
}

// ============================================================================
// ENTIDADES CON DICTIONARY MUTABLE
// ============================================================================

/// <summary>
/// Tienda con categorías usando Dictionary mutable (no IReadOnlyDictionary)
/// </summary>
public class TiendaConCategorias
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }

    public Dictionary<string, ConfiguracionValor> Categorias { get; set; } = new();
}

// ============================================================================
// ENTIDADES PARA AUTO-DETECCIÓN (CONVENTION)
// ============================================================================

/// <summary>
/// Entidad sin configuración explícita de MapOf.
/// La Convention debe detectar automáticamente:
/// - Traducciones → MapOf con key string
/// </summary>
public class ProductoConTraducciones
{
    public string? Id { get; set; }
    public required string Codigo { get; set; }
    public decimal Precio { get; set; }

    private readonly Dictionary<string, ConfiguracionValor> _traducciones = new();
    public IReadOnlyDictionary<string, ConfiguracionValor> Traducciones => _traducciones;

    public void SetTraduccion(string idioma, ConfiguracionValor traduccion)
    {
        _traducciones[idioma] = traduccion;
    }
}

// ============================================================================
// CASO COMPLEJO 1: PROPIEDADES IGNORADAS EN ELEMENTOS
// ============================================================================

/// <summary>
/// Elemento con propiedad calculada que debe ser ignorada
/// </summary>
public class PrecioConDescuento
{
    public decimal PrecioBase { get; set; }
    public decimal PorcentajeDescuento { get; set; }

    // Propiedad calculada - debe ser ignorada en serialización
    public decimal PrecioFinal => PrecioBase * (1 - PorcentajeDescuento / 100);

    // Otra propiedad calculada a ignorar
    public string Descripcion => $"{PrecioBase:C} - {PorcentajeDescuento}% = {PrecioFinal:C}";
}

/// <summary>
/// Tienda con precios por categoría, donde PrecioFinal y Descripcion deben ignorarse
/// </summary>
public class TiendaConPreciosCalculados
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }

    private readonly Dictionary<string, PrecioConDescuento> _preciosPorCategoria = new();
    public IReadOnlyDictionary<string, PrecioConDescuento> PreciosPorCategoria => _preciosPorCategoria;

    public void SetPrecio(string categoria, PrecioConDescuento precio)
    {
        _preciosPorCategoria[categoria] = precio;
    }
}

// ============================================================================
// CASO COMPLEJO 2: ARRAYOF DENTRO DE ELEMENTOS DE MAPOF
// ============================================================================

/// <summary>
/// Franja horaria simple
/// </summary>
public class FranjaHoraria
{
    public required string Apertura { get; set; }
    public required string Cierre { get; set; }
}

/// <summary>
/// Horario de un día con múltiples franjas (mañana, tarde, noche)
/// Contiene un ArrayOf de FranjaHoraria
/// </summary>
public class HorarioConFranjas
{
    public bool Cerrado { get; set; }
    public string? Nota { get; set; }

    // ArrayOf anidado dentro del elemento del Map
    public List<FranjaHoraria> Franjas { get; set; } = [];
}

/// <summary>
/// Negocio con horarios por día donde cada día tiene múltiples franjas
/// MapOf<DiaSemana, HorarioConFranjas> donde HorarioConFranjas tiene List<FranjaHoraria>
/// </summary>
public class NegocioConHorariosFranjas
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Tipo { get; set; }

    private readonly Dictionary<DiaSemana, HorarioConFranjas> _horarios = new();
    public IReadOnlyDictionary<DiaSemana, HorarioConFranjas> Horarios => _horarios;

    public void SetHorario(DiaSemana dia, HorarioConFranjas horario)
    {
        _horarios[dia] = horario;
    }
}

// ============================================================================
// CASO COMPLEJO 3: REFERENCES DENTRO DE ELEMENTOS DE MAPOF
// ============================================================================

/// <summary>
/// Entidad Usuario que será referenciada
/// </summary>
public class Usuario
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }
    public required string Email { get; set; }
}

/// <summary>
/// Configuración de área con responsable (Reference a Usuario)
/// </summary>
public class ConfiguracionArea
{
    public required string Nombre { get; set; }
    public int Prioridad { get; set; }

    // Reference a entidad Usuario
    public Usuario? Responsable { get; set; }
}

/// <summary>
/// Empresa con áreas donde cada área tiene un responsable (Reference)
/// MapOf<string, ConfiguracionArea> donde ConfiguracionArea tiene Usuario Responsable
/// </summary>
public class EmpresaConAreas
{
    public string? Id { get; set; }
    public required string RazonSocial { get; set; }

    private readonly Dictionary<string, ConfiguracionArea> _areas = new();
    public IReadOnlyDictionary<string, ConfiguracionArea> Areas => _areas;

    public void SetArea(string codigo, ConfiguracionArea config)
    {
        _areas[codigo] = config;
    }
}

// ============================================================================
// CASO COMPLEJO 4: COMBINACIÓN DE ARRAYOF + REFERENCE EN ELEMENTO
// ============================================================================

/// <summary>
/// Turno de trabajo con empleados asignados
/// </summary>
public class TurnoTrabajo
{
    public required string Nombre { get; set; }
    public required string HoraInicio { get; set; }
    public required string HoraFin { get; set; }

    // ArrayOf References - lista de usuarios asignados al turno
    public List<Usuario> Empleados { get; set; } = [];
}

/// <summary>
/// Fábrica con turnos por día donde cada turno tiene empleados asignados
/// MapOf<DiaSemana, TurnoTrabajo> donde TurnoTrabajo tiene List<Usuario>
/// </summary>
public class FabricaConTurnos
{
    public string? Id { get; set; }
    public required string Nombre { get; set; }

    private readonly Dictionary<DiaSemana, TurnoTrabajo> _turnos = new();
    public IReadOnlyDictionary<DiaSemana, TurnoTrabajo> Turnos => _turnos;

    public void SetTurno(DiaSemana dia, TurnoTrabajo turno)
    {
        _turnos[dia] = turno;
    }
}
