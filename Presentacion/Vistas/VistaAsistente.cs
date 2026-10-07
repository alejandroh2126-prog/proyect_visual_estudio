using System;
using System.Windows.Forms;
using Logica;
using Presentacion.Comun;

namespace Presentacion.Vistas
{
    /// <summary>Chat con el asistente de ayuda (el chatbot del proyecto web, ahora dentro de la app).</summary>
    public class VistaAsistente : VistaBase
    {
        private readonly TextBox _chat = new TextBox();
        private readonly TextBox _entrada = new TextBox();

        public VistaAsistente(FabricaServicios fabrica) : base(fabrica)
        {
            _chat.Multiline = true;
            _chat.ReadOnly = true;
            _chat.ScrollBars = ScrollBars.Vertical;
            _chat.BackColor = System.Drawing.Color.White;
            _chat.BorderStyle = BorderStyle.None;
            _chat.Font = Tema.Normal;
            _chat.Text = "Asistente: ¡Hola! Pregúntame cómo usar SGAPE o cómo administrar tu emprendimiento." + Environment.NewLine + Environment.NewLine;

            _entrada.Font = Tema.Normal;
            _entrada.Dock = DockStyle.Fill;
            _entrada.Margin = new Padding(0, 0, 8, 0);
            _entrada.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    Enviar();
                }
            };

            Button enviar = Tema.Boton("Enviar", Tema.Verde);
            enviar.Margin = new Padding(0);
            enviar.Click += delegate { Enviar(); };

            TableLayoutPanel barra = new TableLayoutPanel();
            barra.ColumnCount = 2;
            barra.RowCount = 1;
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            barra.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            barra.Margin = new Padding(0, 10, 0, 0);
            barra.Controls.Add(_entrada, 0, 0);
            barra.Controls.Add(enviar, 1, 0);

            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Fila(Tema.TituloVista("Asistente"), false);
            raiz.Fila(_chat, true);
            raiz.Fila(barra, false);
            Controls.Add(raiz);
        }

        protected override void Recargar()
        {
            _entrada.Focus();
        }

        private void Enviar()
        {
            string pregunta = _entrada.Text.Trim();
            if (pregunta.Length == 0) return;
            _entrada.Clear();
            _chat.AppendText("Tú: " + pregunta + Environment.NewLine);
            _chat.AppendText("Asistente: " + Fabrica.Asistente.Responder(pregunta) + Environment.NewLine + Environment.NewLine);
        }
    }
}