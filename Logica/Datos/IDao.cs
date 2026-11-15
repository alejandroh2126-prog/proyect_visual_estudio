using System.Collections.Generic;
using Entidades;

namespace Logica.Datos
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

    public interface IUsuarioDao : IDao<Usuario> { }
    public interface IEmprendimientoDao : IDao<Emprendimiento> { }
    public interface ICargoDao : IDaoDeEmprendimiento<Cargo> { }
    public interface ITrabajadorDao : IDaoDeEmprendimiento<Trabajador> { }
    public interface IProductoDao : IDaoDeEmprendimiento<Producto> { }
    public interface IVentaDao : IDaoDeEmprendimiento<Venta> { }
    public interface IMovimientoDao : IDaoDeEmprendimiento<Movimiento> { }
    public interface IPresupuestoDao : IDaoDeEmprendimiento<Presupuesto> { }
}
