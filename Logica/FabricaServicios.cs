using System;
using Datos;
using Logica.Servicios;
using Logica.Utilidades;

namespace Logica
{
    /// <summary>
    /// UNA SOLA MISION: armar todas las piezas y conectarlas entre si (inyeccion de dependencias manual).
    /// La capa de Presentacion solo conoce esta clase y los servicios; nunca los DAOs.
    /// Los DAOs se piden a una FabricaDao (Oracle, segun conexion.json); los servicios solo ven interfaces.
    /// </summary>
    public class FabricaServicios
    {
        public ServicioAutenticacion Autenticacion { get; }
        public ServicioEmprendimientos Emprendimientos { get; }
        public ServicioNomina Nomina { get; }
        public ServicioFinanzas Finanzas { get; }
        public ServicioPresupuestos Presupuestos { get; }
        public ServicioInventario Inventario { get; }
        public ServicioVentas Ventas { get; }
        public ServicioFacturas Facturas { get; }
        public ServicioReportes Reportes { get; }
        public ServicioAsistente Asistente { get; }
        public ServicioAsistenteN8n AsistenteNube { get; }

        public FabricaServicios()
        {
            // Si no hay conexion.json o Oracle no responde, esto lanza ReglaDeNegocioException y Program.cs la muestra.
            FabricaDao fabricaDao = FabricaDao.Crear(ConfiguracionBD.Cargar());
            fabricaDao.Verificar();

            IUsuarioDao usuarios = fabricaDao.CrearUsuarioDao();
            IEmprendimientoDao emprendimientos = fabricaDao.CrearEmprendimientoDao();
            ICargoDao cargos = fabricaDao.CrearCargoDao();
            ITrabajadorDao trabajadores = fabricaDao.CrearTrabajadorDao();
            IProductoDao productos = fabricaDao.CrearProductoDao();
            IVentaDao ventas = fabricaDao.CrearVentaDao();
            IMovimientoDao movimientos = fabricaDao.CrearMovimientoDao();
            IPresupuestoDao presupuestos = fabricaDao.CrearPresupuestoDao();

            Autenticacion = new ServicioAutenticacion(usuarios);
            Finanzas = new ServicioFinanzas(movimientos);
            Presupuestos = new ServicioPresupuestos(presupuestos, Finanzas);
            Emprendimientos = new ServicioEmprendimientos(emprendimientos, cargos, trabajadores, productos, ventas, movimientos, presupuestos);
            Nomina = new ServicioNomina(cargos, trabajadores);
            Inventario = new ServicioInventario(productos, Finanzas);
            Ventas = new ServicioVentas(ventas, productos, Finanzas);
            Facturas = new ServicioFacturas();
            Reportes = new ServicioReportes(Ventas, Inventario, Finanzas);
            Asistente = new ServicioAsistente();
            AsistenteNube = new ServicioAsistenteN8n(Asistente);
        }
    }
}