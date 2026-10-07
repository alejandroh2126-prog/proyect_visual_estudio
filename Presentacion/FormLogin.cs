using System;
using System.Drawing;
using System.Windows.Forms;
using Logica;
using Presentacion.Comun;

namespace Presentacion
{
    /// <summary>Ventana de inicio de sesion y registro (la misma ventana cambia de modo).</summary>
    public class FormLogin : Form
    {
        private readonly FabricaServicios _fabrica;
        private bool _modoRegistro;

        private readonly Label _titulo = new Label();
        private readonly Label _subtitulo = new Label();
        private readonly Label _etiquetaNombre = new Label();
        private readonly TextBox _nombre = new TextBox();
        private readonly TextBox _correo = new TextBox();
        private readonly TextBox _clave = new TextBox();
        private readonly Button _principal;
        private readonly Button _cambiar;

        public FormLogin(FabricaServicios fabrica)
        {
            _fabrica = fabrica;

            Text = "SGAPE - Iniciar sesión";
            Font = Tema.Normal;
            BackColor = Tema.VerdeOscuro;
            ClientSize = new Size(440, 520);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            FlowLayoutPanel panel = new FlowLayoutPanel();
            panel.Dock = DockStyle.Fill;
            panel.FlowDirection = FlowDirection.TopDown;
            panel.WrapContents = false;
            panel.Padding = new Padding(50, 30, 50, 20);

            _titulo.Text = "SGAPE";
            _titulo.Font = new Font("Segoe UI Semibold", 30F);
            _titulo.ForeColor = Color.White;
            _titulo.AutoSize = true;

            _subtitulo.Text = "Gestión y administración de emprendimientos";
            _subtitulo.ForeColor = Tema.VerdeClaro;
            _subtitulo.AutoSize = true;
            _subtitulo.Margin = new Padding(3, 0, 3, 24);

            _etiquetaNombre.Text = "Nombre completo";
            _etiquetaNombre.ForeColor = Color.White;
            _etiquetaNombre.AutoSize = true;

            Label etiquetaCorreo = new Label();
            etiquetaCorreo.Text = "Correo electrónico";
            etiquetaCorreo.ForeColor = Color.White;
            etiquetaCorreo.AutoSize = true;

            Label etiquetaClave = new Label();
            etiquetaClave.Text = "Contraseña (mínimo 6 caracteres)";
            etiquetaClave.ForeColor = Color.White;
            etiquetaClave.AutoSize = true;

            _nombre.Width = 330;
            _correo.Width = 330;
            _clave.Width = 330;
            _clave.UseSystemPasswordChar = true;

            _principal = Tema.Boton("Iniciar sesión", Tema.Verde);
            _principal.Margin = new Padding(3, 20, 3, 8);
            _principal.Width = 330;
            _principal.AutoSize = false;
            _principal.Height = 42;
            _principal.Click += delegate { Enviar(); };

            _cambiar = new Button();
            _cambiar.FlatStyle = FlatStyle.Flat;
            _cambiar.FlatAppearance.BorderSize = 0;
            _cambiar.ForeColor = Tema.VerdeClaro;
            _cambiar.BackColor = Tema.VerdeOscuro;
            _cambiar.Cursor = Cursors.Hand;
            _cambiar.Width = 330;
            _cambiar.Height = 34;
            _cambiar.Click += delegate { CambiarModo(); };

            panel.Controls.Add(_titulo);
            panel.Controls.Add(_subtitulo);
            panel.Controls.Add(_etiquetaNombre);
            panel.Controls.Add(_nombre);
            panel.Controls.Add(etiquetaCorreo);
            panel.Controls.Add(_correo);
            panel.Controls.Add(etiquetaClave);
            panel.Controls.Add(_clave);
            panel.Controls.Add(_principal);
            panel.Controls.Add(_cambiar);
            Controls.Add(panel);

            AcceptButton = _principal;
            AplicarModo();
        }

        private void CambiarModo()
        {
            _modoRegistro = !_modoRegistro;
            AplicarModo();
        }

        private void AplicarModo()
        {
            _etiquetaNombre.Visible = _modoRegistro;
            _nombre.Visible = _modoRegistro;
            _principal.Text = _modoRegistro ? "Crear cuenta" : "Iniciar sesión";
            _cambiar.Text = _modoRegistro ? "¿Ya tienes cuenta? Inicia sesión" : "¿No tienes cuenta? Regístrate";
            Text = _modoRegistro ? "SGAPE - Crear cuenta" : "SGAPE - Iniciar sesión";
        }

        private void Enviar()
        {
            bool bien = Mensajes.Ejecutar(delegate
            {
                if (_modoRegistro) _fabrica.Autenticacion.Registrar(_nombre.Text, _correo.Text, _clave.Text);
                else _fabrica.Autenticacion.IniciarSesion(_correo.Text, _clave.Text);
            });
            if (bien) DialogResult = DialogResult.OK;
        }
    }
}