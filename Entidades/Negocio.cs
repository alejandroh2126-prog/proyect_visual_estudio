using System;
using System.Collections.Generic;

namespace Entidades
{
    /// <summary>Valores permitidos de estado (iguales a los CHECK de Oracle).</summary>
    public static class Estados
    {
        public const string Activo = "activo";
        public const string Inactivo = "inactivo";
        public const string Abierta = "abierta";
        public const string Cerrada = "cerrada";
    }

    /// <summary>Estados de una postulacion.</summary>
    public static class EstadosPostulacion
    {
        public const string Recibida = "recibida";
        public const string EnRevision = "en_revision";
        public const string Aceptada = "aceptada";
        public const string Rechazada = "rechazada";
    }

    /// <summary>Tipos de movimiento del historial financiero.</summary>
    public enum TipoMovimiento
    {
        Venta,
        Compra,
        AjusteStock,
        Gasto,
        Ingreso
    }

    /// <summary>Usuario que inicia sesion. La clave nunca se guarda en texto plano.</summary>
    public class Usuario
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string ClaveHash { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; } = DateTime.Today;
    }

    /// <summary>Negocio de un usuario. El sector y la ciudad son llaves a los catalogos.</summary>
    public class Emprendimiento
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public int SectorId { get; set; }
        public string Sector { get; set; } = string.Empty;
        public string CiudadCodigo { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public DateTime FechaInicio { get; set; } = DateTime.Today;
        public string Estado { get; set; } = Estados.Activo;

        public override string ToString() { return Nombre; }
    }

    /// <summary>
    /// Cargo laboral. Los valores de nomina (quincena, deducciones, neto) los calcula Oracle
    /// con fn_sg_deducciones y fn_sg_salario_neto en la vista vw_sg_cargos.
    /// </summary>
    public class Cargo
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public int? PlantillaId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal SalarioBase { get; set; }
        public string Descripcion { get; set; } = string.Empty;

        public decimal SalarioQuincenal { get; set; }
        public decimal Deducciones { get; set; }
        public decimal SalarioNeto { get; set; }
        public int TrabajadoresActivos { get; set; }

        public override string ToString() { return Nombre; }
    }

    /// <summary>
    /// PILAR: ABSTRACCION. Datos comunes a cualquier persona.
    /// El documento se maneja en DOS campos: tipo (catalogo) y numero.
    /// </summary>
    public abstract class Persona
    {
        private string _nombre = string.Empty;
        private string _apellido = string.Empty;
        private string _numeroDocumento = string.Empty;

        public string TipoDocumento { get; set; } = "CC";
        public string TipoDocumentoNombre { get; set; } = string.Empty;

        /// <summary>PILAR: ENCAPSULAMIENTO. Se guarda sin puntos ni espacios.</summary>
        public string NumeroDocumento
        {
            get { return _numeroDocumento; }
            set { _numeroDocumento = (value ?? string.Empty).Replace(".", "").Replace(" ", "").Trim().ToUpperInvariant(); }
        }

        public string Nombre
        {
            get { return _nombre; }
            set { _nombre = (value ?? string.Empty).Trim(); }
        }

        public string Apellido
        {
            get { return _apellido; }
            set { _apellido = (value ?? string.Empty).Trim(); }
        }

        public string Telefono { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CiudadCodigo { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;

        public string NombreCompleto { get { return (Nombre + " " + Apellido).Trim(); } }

        /// <summary>PILAR: POLIMORFISMO. Cada clase hija dice que tipo de persona es.</summary>
        public abstract string TipoPersona { get; }

        public override string ToString() { return NombreCompleto; }
    }

    /// <summary>PILAR: HERENCIA. Trabajador es una Persona con datos del empleo.</summary>
    public class Trabajador : Persona
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public int CargoId { get; set; }
        public string Cargo { get; set; } = string.Empty;
        public decimal SalarioBase { get; set; }
        public DateTime FechaIngreso { get; set; } = DateTime.Today;
        public string Estado { get; set; } = Estados.Activo;

        public override string TipoPersona { get { return "Trabajador"; } }
        public bool EstaActivo { get { return Estado == Estados.Activo; } }
    }

    /// <summary>Aspirante que envio su hoja de vida por Telegram a una vacante.</summary>
    public class Postulacion : Persona
    {
        public int Id { get; set; }
        public int VacanteId { get; set; }
        public int EmprendimientoId { get; set; }
        public string Vacante { get; set; } = string.Empty;
        public string CvNombre { get; set; } = string.Empty;
        public string CvTipo { get; set; } = string.Empty;
        public long CvBytes { get; set; }
        public long? TelegramChatId { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Estado { get; set; } = EstadosPostulacion.Recibida;

        public override string TipoPersona { get { return "Aspirante"; } }
    }

    /// <summary>Archivo de hoja de vida (vive en un BLOB de Oracle).</summary>
    public class ArchivoCv
    {
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public byte[] Contenido { get; set; } = Array.Empty<byte>();
    }

    /// <summary>Oferta de empleo de un emprendimiento.</summary>
    public class Vacante
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public string Emprendimiento { get; set; } = string.Empty;
        public int CargoId { get; set; }
        public string Cargo { get; set; } = string.Empty;
        public decimal SalarioBase { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string CiudadCodigo { get; set; } = string.Empty;
        public string Ciudad { get; set; } = string.Empty;
        public DateTime FechaPublicacion { get; set; } = DateTime.Today;
        public DateTime? FechaCierre { get; set; }
        public string Estado { get; set; } = Estados.Abierta;
        public int Postulaciones { get; set; }

        public bool EstaAbierta { get { return Estado == Estados.Abierta; } }
        public override string ToString() { return Titulo; }
    }

    /// <summary>Producto del inventario. El margen y el stock bajo vienen de la vista vw_sg_margen_producto.</summary>
    public class Producto
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Categoria { get; set; } = string.Empty;
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }

        /// <summary>Solo se escribe al crear el producto; despues cambia con sp_sg_mover_stock o con una venta.</summary>
        public int Stock { get; set; }
        public int StockMinimo { get; set; }

        public bool StockBajo { get; set; }
        public decimal MargenValor { get; set; }
        public decimal MargenPorcentaje { get; set; }
        public decimal ValorInventario { get; set; }

        public override string ToString() { return Nombre; }
    }

    /// <summary>Una linea de una venta.</summary>
    public class DetalleVenta
    {
        public int ProductoId { get; set; }
        public string ProductoNombre { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }

        public decimal Subtotal { get { return Cantidad * PrecioUnitario; } }
    }

    /// <summary>Venta (cabecera). El total lo calcula fn_sg_total_venta en la vista vw_sg_ventas_resumen.</summary>
    public class Venta
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public string Observacion { get; set; } = string.Empty;
        public int Productos { get; set; }
        public decimal Total { get; set; }
        public List<DetalleVenta> Detalles { get; set; } = new List<DetalleVenta>();
    }

    /// <summary>Movimiento del historial financiero.</summary>
    public class Movimiento
    {
        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public DateTime Fecha { get; set; } = DateTime.Now;
        public TipoMovimiento Tipo { get; set; }
        public int CategoriaId { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public decimal Monto { get; set; }
        public string Referencia { get; set; } = string.Empty;

        public bool EsIngreso { get { return Tipo == TipoMovimiento.Venta || Tipo == TipoMovimiento.Ingreso; } }
        public bool EsGasto { get { return Tipo == TipoMovimiento.Compra || Tipo == TipoMovimiento.Gasto; } }

        /// <summary>Los movimientos automaticos (ventas, inventario) tienen referencia y no se borran a mano.</summary>
        public bool EsManual { get { return string.IsNullOrEmpty(Referencia); } }
    }

    /// <summary>
    /// Presupuesto mensual con su estado. Gastado, porcentaje y estado los calcula Oracle
    /// (vista vw_sg_uso_presupuesto, procedimiento sp_sg_presupuestos_estado).
    /// </summary>
    public class Presupuesto
    {
        public const string EstadoOk = "OK";
        public const string EstadoAlerta = "ALERTA";
        public const string EstadoExcedido = "EXCEDIDO";

        public int Id { get; set; }
        public int EmprendimientoId { get; set; }
        public int CategoriaId { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public decimal MontoLimite { get; set; }
        public string Mes { get; set; } = string.Empty;
        public decimal Gastado { get; set; }
        public decimal Disponible { get; set; }
        public decimal PorcentajeUso { get; set; }
        public string Estado { get; set; } = EstadoOk;
    }
}
