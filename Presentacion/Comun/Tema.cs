using System;
using System.Drawing;
using System.Windows.Forms;

namespace Presentacion.Comun
{
    /// <summary>
    /// UNA SOLA MISION: colores, fuentes y "fabricas" de controles con el estilo de SGAPE.
    /// Asi cada ventana no repite 15 lineas de estilo por cada boton o tabla.
    /// </summary>
    public static class Tema
    {
        public static readonly Color VerdeOscuro = Color.FromArgb(8, 80, 65);
        public static readonly Color Verde = Color.FromArgb(29, 158, 117);
        public static readonly Color VerdeClaro = Color.FromArgb(225, 245, 238);
        public static readonly Color Fondo = Color.FromArgb(240, 244, 248);
        public static readonly Color Texto = Color.FromArgb(26, 26, 24);
        public static readonly Color TextoSuave = Color.FromArgb(110, 110, 105);
        public static readonly Color Rojo = Color.FromArgb(226, 75, 74);
        public static readonly Color RojoClaro = Color.FromArgb(255, 240, 240);
        public static readonly Color Dorado = Color.FromArgb(186, 117, 23);
        public static readonly Color Gris = Color.FromArgb(100, 116, 139);

        public static readonly Font Normal = new Font("Segoe UI", 10F);
        public static readonly Font Negrita = new Font("Segoe UI", 10F, FontStyle.Bold);
        public static readonly Font Titulo = new Font("Segoe UI Semibold", 18F);
        public static readonly Font Grande = new Font("Segoe UI Semibold", 20F);

        // ---------------- Controles ----------------

        public static Button Boton(string texto, Color fondo)
        {
            Button b = new Button();
            b.Text = texto;
            b.BackColor = fondo;
            b.ForeColor = Color.White;
            b.Font = Negrita;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Cursor = Cursors.Hand;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.Padding = new Padding(10, 4, 10, 4);
            b.MinimumSize = new Size(0, 36);
            b.Margin = new Padding(0, 0, 8, 8);
            return b;
        }

        public static Label Etiqueta(string texto)
        {
            Label l = new Label();
            l.Text = texto;
            l.Font = Normal;
            l.ForeColor = Texto;
            l.AutoSize = true;
            l.Margin = new Padding(0, 6, 8, 6);
            return l;
        }

        public static Label TituloVista(string texto)
        {
            Label l = new Label();
            l.Text = texto;
            l.Font = Titulo;
            l.ForeColor = VerdeOscuro;
            l.AutoSize = true;
            l.Margin = new Padding(0, 0, 0, 10);
            return l;
        }

        public static NumericUpDown Numero(decimal maximo, int decimales)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = 0;
            n.Maximum = maximo;
            n.DecimalPlaces = decimales;
            n.ThousandsSeparator = true;
            n.Font = Normal;
            return n;
        }

        public static DataGridView Tabla(params string[] columnas)
        {
            DataGridView t = new DataGridView();
            t.ReadOnly = true;
            t.AllowUserToAddRows = false;
            t.AllowUserToDeleteRows = false;
            t.AllowUserToResizeRows = false;
            t.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            t.MultiSelect = false;
            t.RowHeadersVisible = false;
            t.BackgroundColor = Color.White;
            t.BorderStyle = BorderStyle.None;
            t.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            t.GridColor = Color.FromArgb(230, 232, 235);
            t.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            t.EnableHeadersVisualStyles = false;
            t.Font = Normal;
            t.ColumnHeadersDefaultCellStyle.BackColor = VerdeOscuro;
            t.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            t.ColumnHeadersDefaultCellStyle.SelectionBackColor = VerdeOscuro;
            t.ColumnHeadersDefaultCellStyle.Font = Negrita;
            t.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            t.ColumnHeadersHeight = 36;
            t.RowTemplate.Height = 30;
            t.DefaultCellStyle.SelectionBackColor = VerdeClaro;
            t.DefaultCellStyle.SelectionForeColor = Texto;
            foreach (string c in columnas) t.Columns.Add(c, c);
            return t;
        }

        public static TableLayoutPanel ColumnaVertical()
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 1;
            t.RowCount = 0;
            t.BackColor = Color.Transparent;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            return t;
        }

        public static FlowLayoutPanel Barra(params Control[] controles)
        {
            FlowLayoutPanel f = new FlowLayoutPanel();
            f.AutoSize = true;
            f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            f.WrapContents = true;
            f.FlowDirection = FlowDirection.LeftToRight;
            f.BackColor = Color.Transparent;
            f.Margin = new Padding(0);
            f.Controls.AddRange(controles);
            return f;
        }

        public static TableLayoutPanel FilaTarjetas(params TarjetaKpi[] tarjetas)
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.ColumnCount = tarjetas.Length;
            t.RowCount = 1;
            t.BackColor = Color.Transparent;
            t.Margin = new Padding(0);
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            for (int i = 0; i < tarjetas.Length; i++)
            {
                t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / tarjetas.Length));
                tarjetas[i].Dock = DockStyle.Fill;
                t.Controls.Add(tarjetas[i], i, 0);
            }
            return t;
        }

        // ---------------- Metodos de extension (atajos) ----------------

        /// <summary>Agrega una fila a un TableLayoutPanel vertical. expandir = ocupa el espacio sobrante.</summary>
        public static void Fila(this TableLayoutPanel t, Control c, bool expandir)
        {
            t.RowStyles.Add(expandir ? new RowStyle(SizeType.Percent, 100F) : new RowStyle(SizeType.AutoSize));
            t.RowCount = t.RowStyles.Count;
            c.Dock = DockStyle.Fill;
            t.Controls.Add(c, 0, t.RowStyles.Count - 1);
        }

        /// <summary>Agrega una fila a la tabla guardando el objeto (Tag) para recuperarlo al seleccionar.</summary>
        public static DataGridViewRow AgregarFila(this DataGridView t, object tag, params object[] celdas)
        {
            int indice = t.Rows.Add(celdas);
            DataGridViewRow fila = t.Rows[indice];
            fila.Tag = tag;
            return fila;
        }

        public static T? Seleccionado<T>(this DataGridView t) where T : class
        {
            if (t.SelectedRows.Count == 0) return null;
            return t.SelectedRows[0].Tag as T;
        }

        public static void AlinearDerecha(this DataGridView t, params int[] columnas)
        {
            foreach (int c in columnas)
                t.Columns[c].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
    }
}