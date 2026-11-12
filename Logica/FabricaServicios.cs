using Logica.Repositorios;
using Logica.Servicios;

namespace Logica
{
    /// <summary>
    /// UNA SOLA MISION: armar todas las piezas y conectarlas entre si (inyeccion de dependencias manual).
    /// La capa de Presentacion solo conoce esta clase y los servicios; nunca los repositorios.
    /// Asi, si manana cambias los archivos CSV por una base de datos, solo se toca Logica.
    /// </summary>
    public class FabricaServicios
    {
        public ServicioFichaTrabajador FichasTrabajador { get; }
        public ServicioPerfilCuenta PerfilCuenta { get; }
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
        public ServicioAsistenteIA AsistenteIA { get; }
        public ServicioAsistenteN8n AsistenteNube { get; }

        public FabricaServicios()
        {
            RepositorioUsuarios usuarios = new RepositorioUsuarios();
            RepositorioEmprendimientos emprendimientos = new RepositorioEmprendimientos();
            RepositorioCargos cargos = new RepositorioCargos();
            RepositorioTrabajadores trabajadores = new RepositorioTrabajadores(cargos);
            RepositorioProductos productos = new RepositorioProductos();
            RepositorioVentas ventas = new RepositorioVentas();
            RepositorioMovimientos movimientos = new RepositorioMovimientos();
            RepositorioPresupuestos presupuestos = new RepositorioPresupuestos();

            PerfilCuenta = new ServicioPerfilCuenta(new RepositorioPerfilesCuenta());
            Autenticacion = new ServicioAutenticacion(usuarios);
            Finanzas = new ServicioFinanzas(movimientos);
            Presupuestos = new ServicioPresupuestos(presupuestos, Finanzas);
            FichasTrabajador = new ServicioFichaTrabajador(new RepositorioFichasTrabajador(), trabajadores, emprendimientos);
            Emprendimientos = new ServicioEmprendimientos(emprendimientos, cargos, trabajadores, productos, ventas, movimientos, presupuestos, FichasTrabajador);
            Nomina = new ServicioNomina(cargos, trabajadores, emprendimientos, FichasTrabajador);
            Inventario = new ServicioInventario(productos, Finanzas);
            Ventas = new ServicioVentas(ventas, productos, Finanzas);
            Facturas = new ServicioFacturas();
            Reportes = new ServicioReportes(Ventas, Inventario, Finanzas);
            Asistente = new ServicioAsistente();
            AsistenteIA = new ServicioAsistenteIA(Asistente);
            AsistenteNube = new ServicioAsistenteN8n(AsistenteIA);
        }
    }
}