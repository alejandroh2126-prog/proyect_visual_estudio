using Datos.Oracle;

namespace Datos
{
    /// <summary>
    /// PATRON ABSTRACT FACTORY. Es la unica clase que decide QUE DAOs se crean.
    /// Cada fabrica concreta (hoy FabricaDaoOracle) crea la familia completa de DAOs de un mismo
    /// almacenamiento. Los servicios reciben DAOs ya creados y no saben de que fabrica salieron:
    /// para guardar en otra base de datos solo hay que crear otra fabrica concreta.
    /// </summary>
    public abstract class FabricaDao
    {
        public abstract IUsuarioDao CrearUsuarioDao();
        public abstract IEmprendimientoDao CrearEmprendimientoDao();
        public abstract ICargoDao CrearCargoDao();
        public abstract ITrabajadorDao CrearTrabajadorDao();
        public abstract IProductoDao CrearProductoDao();
        public abstract IVentaDao CrearVentaDao();
        public abstract IMovimientoDao CrearMovimientoDao();
        public abstract IPresupuestoDao CrearPresupuestoDao();
        public abstract IEstadisticaDao CrearEstadisticaDao();

        /// <summary>Comprueba que la base de datos esta lista. Lanza ReglaDeNegocioException si no lo esta.</summary>
        public virtual void Verificar()
        {
        }

        /// <summary>FACTORY METHOD: devuelve la fabrica de DAOs segun la configuracion de conexion.</summary>
        public static FabricaDao Crear(ConfiguracionBD config)
        {
            return new FabricaDaoOracle(config);
        }
    }
}
