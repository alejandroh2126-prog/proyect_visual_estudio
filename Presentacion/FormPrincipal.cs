using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Utilidades;
using Presentacion.Comun;
using Presentacion.Vistas;

namespace Presentacion
{
    /// <summary>
    /// Ventana principal: menu lateral + selector de emprendimiento + la pestana activa.
    /// UNA SOLA MISION: organizar la navegacion. No calcula nada: cada vista se encarga de lo suyo.
    /// </summary>
    public class FormPrincipal : Form
    {
        private readonly FabricaServicios _fabrica;
        private readonly Dictionary<Button, VistaBase> _vistas = new Dictionary<Button, VistaBase>();
        private readonly Panel _contenido = new Panel();
        private readonly ComboBox _selector = new ComboBox();
        private readonly VistaEmprendimientos _vistaEmprendimientos;
        private VistaBase? _vistaActual;
        private bool _cargandoSelector;

        public FormPrincipal(FabricaServicios fabrica)
        {
            _fabrica = fabrica;

            Text = "SGAPE - Sistema de Gestión y Administración de Emprendimientos";
            Font = Tema.Normal;
            BackColor = Tema.Fondo;
            ClientSize = new Size(1240, 760);
            MinimumSize = new Size(1040, 680);
            StartPosition = FormStartPosition.CenterScreen;

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.Dock = DockStyle.Fill;
            raiz.ColumnCount = 2;
            raiz.RowCount = 2;
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230F));
            raiz.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            raiz.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
            raiz.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            _vistaEmprendimientos = new VistaEmprendimientos(fabrica);
            _vistaEmprendimientos.DatosCambiaron += delegate { CargarSelector(); };

            Control menu = ConstruirMenu();
            Control barra = ConstruirBarraSuperior();
            _contenido.Dock = DockStyle.Fill;
            _contenido.Margin = new Padding(0);

            raiz.Controls.Add(menu, 0, 0);
            raiz.SetRowSpan(menu, 2);
            raiz.Controls.Add(barra, 1, 0);
            raiz.Controls.Add(_contenido, 1, 1);
            Controls.Add(raiz);

            CargarSelector();
            Navegar(new List<Button>(_vistas.Keys)[0]);
        }

        // ---------------- Construccion de la interfaz ----------------

        private Control ConstruirMenu()
        {
            TableLayoutPanel menu = new TableLayoutPanel();
            menu.Dock = DockStyle.Fill;
            menu.BackColor = Tema.VerdeOscuro;
            menu.Margin = new Padding(0);
            menu.ColumnCount = 1;
            menu.RowCount = 3;
            menu.RowStyles.Add(new RowStyle(SizeType.Absolute, 80F));
            menu.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            menu.RowStyles.Add(new RowStyle(SizeType.Absolute, 56F));

            Label logo = new Label();
            logo.Text = "SGAPE";
            logo.Font = new Font("Segoe UI Semibold", 22F);
            logo.ForeColor = Color.White;
            logo.Dock = DockStyle.Fill;
            logo.TextAlign = ContentAlignment.MiddleCenter;

            FlowLayoutPanel opciones = new FlowLayoutPanel();
            opciones.Dock = DockStyle.Fill;
            opciones.FlowDirection = FlowDirection.TopDown;
            opciones.WrapContents = false;
            opciones.Margin = new Padding(0);

            Agregar(opciones, "Resumen", new VistaInicio(_fabrica));
            Agregar(opciones, "Emprendimientos", _vistaEmprendimientos);
            Agregar(opciones, "Nómina", new VistaNomina(_fabrica));
            Agregar(opciones, "Inventario", new VistaInventario(_fabrica));
            Agregar(opciones, "Ventas", new VistaVentas(_fabrica));
            Agregar(opciones, "Finanzas", new VistaFinanzas(_fabrica));
            Agregar(opciones, "Presupuestos", new VistaPresupuestos(_fabrica));
            Agregar(opciones, "Reportes", new VistaReportes(_fabrica));
            Agregar(opciones, "Asistente", new VistaAsistente(_fabrica));

            Button salir = CrearBotonMenu("Cerrar sesión");
            salir.Dock = DockStyle.Fill;
            salir.Margin = new Padding(0);
            salir.Click += delegate { CerrarSesion(); };

            menu.Controls.Add(logo, 0, 0);
            menu.Controls.Add(opciones, 0, 1);
            menu.Controls.Add(salir, 0, 2);
            return menu;
        }

        private void Agregar(FlowLayoutPanel panel, string texto, VistaBase vista)
        {
            Button boton = CrearBotonMenu(texto);
            boton.Click += delegate { Navegar(boton); };
            _vistas.Add(boton, vista);
            panel.Controls.Add(boton);
        }

        private static Button CrearBotonMenu(string texto)
        {
            Button b = new Button();
            b.Text = texto;
            b.Font = Tema.Normal;
            b.ForeColor = Color.White;
            b.BackColor = Tema.VerdeOscuro;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Padding = new Padding(22, 0, 0, 0);
            b.Cursor = Cursors.Hand;
            b.Size = new Size(230, 46);
            b.Margin = new Padding(0);
            return b;
        }

        private Control ConstruirBarraSuperior()
        {
            TableLayoutPanel barra = new TableLayoutPanel();
            barra.Dock = DockStyle.Fill;
            barra.BackColor = Color.White;
            barra.Margin = new Padding(0);
            barra.Padding = new Padding(20, 0, 20, 0);
            barra.ColumnCount = 3;
            barra.RowCount = 1;
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            barra.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Label etiqueta = Tema.Etiqueta("Emprendimiento:");
            etiqueta.Anchor = AnchorStyles.Left;

            _selector.DropDownStyle = ComboBoxStyle.DropDownList;
            _selector.Width = 300;
            _selector.Anchor = AnchorStyles.Left;
            _selector.SelectedIndexChanged += delegate
            {
                if (_cargandoSelector || _vistaActual == null) return;
                _vistaActual.Mostrar(EmprendimientoSeleccionado);
            };

            Label usuario = new Label();
            usuario.Text = Sesion.UsuarioActual != null ? Sesion.UsuarioActual.Nombre : "";
            usuario.ForeColor = Tema.TextoSuave;
            usuario.Dock = DockStyle.Fill;
            usuario.TextAlign = ContentAlignment.MiddleRight;

            barra.Controls.Add(etiqueta, 0, 0);
            barra.Controls.Add(_selector, 1, 0);
            barra.Controls.Add(usuario, 2, 0);
            return barra;
        }

        // ---------------- Comportamiento ----------------

        private Emprendimiento? EmprendimientoSeleccionado
        {
            get { return _selector.SelectedItem as Emprendimiento; }
        }

        private void CargarSelector()
        {
            _cargandoSelector = true;
            int idPrevio = EmprendimientoSeleccionado != null ? EmprendimientoSeleccionado.Id : -1;
            _selector.Items.Clear();

            Mensajes.Ejecutar(delegate
            {
                foreach (Emprendimiento e in _fabrica.Emprendimientos.ListarDelUsuario())
                {
                    int indice = _selector.Items.Add(e);
                    if (e.Id == idPrevio) _selector.SelectedIndex = indice;
                }
                if (_selector.SelectedIndex < 0 && _selector.Items.Count > 0) _selector.SelectedIndex = 0;
            });

            _cargandoSelector = false;
            if (_vistaActual != null) _vistaActual.Mostrar(EmprendimientoSeleccionado);
        }

        private void Navegar(Button boton)
        {
            foreach (KeyValuePair<Button, VistaBase> par in _vistas)
                par.Key.BackColor = par.Key == boton ? Tema.Verde : Tema.VerdeOscuro;

            _vistaActual = _vistas[boton];
            _contenido.Controls.Clear();
            _contenido.Controls.Add(_vistaActual);
            _vistaActual.Mostrar(EmprendimientoSeleccionado);
        }

        private void CerrarSesion()
        {
            if (!Mensajes.Confirmar("¿Cerrar la sesión?")) return;
            _fabrica.Autenticacion.CerrarSesion();
            DialogResult = DialogResult.Retry;
        }
    }
}