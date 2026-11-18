using System;
using Datos;
using Logica.Servicios;

namespace Logica
{
    /// <summary>
    /// UNA SOLA MISION: armar todas las piezas (inyeccion de dependencias manual).
    /// La aplicacion de escritorio y el bot de Telegram usan ESTA MISMA fabrica, asi que comparten
    /// todas las reglas de negocio y todos los objetos de Oracle.
    /// </summary>
    public class FabricaServicios
    {
        public ServicioAutenticacion Autenticacion { get; }
        public ServicioCatalogos Catalogos { get; }
        public ServicioEmprendimientos Emprendimientos { get; }
        public ServicioNomina Nomina { get; }
        public ServicioInventario Inventario { get; }
        public ServicioVentas Ventas { get; }
        public ServicioFinanzas Finanzas { get; }
        public ServicioPresupuestos Presupuestos { get; }
        public ServicioVacantes Vacantes { get; }
        public ServicioReportes Reportes { get; }
        public ServicioTelegram Telegram { get; }
        public ServicioConfiguracion Configuracion { get; }
        public ServicioPlanes Planes { get; }

        private readonly IReporteDao _reportes;

        public FabricaServicios() : this(ConfiguracionBD.Cargar())
        {
        }

        public FabricaServicios(ConfiguracionBD configuracion)
        {
            // Si no hay conexion o faltan objetos de Oracle, lanza ReglaDeNegocioException con el motivo.
            FabricaDao fabricaDao = FabricaDao.Crear(configuracion);
            fabricaDao.Verificar();

            IProductoDao productos = fabricaDao.CrearProductoDao();
            ICargoDao cargos = fabricaDao.CrearCargoDao();
            _reportes = fabricaDao.CrearReporteDao();

            IUsuarioDao usuarios = fabricaDao.CrearUsuarioDao();
            Planes = new ServicioPlanes(usuarios);
            Autenticacion = new ServicioAutenticacion(usuarios);
            Catalogos = new ServicioCatalogos(fabricaDao.CrearCatalogoDao(), productos);
            Emprendimientos = new ServicioEmprendimientos(fabricaDao.CrearEmprendimientoDao(), _reportes, Planes);
            Nomina = new ServicioNomina(cargos, fabricaDao.CrearTrabajadorDao(), _reportes, Catalogos);
            Inventario = new ServicioInventario(productos);
            Ventas = new ServicioVentas(fabricaDao.CrearVentaDao(), productos, _reportes);
            Finanzas = new ServicioFinanzas(fabricaDao.CrearMovimientoDao(), _reportes, Catalogos);
            Presupuestos = new ServicioPresupuestos(fabricaDao.CrearPresupuestoDao(), Catalogos);
            Vacantes = new ServicioVacantes(fabricaDao.CrearVacanteDao(), fabricaDao.CrearPostulacionDao(), cargos, Catalogos);
            Reportes = new ServicioReportes(_reportes, Ventas, Inventario, Presupuestos, Planes);
            Telegram = new ServicioTelegram(fabricaDao.CrearTelegramDao(), Planes);
            Configuracion = new ServicioConfiguracion(fabricaDao.CrearConfiguracionDao());
        }

        /// <summary>
        /// Mantenimiento con procedimientos de Oracle: sp_sg_limpiar_bitacora (registros de mas de un año)
        /// y sp_sg_cerrar_vacantes_vencidas. Es un extra: si falla, el programa sigue.
        /// </summary>
        public void Mantenimiento()
        {
            try { _reportes.LimpiarBitacora(365); } catch (Exception) { }
            try { _reportes.CerrarVacantesVencidas(); } catch (Exception) { }
        }
    }
}
