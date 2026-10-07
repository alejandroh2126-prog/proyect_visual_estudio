using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Presentacion.Comun
{
    /// <summary>Tarjeta con un numero grande y una etiqueta (ej: "Balance  $1.200.000").</summary>
    public class TarjetaKpi : Panel
    {
        private readonly Label _valor;

        public TarjetaKpi(string titulo, Color color)
        {
            BackColor = Color.White;
            Height = 84;
            Margin = new Padding(0, 0, 12, 12);
            Padding = new Padding(14, 8, 14, 8);

            _valor = new Label();
            _valor.Text = "-";
            _valor.ForeColor = color;
            _valor.Font = Tema.Grande;
            _valor.AutoSize = false;
            _valor.Dock = DockStyle.Fill;
            _valor.TextAlign = ContentAlignment.MiddleLeft;

            Label etiqueta = new Label();
            etiqueta.Text = titulo;
            etiqueta.ForeColor = Tema.TextoSuave;
            etiqueta.Font = Tema.Normal;
            etiqueta.AutoSize = false;
            etiqueta.Height = 24;
            etiqueta.Dock = DockStyle.Bottom;

            // Primero el que llena (Fill) y despues el que se pega al borde.
            Controls.Add(_valor);
            Controls.Add(etiqueta);
        }

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public string Valor
        {
            get { return _valor.Text; }
            set { _valor.Text = value; }
        }
    }
}