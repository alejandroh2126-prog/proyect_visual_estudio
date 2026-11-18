using System;
using System.Collections.Generic;
using Entidades;

namespace Datos
{
    // =====================================================================
    // PATRON DAO (Data Access Object). Los servicios de la capa Logica solo
    // conocen estas interfaces: no saben que detras hay Oracle, ni que SQL,
    // vistas, paquetes o procedimientos se usan.
    // =====================================================================

    public interface IUsuarioDao
    {
        Usuario? ObtenerPorEmail(string email);
        Usuario? ObtenerPorId(int id);
        Usuario Insertar(Usuario usuario);
    }

    /// <summary>Catalogos (diccionarios de datos). Solo lectura.</summary>
    public interface ICatalogoDao
    {
        List<Sector> Sectores();
        List<Ciudad> Ciudades();
        List<CargoPlantilla> PlantillasDeCargo(int sectorId);
        List<TipoDocumento> TiposDocumento();
        List<CategoriaMovimiento> Categorias();
        List<PreguntaFrecuente> PreguntasFrecuentes();
    }

    public interface IEmprendimientoDao
    {
        List<Emprendimiento> ListarPorUsuario(int usuarioId);
        Emprendimiento? ObtenerPorId(int id);
        Emprendimiento Insertar(Emprendimiento e);
        void Actualizar(Emprendimiento e);

        /// <summary>Oracle borra en cascada cargos, trabajadores, productos, ventas... (ON DELETE CASCADE).</summary>
        bool Eliminar(int id);
    }

    public interface ICargoDao
    {
        List<Cargo> ListarPorEmprendimiento(int emprendimientoId);
        Cargo? ObtenerPorId(int id);
        Cargo Insertar(Cargo c);
        void Actualizar(Cargo c);
        bool Eliminar(int id);
    }

    public interface ITrabajadorDao
    {
        List<Trabajador> ListarPorEmprendimiento(int emprendimientoId);
        Trabajador? ObtenerPorId(int id);
        Trabajador Insertar(Trabajador t);
        void Actualizar(Trabajador t);
        bool Eliminar(int id);
    }

    public interface IProductoDao
    {
        List<Producto> ListarPorEmprendimiento(int emprendimientoId);
        Producto? ObtenerPorId(int id);
        Producto Insertar(Producto p);

        /// <summary>Actualiza los datos SIN tocar el stock (el stock solo cambia por procedimiento).</summary>
        void Actualizar(Producto p);
        bool Eliminar(int id);
        List<string> CategoriasUsadas(int emprendimientoId);

        /// <summary>sp_sg_mover_stock: stock + movimiento en una sola transaccion.</summary>
        void MoverStock(int productoId, int cantidad, bool esEntrada);

        /// <summary>sp_sg_ajustar_precio_venta. Devuelve cuantos productos cambiaron.</summary>
        int AjustarPrecios(int emprendimientoId, decimal porcentaje);

        /// <summary>sp_sg_productos_stock_bajo.</summary>
        List<ProductoStockBajo> StockBajo(int emprendimientoId);
    }

    public interface IVentaDao
    {
        /// <summary>Historial desde vw_sg_ventas_resumen (total calculado por fn_sg_total_venta).</summary>
        List<Venta> ListarPorEmprendimiento(int emprendimientoId);
        Venta? ObtenerConDetalles(int id);

        /// <summary>sp_sg_registrar_venta: valida stock, guarda, descuenta y registra el ingreso. Devuelve el id.</summary>
        int Registrar(int emprendimientoId, string observacion, List<DetalleVenta> carrito);
    }

    public interface IMovimientoDao
    {
        List<Movimiento> ListarPorEmprendimiento(int emprendimientoId);
        Movimiento? ObtenerPorId(int id);
        Movimiento Insertar(Movimiento m);
        bool Eliminar(int id);
    }

    public interface IPresupuestoDao
    {
        /// <summary>sp_sg_presupuestos_estado: presupuestos del mes con gastado, porcentaje y estado.</summary>
        List<Presupuesto> EstadoDelMes(int emprendimientoId, string mes);
        Presupuesto? ObtenerPorId(int id);
        Presupuesto Insertar(Presupuesto p);
        void ActualizarLimite(int id, decimal montoLimite);
        bool Eliminar(int id);
    }

    public interface IVacanteDao
    {
        List<Vacante> ListarPorEmprendimiento(int emprendimientoId);

        /// <summary>Vacantes abiertas de todos los emprendimientos (las ve cualquier aspirante en el bot).</summary>
        List<Vacante> ListarAbiertas();
        Vacante? ObtenerPorId(int id);
        Vacante Insertar(Vacante v);
        void Actualizar(Vacante v);
        void CambiarEstado(int id, string estado);
        bool Eliminar(int id);
    }

    public interface IPostulacionDao
    {
        List<Postulacion> ListarPorVacante(int vacanteId);
        Postulacion? ObtenerPorId(int id);

        /// <summary>Guarda la postulacion con la hoja de vida en un BLOB.</summary>
        int Insertar(Postulacion p, ArchivoCv cv);
        ArchivoCv? ObtenerCv(int postulacionId);
        void CambiarEstado(int id, string estado);

        /// <summary>sp_sg_contratar_postulante: crea el trabajador. Devuelve su id.</summary>
        int Contratar(int postulacionId, DateTime fechaIngreso);
    }

    /// <summary>Indicadores calculados por Oracle: paquetes pkg_sg_nomina y pkg_sg_reportes, y procedimientos.</summary>
    public interface IReporteDao
    {
        ResumenEmprendimiento? Resumen(int emprendimientoId);
        List<ResumenEmprendimiento> ResumenesDelUsuario(int usuarioId);

        ResumenNomina Nomina(int emprendimientoId);
        List<FilaNomina> DetalleNomina(int emprendimientoId);

        ResumenFinanciero Finanzas(int emprendimientoId);
        decimal VentasEntre(int emprendimientoId, DateTime desde, DateTime hasta);
        decimal ValorInventario(int emprendimientoId);
        List<ValorEtiquetado> TopProductos(int emprendimientoId, int limite);
        List<ValorEtiquetado> VentasPorMes(int emprendimientoId, int meses);
        List<ValorEtiquetado> GastosPorCategoria(int emprendimientoId, string? mes);

        int LimpiarBitacora(int dias);
        int CerrarVacantesVencidas();
    }

    /// <summary>Paquete pkg_sg_telegram.</summary>
    public interface ITelegramDao
    {
        string GenerarCodigo(int usuarioId);
        VinculoTelegram Vincular(long chatId, string codigo);
        int? UsuarioDeChat(long chatId);
        int? EmprendimientoDeChat(long chatId);
        void FijarEmprendimiento(long chatId, int emprendimientoId);
        void Desvincular(long chatId);
        int ChatsVinculados(int usuarioId);
    }
}
