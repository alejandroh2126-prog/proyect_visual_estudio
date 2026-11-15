namespace Datos.Oracle
{
    /// <summary>Fabrica concreta: crea los DAOs que guardan los datos en una base de datos Oracle.</summary>
    public class FabricaDaoOracle : FabricaDao
    {
        private readonly ConfiguracionBD _config;

        public FabricaDaoOracle(ConfiguracionBD config)
        {
            _config = config;
            ConexionOracle.Configurar(config);
        }

        public override void Verificar()
        {
            ConexionOracle.Probar();
        }

        public override IUsuarioDao CrearUsuarioDao() { return new UsuarioDaoOracle(); }
        public override IEmprendimientoDao CrearEmprendimientoDao() { return new EmprendimientoDaoOracle(); }
        public override ICargoDao CrearCargoDao() { return new CargoDaoOracle(); }
        public override ITrabajadorDao CrearTrabajadorDao() { return new TrabajadorDaoOracle(); }
        public override IProductoDao CrearProductoDao() { return new ProductoDaoOracle(); }
        public override IVentaDao CrearVentaDao() { return new VentaDaoOracle(); }
        public override IMovimientoDao CrearMovimientoDao() { return new MovimientoDaoOracle(); }
        public override IPresupuestoDao CrearPresupuestoDao() { return new PresupuestoDaoOracle(); }
    }
}
