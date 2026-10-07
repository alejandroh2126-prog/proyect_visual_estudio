using System;
using System.Drawing;
using System.Windows.Forms;

namespace Presentacion.Comun
{
    /// <summary>
    /// PILAR: ABSTRACCION. Molde de todas las ventanas emergentes (formularios).
    /// La clase hija solo agrega campos con Campo(...) y escribe que hacer en Aceptar().
    /// Si Aceptar() lanza un error de negocio, se muestra y la ventana sigue abierta
    /// (el usuario no pierde lo que escribio).
    /// </summary>
    public abstract class DialogoBase : Form
    {
        private readonly TableLayoutPanel _campos;

        protected DialogoBase(string titulo)
        {
            Text = titulo;
            Font = Tema.Normal;
            BackColor = Color.White;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            MinimumSize = new Size(460, 0);

            TableLayoutPanel raiz = new TableLayoutPanel();
            raiz.AutoSize = true;
            raiz.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            raiz.ColumnCount = 1;
            raiz.RowCount = 2;
            raiz.Padding = new Padding(20);
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            raiz.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            _campos = new TableLayoutPanel();
            _campos.AutoSize = true;
            _campos.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _campos.ColumnCount = 2;
            _campos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            _campos.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Button guardar = Tema.Boton("Guardar", Tema.Verde);
            Button cancelar = Tema.Boton("Cancelar", Tema.Gris);
            guardar.Click += delegate
            {
                bool bien = Mensajes.Ejecutar(Aceptar);
                if (bien) DialogResult = DialogResult.OK;
            };
            cancelar.Click += delegate { DialogResult = DialogResult.Cancel; };

            FlowLayoutPanel botones = Tema.Barra(guardar, cancelar);
            botones.Margin = new Padding(0, 14, 0, 0);

            raiz.Controls.Add(_campos, 0, 0);
            raiz.Controls.Add(botones, 0, 1);
            Controls.Add(raiz);

            AcceptButton = guardar;
            CancelButton = cancelar;
        }

        /// <summary>Agrega una fila "etiqueta + control" y devuelve el control para poder leerlo despues.</summary>
        protected T Campo<T>(string etiqueta, T control) where T : Control
        {
            int fila = _campos.RowCount;
            _campos.RowCount = fila + 1;
            _campos.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            Label l = Tema.Etiqueta(etiqueta);
            l.Margin = new Padding(0, 8, 12, 8);
            control.Width = 280;
            control.Margin = new Padding(0, 4, 0, 4);

            _campos.Controls.Add(l, 0, fila);
            _campos.Controls.Add(control, 1, fila);
            return control;
        }

        /// <summary>Se ejecuta al pulsar Guardar. Lanzar ReglaDeNegocioException para avisar un error.</summary>
        protected abstract void Aceptar();
    }
}