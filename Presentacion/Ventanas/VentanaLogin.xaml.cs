using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using Entidades;
using Presentacion.Comun;

namespace Presentacion.Ventanas
{
    /// <summary>Inicio de sesion y registro. La clave va en un PasswordBox (no se puede enlazar por seguridad).</summary>
    public partial class VentanaLogin : Window
    {
        private bool _registro;
        private bool _entro;

        public VentanaLogin()
        {
            InitializeComponent();
            Loaded += (s, e) => CajaCorreo.Focus();
        }

        private void Cambiar_Click(object sender, RoutedEventArgs e)
        {
            _registro = !_registro;
            Visibility v = _registro ? Visibility.Visible : Visibility.Collapsed;
            CajaNombre.Visibility = v;
            CajaConfirmacion.Visibility = v;
            Titulo.Text = _registro ? "Crear cuenta" : "Iniciar sesión";
            BotonPrincipal.Content = _registro ? "Crear cuenta" : "Entrar";
            BotonCambiar.Content = _registro ? "¿Ya tienes cuenta? Iniciar sesión" : "¿No tienes cuenta? Crear cuenta";
            Mensaje.Visibility = Visibility.Collapsed;
        }

        private async void Principal_Click(object sender, RoutedEventArgs e)
        {
            string nombre = CajaNombre.Text, correo = CajaCorreo.Text, clave = CajaClave.Password, confirmacion = CajaConfirmacion.Password;
            bool registro = _registro;

            // Validacion de interfaz: campos vacios (las reglas completas estan en ServicioAutenticacion)
            if (correo.Trim().Length == 0 || clave.Length == 0 || (registro && nombre.Trim().Length == 0))
            {
                MostrarError("Completa los campos marcados con *.");
                return;
            }

            BotonPrincipal.IsEnabled = false;
            Cargando.Visibility = Visibility.Visible;
            Mensaje.Visibility = Visibility.Collapsed;
            try
            {
                Usuario u = await Task.Run(() => registro
                    ? App.Servicios.Autenticacion.Registrar(nombre, correo, clave, confirmacion)
                    : App.Servicios.Autenticacion.IniciarSesion(correo, clave));
                Sesion.Usuario = u;
                _entro = true;
                new VentanaPrincipal().Show();
                Close();
            }
            catch (ReglaDeNegocioException ex)
            {
                MostrarError(ex.Message);
            }
            catch (Exception ex)
            {
                MostrarError("Ocurrió un error inesperado: " + ex.Message);
            }
            finally
            {
                BotonPrincipal.IsEnabled = true;
                Cargando.Visibility = Visibility.Collapsed;
                CajaClave.Clear();
                CajaConfirmacion.Clear();
            }
        }

        private void MostrarError(string texto)
        {
            Mensaje.Text = texto;
            Mensaje.Visibility = Visibility.Visible;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!_entro) Application.Current.Shutdown();
        }
    }
}
