using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Entidades;
using Logica;
using Presentacion.Comun;
using Presentacion.Ventanas;

namespace Presentacion
{
    public partial class App : Application
    {
        /// <summary>Todos los servicios (la misma fabrica que usa el bot de Telegram).</summary>
        public static FabricaServicios Servicios { get; private set; } = null!;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            DispatcherUnhandledException += ErrorNoControlado;
            Apariencia.Cargar();   // colores y tamaño que el usuario eligio (seccion Apariencia)

            VentanaCarga carga = new VentanaCarga();
            carga.Show();
            try
            {
                // Conectar a Oracle puede tardar unos segundos: se hace fuera del hilo de la interfaz.
                Servicios = await Fondo.Run(() =>
                {
                    FabricaServicios f = new FabricaServicios();
                    f.Mantenimiento();
                    f.Catalogos.Sectores();
                    f.Catalogos.Ciudades();
                    f.Catalogos.TiposDocumento();
                    f.Catalogos.Categorias();
                    return f;
                });
            }
            catch (ReglaDeNegocioException ex)
            {
                carga.Close();
                MessageBox.Show(ex.Message, "SGAPE - No se pudo iniciar", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }
            catch (Exception ex)
            {
                carga.Close();
                MessageBox.Show("No se pudo iniciar SGAPE:\n" + ex.Message, "SGAPE", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }

            carga.Close();
            MostrarLogin();
        }

        public static void MostrarLogin()
        {
            Comun.Sesion.Cerrar();
            VentanaLogin login = new VentanaLogin();
            login.Show();
        }

        private void ErrorNoControlado(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            string mensaje = e.Exception is ReglaDeNegocioException ? e.Exception.Message : "Ocurrió un error inesperado: " + e.Exception.Message;
            Comun.Notificador.Error(mensaje);
            e.Handled = true;
        }
    }
}
