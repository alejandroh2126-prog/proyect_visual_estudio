using Datos.Oracle;

namespace Datos
{
    /// <summary>
    /// PATRON ABSTRACT FACTORY. Crea la familia completa de DAOs de un mismo almacenamiento.
    /// Hoy la unica fabrica concreta es FabricaDaoOracle; para otra base de datos bastaria otra fabrica.
    /// </summary>
    public abstract class FabricaDao
    {
        public abstract IUsuarioDao CrearUsuarioDao();
        public abstract ICatalogoDao CrearCatalogoDao();
        public abstract IEmprendimientoDao CrearEmprendimientoDao();
        public abstract ICargoDao CrearCargoDao();
        public abstract ITrabajadorDao CrearTrabajadorDao();
        public abstract IProductoDao CrearProductoDao();
        public abstract IVentaDao CrearVentaDao();
        public abstract IMovimientoDao CrearMovimientoDao();
        public abstract IPresupuestoDao CrearPresupuestoDao();
        public abstract IVacanteDao CrearVacanteDao();
        public abstract IPostulacionDao CrearPostulacionDao();
        public abstract IReporteDao CrearReporteDao();
        public abstract ITelegramDao CrearTelegramDao();

        /// <summary>Comprueba que la base de datos esta lista. Lanza ReglaDeNegocioException si no.</summary>
        public abstract void Verificar();

        /// <summary>FACTORY METHOD: la fabrica segun la configuracion.</summary>
        public static FabricaDao Crear(ConfiguracionBD config)
        {
            return new FabricaDaoOracle(config);
        }
    }
}
