using System.Collections.Generic;
using Entidades;

namespace Datos
{
    /// <summary>
    /// PATRON DAO (Data Access Object). Un DAO es el UNICO que sabe donde y como se guardan los datos
    /// (Oracle u otra base de datos). Los servicios solo conocen estas interfaces, nunca una base de datos.
    /// Asi se puede cambiar de CSV a Oracle sin tocar ni una linea de la logica de negocio.
    /// </summary>
    public interface IDao<T> where T : class
    {
        List<T> Listar();
        T? ObtenerPorId(int id);

        /// <summary>Guarda la entidad nueva y le asigna el Id generado.</summary>
        T Insertar(T entidad);

        void Actualizar(T entidad);

        /// <summary>Elimina por Id. Devuelve false si no existia.</summary>
        bool Eliminar(int id);
    }

    /// <summary>DAO de las entidades que pertenecen a un emprendimiento.</summary>
    public interface IDaoDeEmprendimiento<T> : IDao<T> where T : class
    {
        /// <summary>Elimina todos los registros del emprendimiento. Devuelve cuantos elimino.</summary>
        int EliminarPorEmprendimiento(int emprendimientoId);
    }

    /// <summary>
    /// DAO de estadisticas: los numeros los calcula la BASE DE DATOS con sus funciones y paquetes PL/SQL
    /// (pkg_sg_nomina, pkg_sg_reportes) y el procedimiento sp_sg_limpiar_bitacora.
    /// </summary>
    public interface IEstadisticaDao
    {
        int TrabajadoresActivos(int emprendimientoId);
        decimal NominaMensual(int emprendimientoId);
        decimal NominaQuincenal(int emprendimientoId);
        decimal TotalDeducciones(int emprendimientoId);
        decimal NominaNeta(int emprendimientoId);
        decimal TotalIngresos(int emprendimientoId);
        decimal TotalGastos(int emprendimientoId);

        /// <summary>Borra de la bitacora los registros con mas de estos dias de antiguedad.</summary>
        void LimpiarBitacora(int dias);
    }

    public interface IUsuarioDao : IDao<Usuario> { }
    public interface IEmprendimientoDao : IDao<Emprendimiento> { }
    public interface ICargoDao : IDaoDeEmprendimiento<Cargo> { }
    public interface ITrabajadorDao : IDaoDeEmprendimiento<Trabajador> { }
    public interface IProductoDao : IDaoDeEmprendimiento<Producto> { }
    public interface IVentaDao : IDaoDeEmprendimiento<Venta> { }
    public interface IMovimientoDao : IDaoDeEmprendimiento<Movimiento> { }
    public interface IPresupuestoDao : IDaoDeEmprendimiento<Presupuesto> { }
}
