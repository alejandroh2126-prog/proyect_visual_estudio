namespace Entidades
{
    // =====================================================================
    // CATALOGOS (diccionarios de datos). El usuario los ELIGE de una lista,
    // no los escribe: asi se evitan errores de digitacion.
    // =====================================================================

    /// <summary>Municipio de Colombia (codigo DIVIPOLA del DANE).</summary>
    public class Ciudad
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Departamento { get; set; } = string.Empty;

        /// <summary>"Valledupar (Cesar)": es lo que se muestra y por lo que se busca.</summary>
        public string NombreCompleto { get; set; } = string.Empty;

        public override string ToString() { return NombreCompleto; }
    }

    /// <summary>Sector economico de un emprendimiento.</summary>
    public class Sector
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;

        public override string ToString() { return Nombre; }
    }

    /// <summary>Cargo tipico de un sector, con salario sugerido.</summary>
    public class CargoPlantilla
    {
        public int Id { get; set; }
        public int SectorId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal SalarioSugerido { get; set; }
        public string Descripcion { get; set; } = string.Empty;

        public override string ToString() { return Nombre; }
    }

    /// <summary>Tipo de documento de identidad con el patron que debe cumplir el numero.</summary>
    public class TipoDocumento
    {
        public string Codigo { get; set; } = string.Empty;
        public string Nombre { get; set; } = string.Empty;
        public string Patron { get; set; } = string.Empty;
        public string Ayuda { get; set; } = string.Empty;

        public override string ToString() { return Codigo + " - " + Nombre; }
    }

    /// <summary>Categoria de ingresos o gastos.</summary>
    public class CategoriaMovimiento
    {
        public const string TipoIngreso = "Ingreso";
        public const string TipoGasto = "Gasto";

        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;

        /// <summary>Las de sistema (Ventas, Inventario) solo las usa el programa automaticamente.</summary>
        public bool Sistema { get; set; }

        /// <summary>Ingresos que se le devuelven (en parte o con ganancia) a un socio, inversionista o prestamista.</summary>
        public static readonly string[] ConDevolucion = { "Aportes de socios", "Inversión", "Préstamos recibidos" };

        /// <summary>Gasto de sistema con el que se registra la devolucion.</summary>
        public const string DevolucionSocios = "Devolución a socios";

        public bool PermiteDevolucion { get { return Tipo == TipoIngreso && System.Array.IndexOf(ConDevolucion, Nombre) >= 0; } }

        public override string ToString() { return Nombre; }
    }
}
