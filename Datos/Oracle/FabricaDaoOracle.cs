namespace Datos.Oracle
{
    /// <summary>Fabrica concreta: DAOs que trabajan con Oracle (tablas, vistas, paquetes y procedimientos).</summary>
    public class FabricaDaoOracle : FabricaDao
    {
        public FabricaDaoOracle(ConfiguracionBD config)
        {
            ConexionOracle.Configurar(config);
        }

        public override void Verificar() { ConexionOracle.Probar(); }

        public override IUsuarioDao CrearUsuarioDao() { return new UsuarioDaoOracle(); }
        public override ICatalogoDao CrearCatalogoDao() { return new CatalogoDaoOracle(); }
        public override IEmprendimientoDao CrearEmprendimientoDao() { return new EmprendimientoDaoOracle(); }
        public override ICargoDao CrearCargoDao() { return new CargoDaoOracle(); }
        public override ITrabajadorDao CrearTrabajadorDao() { return new TrabajadorDaoOracle(); }
        public override IProductoDao CrearProductoDao() { return new ProductoDaoOracle(); }
        public override IVentaDao CrearVentaDao() { return new VentaDaoOracle(); }
        public override IMovimientoDao CrearMovimientoDao() { return new MovimientoDaoOracle(); }
        public override IPresupuestoDao CrearPresupuestoDao() { return new PresupuestoDaoOracle(); }
        public override IVacanteDao CrearVacanteDao() { return new VacanteDaoOracle(); }
        public override IPostulacionDao CrearPostulacionDao() { return new PostulacionDaoOracle(); }
        public override IReporteDao CrearReporteDao() { return new ReporteDaoOracle(); }
        public override ITelegramDao CrearTelegramDao() { return new TelegramDaoOracle(); }
        public override IConfiguracionDao CrearConfiguracionDao() { return new ConfiguracionDaoOracle(); }
    }
}
