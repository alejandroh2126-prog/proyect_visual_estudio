using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Entidades;
using Logica.Utilidades;

namespace Presentacion.Comun
{
    // =====================================================================
    // GRAFICAS ESTADISTICAS (barras, linea y dona/pastel)
    // Se dibujan con WPF (OnRender): no necesitan paquetes externos, se animan al cargar
    // y muestran el valor exacto al pasar el mouse.
    // =====================================================================

    /// <summary>Un dato de una grafica.</summary>
    public class PuntoGrafica
    {
        public string Etiqueta { get; set; } = string.Empty;
        public double Valor { get; set; }

        /// <summary>Segunda serie (barras dobles: ingresos y gastos).</summary>
        public double Valor2 { get; set; }

        /// <summary>Valor ya formateado (moneda o cantidad) para la burbuja y la leyenda.</summary>
        public string Texto { get; set; } = string.Empty;
        public string Texto2 { get; set; } = string.Empty;

        /// <summary>Porcentaje del total (pastel), por ejemplo "35 %".</summary>
        public string Porcentaje { get; set; } = string.Empty;

        /// <summary>Color propio (pastel y barras horizontales). Si es null se usa el de la grafica.</summary>
        public Brush? Color { get; set; }
    }

    /// <summary>Colores y formatos compartidos por las graficas.</summary>
    public static class PaletaGraficas
    {
        private static readonly CultureInfo Co = CultureInfo.GetCultureInfo("es-CO");

        private static readonly Color[] Colores =
        {
            Color.FromRgb(0x26, 0xA6, 0x9A), // verde azulado
            Color.FromRgb(0xFF, 0xA7, 0x26), // naranja
            Color.FromRgb(0x5C, 0x6B, 0xC0), // indigo
            Color.FromRgb(0xEF, 0x53, 0x50), // rojo
            Color.FromRgb(0xAB, 0x47, 0xBC), // morado
            Color.FromRgb(0x29, 0xB6, 0xF6), // azul claro
            Color.FromRgb(0x66, 0xBB, 0x6A), // verde
            Color.FromRgb(0x8D, 0x6E, 0x63)  // cafe
        };

        public static readonly Brush Verde = Congelado(Color.FromRgb(0x43, 0xA0, 0x47));
        public static readonly Brush Naranja = Congelado(Color.FromRgb(0xFB, 0x8C, 0x00));
        public static readonly Brush Rojo = Congelado(Color.FromRgb(0xE5, 0x39, 0x35));

        private static Brush Congelado(Color c)
        {
            SolidColorBrush b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static Brush Pincel(int i) { return Congelado(Colores[i % Colores.Length]); }

        /// <summary>Numero corto para ejes: $1,2 M · $850 mil · 35.</summary>
        public static string Compacto(double v, bool moneda)
        {
            double a = Math.Abs(v);
            string s;
            if (a >= 1e9) s = (v / 1e9).ToString("0.#", Co) + " mil M";
            else if (a >= 1e6) s = (v / 1e6).ToString("0.#", Co) + " M";
            else if (a >= 1e3) s = (v / 1e3).ToString("0.#", Co) + " mil";
            else s = v.ToString("0.##", Co);
            return moneda ? "$" + s : s;
        }

        /// <summary>Convierte valores de Oracle en puntos con color y porcentaje.</summary>
        public static List<PuntoGrafica> Desde(List<ValorEtiquetado> valores, bool moneda, bool conColores)
        {
            decimal total = 0m;
            foreach (ValorEtiquetado v in valores) total += v.Valor;
            List<PuntoGrafica> puntos = new List<PuntoGrafica>();
            for (int i = 0; i < valores.Count; i++)
            {
                ValorEtiquetado v = valores[i];
                puntos.Add(new PuntoGrafica
                {
                    Etiqueta = v.Etiqueta,
                    Valor = (double)v.Valor,
                    Texto = moneda ? Formato.Moneda(v.Valor) : v.Cantidad.ToString(Co),
                    Porcentaje = total > 0 ? Math.Round(v.Valor * 100m / total).ToString(Co) + " %" : string.Empty,
                    Color = conColores ? Pincel(i) : null
                });
            }
            return puntos;
        }

        /// <summary>Calcula un tope "redondo" para el eje y el paso entre lineas guia.</summary>
        public static void Escala(double maximo, int divisiones, out double tope, out double paso)
        {
            if (maximo <= 0) { tope = 1; paso = 0.25; return; }
            double crudo = maximo / divisiones;
            double magnitud = Math.Pow(10, Math.Floor(Math.Log10(crudo)));
            double r = crudo / magnitud;
            double redondo = r <= 1 ? 1 : r <= 2 ? 2 : r <= 2.5 ? 2.5 : r <= 5 ? 5 : 10;
            paso = redondo * magnitud;
            tope = Math.Ceiling(maximo / paso) * paso;
        }
    }

    /// <summary>Base de las graficas: datos, animacion de entrada, mouse y textos.</summary>
    public abstract class GraficaBase : Control
    {
        public static readonly DependencyProperty DatosProperty = DependencyProperty.Register(
            nameof(Datos), typeof(IEnumerable), typeof(GraficaBase), new PropertyMetadata(null, AlCambiarDatos));

        public static readonly DependencyProperty ProgresoProperty = DependencyProperty.Register(
            nameof(Progreso), typeof(double), typeof(GraficaBase), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty RellenoProperty = DependencyProperty.Register(
            nameof(Relleno), typeof(Brush), typeof(GraficaBase),
            new FrameworkPropertyMetadata(PaletaGraficas.Pincel(0), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty Relleno2Property = DependencyProperty.Register(
            nameof(Relleno2), typeof(Brush), typeof(GraficaBase),
            new FrameworkPropertyMetadata(PaletaGraficas.Pincel(1), FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty MonedaProperty = DependencyProperty.Register(
            nameof(Moneda), typeof(bool), typeof(GraficaBase), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty TextoVacioProperty = DependencyProperty.Register(
            nameof(TextoVacio), typeof(string), typeof(GraficaBase),
            new FrameworkPropertyMetadata("Aún no hay datos para mostrar", FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty Serie1Property = DependencyProperty.Register(
            nameof(Serie1), typeof(string), typeof(GraficaBase), new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty Serie2Property = DependencyProperty.Register(
            nameof(Serie2), typeof(string), typeof(GraficaBase), new PropertyMetadata(string.Empty));

        public IEnumerable? Datos { get { return (IEnumerable?)GetValue(DatosProperty); } set { SetValue(DatosProperty, value); } }
        public double Progreso { get { return (double)GetValue(ProgresoProperty); } set { SetValue(ProgresoProperty, value); } }
        public Brush Relleno { get { return (Brush)GetValue(RellenoProperty); } set { SetValue(RellenoProperty, value); } }
        public Brush Relleno2 { get { return (Brush)GetValue(Relleno2Property); } set { SetValue(Relleno2Property, value); } }
        public bool Moneda { get { return (bool)GetValue(MonedaProperty); } set { SetValue(MonedaProperty, value); } }
        public string TextoVacio { get { return (string)GetValue(TextoVacioProperty); } set { SetValue(TextoVacioProperty, value); } }
        public string Serie1 { get { return (string)GetValue(Serie1Property); } set { SetValue(Serie1Property, value); } }
        public string Serie2 { get { return (string)GetValue(Serie2Property); } set { SetValue(Serie2Property, value); } }

        protected readonly List<PuntoGrafica> Puntos = new List<PuntoGrafica>();
        protected int Resaltado = -1;

        private static readonly Brush FondoBurbuja = Congelar(new SolidColorBrush(Color.FromArgb(0xEE, 0x26, 0x32, 0x38)));

        private static Brush Congelar(Brush b) { b.Freeze(); return b; }

        static GraficaBase()
        {
            // Si cambia el tema (claro/oscuro o colores) la grafica se vuelve a dibujar con los colores nuevos
            ForegroundProperty.OverrideMetadata(typeof(GraficaBase), new FrameworkPropertyMetadata(
                SystemColors.ControlTextBrush, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
            BackgroundProperty.OverrideMetadata(typeof(GraficaBase), new FrameworkPropertyMetadata(
                null, FrameworkPropertyMetadataOptions.AffectsRender));
        }

        protected GraficaBase()
        {
            SnapsToDevicePixels = true;
            Loaded += (s, e) => Animar();
        }

        private static void AlCambiarDatos(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            GraficaBase g = (GraficaBase)d;
            g.Puntos.Clear();
            if (e.NewValue is IEnumerable lista)
                foreach (object o in lista)
                    if (o is PuntoGrafica p) g.Puntos.Add(p);
            g.Resaltado = -1;
            if (g.IsLoaded) g.Animar();
            g.InvalidateVisual();
        }

        private void Animar()
        {
            DoubleAnimation a = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(850))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            BeginAnimation(ProgresoProperty, a);
        }

        /// <summary>True si no hay puntos o todos valen cero.</summary>
        protected bool SinDatos
        {
            get
            {
                foreach (PuntoGrafica p in Puntos) if (p.Valor != 0 || p.Valor2 != 0) return false;
                return true;
            }
        }

        protected abstract int IndiceEn(Point posicion);
        protected abstract void Dibujar(DrawingContext dc);

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int i = SinDatos ? -1 : IndiceEn(e.GetPosition(this));
            if (i != Resaltado) { Resaltado = i; InvalidateVisual(); }
        }

        protected override void OnMouseLeave(MouseEventArgs e)
        {
            base.OnMouseLeave(e);
            if (Resaltado != -1) { Resaltado = -1; InvalidateVisual(); }
        }

        protected override void OnRender(DrawingContext dc)
        {
            // Fondo transparente: hace que el mouse se detecte en toda el area
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, ActualWidth, ActualHeight));
            if (ActualWidth < 40 || ActualHeight < 40) return;
            if (SinDatos)
            {
                FormattedText t = Texto(TextoVacio, 14, Tenue(0.55));
                dc.DrawText(t, new Point((ActualWidth - t.Width) / 2, (ActualHeight - t.Height) / 2));
                return;
            }
            Dibujar(dc);
        }

        // ---------------- utilidades de dibujo ----------------

        protected Color ColorTexto
        {
            get { return Foreground is SolidColorBrush s ? s.Color : Colors.Black; }
        }

        /// <summary>El color del texto con transparencia (lineas guia, textos secundarios).</summary>
        protected Brush Tenue(double opacidad)
        {
            Color c = ColorTexto;
            return Congelar(new SolidColorBrush(Color.FromArgb((byte)(opacidad * 255), c.R, c.G, c.B)));
        }

        protected static Color ColorDe(Brush? b, Color porDefecto)
        {
            return b is SolidColorBrush s ? s.Color : porDefecto;
        }

        protected FormattedText Texto(string s, double tamano, Brush color, bool negrita = false)
        {
            Typeface letra = new Typeface(FontFamily, FontStyles.Normal, negrita ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal);
            return new FormattedText(s, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, letra, tamano, color,
                                     VisualTreeHelper.GetDpi(this).PixelsPerDip);
        }

        /// <summary>Recorta el texto con "…" hasta que quepa en el ancho.</summary>
        protected FormattedText TextoQueCabe(string s, double tamano, Brush color, double ancho, bool negrita = false)
        {
            FormattedText t = Texto(s, tamano, color, negrita);
            if (t.Width <= ancho || s.Length <= 2) return t;
            int n = s.Length;
            while (n > 1)
            {
                n--;
                t = Texto(s.Substring(0, n).TrimEnd() + "…", tamano, color, negrita);
                if (t.Width <= ancho) break;
            }
            return t;
        }

        /// <summary>Burbuja oscura con el detalle del dato resaltado (siempre dentro de la grafica).</summary>
        protected void Burbuja(DrawingContext dc, Point punta, params string[] lineas)
        {
            List<FormattedText> textos = new List<FormattedText>();
            double ancho = 0, alto = 0;
            for (int i = 0; i < lineas.Length; i++)
            {
                if (string.IsNullOrEmpty(lineas[i])) continue;
                FormattedText t = Texto(lineas[i], i == 0 ? 12 : 13.5, Brushes.White, i > 0);
                textos.Add(t);
                ancho = Math.Max(ancho, t.Width);
                alto += t.Height + 2;
            }
            if (textos.Count == 0) return;
            double w = ancho + 20, h = alto + 12;
            double x = Math.Max(2, Math.Min(ActualWidth - w - 2, punta.X - w / 2));
            double y = punta.Y - h - 10;
            if (y < 2) y = Math.Min(ActualHeight - h - 2, punta.Y + 12);
            dc.DrawRoundedRectangle(FondoBurbuja, null, new Rect(x, y, w, h), 8, 8);
            double ty = y + 6;
            foreach (FormattedText t in textos)
            {
                dc.DrawText(t, new Point(x + 10, ty));
                ty += t.Height + 2;
            }
        }

        /// <summary>Lineas guia horizontales con su valor a la izquierda. Devuelve el area util de la grafica.</summary>
        protected Rect EjeVertical(DrawingContext dc, double maximo, out double tope)
        {
            PaletaGraficas.Escala(maximo, 4, out tope, out double paso);
            double margenIzq = 0;
            for (double v = 0; v <= tope + paso / 2; v += paso)
                margenIzq = Math.Max(margenIzq, Texto(PaletaGraficas.Compacto(v, Moneda), 11.5, Tenue(0.6)).Width);
            margenIzq += 10;
            Rect area = new Rect(margenIzq, 12, Math.Max(10, ActualWidth - margenIzq - 6), Math.Max(10, ActualHeight - 12 - 26));

            Pen guia = new Pen(Tenue(0.10), 1) { DashStyle = new DashStyle(new double[] { 3, 3 }, 0) };
            guia.Freeze();
            for (double v = 0; v <= tope + paso / 2; v += paso)
            {
                double y = Math.Round(area.Bottom - v / tope * area.Height) + 0.5;
                dc.DrawLine(v == 0 ? new Pen(Tenue(0.22), 1) : guia, new Point(area.Left, y), new Point(area.Right, y));
                FormattedText t = Texto(PaletaGraficas.Compacto(v, Moneda), 11.5, Tenue(0.6));
                dc.DrawText(t, new Point(margenIzq - 8 - t.Width, y - t.Height / 2));
            }
            return area;
        }

        /// <summary>Etiquetas del eje X; si no caben todas, se salta algunas.</summary>
        protected void EtiquetasX(DrawingContext dc, Rect area, IList<double> centros)
        {
            if (Puntos.Count == 0) return;
            double maxAncho = 0;
            foreach (PuntoGrafica p in Puntos) maxAncho = Math.Max(maxAncho, Texto(p.Etiqueta, 11.5, Tenue(0.65)).Width);
            double espacio = Puntos.Count > 1 ? Math.Abs(centros[1] - centros[0]) : area.Width;
            int salto = Math.Max(1, (int)Math.Ceiling((maxAncho + 8) / Math.Max(1, espacio)));
            for (int i = 0; i < Puntos.Count; i++)
            {
                if (i % salto != 0 && i != Resaltado) continue;
                FormattedText t = Texto(Puntos[i].Etiqueta, 11.5, i == Resaltado ? Tenue(0.95) : Tenue(0.65), i == Resaltado);
                double x = Math.Max(0, Math.Min(ActualWidth - t.Width, centros[i] - t.Width / 2));
                dc.DrawText(t, new Point(x, area.Bottom + 7));
            }
        }
    }

    /// <summary>Barras verticales (una o dos series) u horizontales (ranking, por ejemplo los mas vendidos).</summary>
    public class GraficaBarras : GraficaBase
    {
        public static readonly DependencyProperty HorizontalProperty = DependencyProperty.Register(
            nameof(Horizontal), typeof(bool), typeof(GraficaBarras), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

        public bool Horizontal { get { return (bool)GetValue(HorizontalProperty); } set { SetValue(HorizontalProperty, value); } }

        private bool DosSeries
        {
            get
            {
                foreach (PuntoGrafica p in Puntos) if (p.Valor2 != 0) return true;
                return !string.IsNullOrEmpty(Serie2);
            }
        }

        private Rect _area;
        private double _alturaFila;

        protected override int IndiceEn(Point pos)
        {
            if (Puntos.Count == 0) return -1;
            if (Horizontal)
            {
                int fila = (int)((pos.Y - _area.Top) / Math.Max(1, _alturaFila));
                return fila >= 0 && fila < Puntos.Count ? fila : -1;
            }
            if (pos.X < _area.Left || pos.X > _area.Right) return -1;
            int i = (int)((pos.X - _area.Left) / (_area.Width / Puntos.Count));
            return Math.Min(Puntos.Count - 1, Math.Max(0, i));
        }

        protected override void Dibujar(DrawingContext dc)
        {
            if (Horizontal) { DibujarHorizontal(dc); return; }

            bool dos = DosSeries;
            double maximo = 0;
            foreach (PuntoGrafica p in Puntos) maximo = Math.Max(maximo, Math.Max(p.Valor, dos ? p.Valor2 : 0));
            _area = EjeVertical(dc, maximo, out double tope);

            int n = Puntos.Count;
            double ranura = _area.Width / n;
            double anchoBarra = dos ? Math.Min(26, ranura * 0.32) : Math.Min(42, ranura * 0.56);
            double separacion = dos ? Math.Max(2, anchoBarra * 0.15) : 0;
            List<double> centros = new List<double>();

            for (int i = 0; i < n; i++)
            {
                PuntoGrafica p = Puntos[i];
                double centro = _area.Left + ranura * (i + 0.5);
                centros.Add(centro);
                if (i == Resaltado)
                    dc.DrawRoundedRectangle(Tenue(0.06), null, new Rect(centro - ranura / 2 + 2, _area.Top, ranura - 4, _area.Height), 8, 8);

                double h1 = Math.Max(0, p.Valor) / tope * _area.Height * Progreso;
                Brush b1 = p.Color ?? Relleno;
                if (!dos)
                {
                    Barra(dc, b1, new Rect(centro - anchoBarra / 2, _area.Bottom - h1, anchoBarra, h1), anchoBarra);
                }
                else
                {
                    double h2 = Math.Max(0, p.Valor2) / tope * _area.Height * Progreso;
                    Barra(dc, b1, new Rect(centro - separacion / 2 - anchoBarra, _area.Bottom - h1, anchoBarra, h1), anchoBarra);
                    Barra(dc, Relleno2, new Rect(centro + separacion / 2, _area.Bottom - h2, anchoBarra, h2), anchoBarra);
                }
            }
            EtiquetasX(dc, _area, centros);

            if (Resaltado >= 0 && Resaltado < n)
            {
                PuntoGrafica p = Puntos[Resaltado];
                double alto = Math.Max(p.Valor, dos ? p.Valor2 : 0) / tope * _area.Height;
                Point punta = new Point(centros[Resaltado], _area.Bottom - alto);
                if (dos)
                    Burbuja(dc, punta, p.Etiqueta, (Serie1.Length > 0 ? Serie1 + ": " : "") + p.Texto, (Serie2.Length > 0 ? Serie2 + ": " : "") + p.Texto2);
                else
                    Burbuja(dc, punta, p.Etiqueta, p.Texto);
            }
        }

        /// <summary>Barra con las esquinas de arriba redondeadas.</summary>
        private static void Barra(DrawingContext dc, Brush b, Rect r, double ancho)
        {
            if (r.Height < 0.5) return;
            double radio = Math.Min(Math.Min(6, ancho / 2), r.Height);
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(new Point(r.Left, r.Bottom), true, true);
                c.LineTo(new Point(r.Left, r.Top + radio), false, false);
                c.ArcTo(new Point(r.Left + radio, r.Top), new Size(radio, radio), 0, false, SweepDirection.Clockwise, false, false);
                c.LineTo(new Point(r.Right - radio, r.Top), false, false);
                c.ArcTo(new Point(r.Right, r.Top + radio), new Size(radio, radio), 0, false, SweepDirection.Clockwise, false, false);
                c.LineTo(new Point(r.Right, r.Bottom), false, false);
            }
            g.Freeze();
            dc.DrawGeometry(b, null, g);
        }

        private void DibujarHorizontal(DrawingContext dc)
        {
            int n = Puntos.Count;
            _area = new Rect(0, 0, ActualWidth, ActualHeight);
            _alturaFila = Math.Min(46, ActualHeight / n);
            double maximo = 0;
            foreach (PuntoGrafica p in Puntos) maximo = Math.Max(maximo, p.Valor);
            if (maximo <= 0) maximo = 1;

            double anchoNombre = Math.Min(170, ActualWidth * 0.36);
            double anchoValor = 0;
            foreach (PuntoGrafica p in Puntos) anchoValor = Math.Max(anchoValor, Texto(p.Texto, 12.5, Tenue(0.9), true).Width);
            double inicioBarra = anchoNombre + 10;
            double largoMax = Math.Max(10, ActualWidth - inicioBarra - anchoValor - 12);
            double grosor = Math.Min(16, _alturaFila * 0.42);

            for (int i = 0; i < n; i++)
            {
                PuntoGrafica p = Puntos[i];
                double yCentro = _area.Top + _alturaFila * (i + 0.5);
                if (i == Resaltado)
                    dc.DrawRoundedRectangle(Tenue(0.06), null, new Rect(0, yCentro - _alturaFila / 2 + 1, ActualWidth, _alturaFila - 2), 8, 8);

                FormattedText nombre = TextoQueCabe(p.Etiqueta, 13, Tenue(0.85), anchoNombre - 8);
                dc.DrawText(nombre, new Point(6, yCentro - nombre.Height / 2));

                Rect pista = new Rect(inicioBarra, yCentro - grosor / 2, largoMax, grosor);
                dc.DrawRoundedRectangle(Tenue(0.07), null, pista, grosor / 2, grosor / 2);
                double largo = Math.Max(grosor, p.Valor / maximo * largoMax * Progreso);
                dc.DrawRoundedRectangle(p.Color ?? Relleno, null, new Rect(inicioBarra, pista.Top, largo, grosor), grosor / 2, grosor / 2);

                FormattedText valor = Texto(p.Texto, 12.5, Tenue(0.9), true);
                dc.DrawText(valor, new Point(inicioBarra + largoMax + 8, yCentro - valor.Height / 2));
            }
        }
    }

    /// <summary>Linea con area sombreada (tendencia, por ejemplo ventas por dia).</summary>
    public class GraficaLinea : GraficaBase
    {
        private Rect _area;
        private readonly List<double> _xs = new List<double>();

        protected override int IndiceEn(Point pos)
        {
            if (_xs.Count == 0 || pos.X < _area.Left - 10 || pos.X > _area.Right + 10) return -1;
            int mejor = 0;
            for (int i = 1; i < _xs.Count; i++)
                if (Math.Abs(_xs[i] - pos.X) < Math.Abs(_xs[mejor] - pos.X)) mejor = i;
            return mejor;
        }

        protected override void Dibujar(DrawingContext dc)
        {
            double maximo = 0;
            foreach (PuntoGrafica p in Puntos) maximo = Math.Max(maximo, p.Valor);
            Rect eje = EjeVertical(dc, maximo, out double tope);
            // Margen a los lados para que el primer y el ultimo punto no queden pegados al borde
            _area = new Rect(eje.Left + 10, eje.Top, Math.Max(10, eje.Width - 20), eje.Height);

            int n = Puntos.Count;
            _xs.Clear();
            List<Point> puntos = new List<Point>();
            for (int i = 0; i < n; i++)
            {
                double x = n == 1 ? _area.Left + _area.Width / 2 : _area.Left + _area.Width * i / (n - 1);
                double y = _area.Bottom - Math.Max(0, Puntos[i].Valor) / tope * _area.Height;
                _xs.Add(x);
                puntos.Add(new Point(x, y));
            }

            Color c = ColorDe(Relleno, Colors.Teal);
            // La linea "aparece" de izquierda a derecha
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, _area.Left + (_area.Width + 12) * Progreso, ActualHeight)));

            StreamGeometry areaGeo = new StreamGeometry();
            using (StreamGeometryContext g = areaGeo.Open())
            {
                g.BeginFigure(new Point(puntos[0].X, eje.Bottom), true, true);
                foreach (Point p in puntos) g.LineTo(p, false, true);
                g.LineTo(new Point(puntos[n - 1].X, eje.Bottom), false, false);
            }
            areaGeo.Freeze();
            LinearGradientBrush degradado = new LinearGradientBrush(Color.FromArgb(0x66, c.R, c.G, c.B), Color.FromArgb(0x05, c.R, c.G, c.B), 90);
            degradado.Freeze();
            dc.DrawGeometry(degradado, null, areaGeo);

            StreamGeometry linea = new StreamGeometry();
            using (StreamGeometryContext g = linea.Open())
            {
                g.BeginFigure(puntos[0], false, false);
                for (int i = 1; i < n; i++) g.LineTo(puntos[i], true, true);
            }
            linea.Freeze();
            Pen lapiz = new Pen(Relleno, 3) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
            lapiz.Freeze();
            dc.DrawGeometry(null, lapiz, linea);

            Pen borde = new Pen(Relleno, 2);
            borde.Freeze();
            Brush fondoPunto = Background ?? Brushes.White;
            for (int i = 0; i < n; i++)
                if (Puntos[i].Valor > 0 || n <= 16)
                    dc.DrawEllipse(fondoPunto, borde, puntos[i], 3.5, 3.5);
            dc.Pop();

            EtiquetasX(dc, eje, _xs);

            if (Resaltado >= 0 && Resaltado < n)
            {
                Point p = puntos[Resaltado];
                Pen guia = new Pen(Tenue(0.25), 1) { DashStyle = DashStyles.Dash };
                guia.Freeze();
                dc.DrawLine(guia, new Point(p.X, _area.Top), new Point(p.X, eje.Bottom));
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x40, c.R, c.G, c.B)), null, p, 11, 11);
                dc.DrawEllipse(Relleno, new Pen(Brushes.White, 2), p, 5.5, 5.5);
                PuntoGrafica d = Puntos[Resaltado];
                Burbuja(dc, p, d.Etiqueta, d.Texto, d.Texto2);
            }
        }
    }

    /// <summary>Pastel en forma de dona; en el centro muestra el total o el dato que se señala con el mouse.</summary>
    public class GraficaDona : GraficaBase
    {
        public static readonly DependencyProperty CentroProperty = DependencyProperty.Register(
            nameof(Centro), typeof(string), typeof(GraficaDona), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        public static readonly DependencyProperty CentroDetalleProperty = DependencyProperty.Register(
            nameof(CentroDetalle), typeof(string), typeof(GraficaDona), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>Grosor del anillo como fraccion del radio (1 = pastel completo).</summary>
        public static readonly DependencyProperty GrosorProperty = DependencyProperty.Register(
            nameof(Grosor), typeof(double), typeof(GraficaDona), new FrameworkPropertyMetadata(0.36, FrameworkPropertyMetadataOptions.AffectsRender));

        public string Centro { get { return (string)GetValue(CentroProperty); } set { SetValue(CentroProperty, value); } }
        public string CentroDetalle { get { return (string)GetValue(CentroDetalleProperty); } set { SetValue(CentroDetalleProperty, value); } }
        public double Grosor { get { return (double)GetValue(GrosorProperty); } set { SetValue(GrosorProperty, value); } }

        private Point _centro;
        private double _radio, _interno;

        private double Total
        {
            get
            {
                double t = 0;
                foreach (PuntoGrafica p in Puntos) t += Math.Max(0, p.Valor);
                return t;
            }
        }

        protected override int IndiceEn(Point pos)
        {
            Vector v = pos - _centro;
            double d = v.Length;
            if (d < _interno * 0.9 || d > _radio + 10) return -1;
            // Angulo desde las 12 en punto, en el sentido del reloj
            double angulo = Math.Atan2(v.X, -v.Y) * 180 / Math.PI;
            if (angulo < 0) angulo += 360;
            double total = Total, acumulado = 0;
            for (int i = 0; i < Puntos.Count; i++)
            {
                acumulado += Math.Max(0, Puntos[i].Valor) / total * 360;
                if (angulo <= acumulado) return i;
            }
            return Puntos.Count - 1;
        }

        private static Point EnCirculo(Point c, double r, double grados)
        {
            double rad = grados * Math.PI / 180;
            return new Point(c.X + r * Math.Sin(rad), c.Y - r * Math.Cos(rad));
        }

        protected override void Dibujar(DrawingContext dc)
        {
            _radio = Math.Min(ActualWidth, ActualHeight) / 2 - 8;
            _interno = _radio * (1 - Math.Clamp(Grosor, 0.1, 1));
            _centro = new Point(ActualWidth / 2, ActualHeight / 2);
            double total = Total;
            double inicio = 0;
            double barrido = 360 * Progreso;

            for (int i = 0; i < Puntos.Count; i++)
            {
                double angulo = Math.Max(0, Puntos[i].Valor) / total * 360;
                double desde = inicio, hasta = Math.Min(inicio + angulo, barrido);
                inicio += angulo;
                if (hasta <= desde) continue;
                double extra = i == Resaltado ? 6 : 0;
                Segmento(dc, Puntos[i].Color ?? PaletaGraficas.Pincel(i), desde, hasta, _radio + extra, Math.Max(0, _interno - extra / 2));
            }

            // Texto del centro: el dato señalado o el total
            string grande = Centro, pequeno = CentroDetalle;
            if (Resaltado >= 0 && Resaltado < Puntos.Count)
            {
                PuntoGrafica p = Puntos[Resaltado];
                grande = p.Porcentaje.Length > 0 ? p.Porcentaje : p.Texto;
                pequeno = p.Etiqueta + (p.Porcentaje.Length > 0 ? " · " + p.Texto : string.Empty);
            }
            if (_interno > 30)
            {
                double ancho = _interno * 1.7;
                FormattedText g = TextoQueCabe(grande, Math.Max(14, Math.Min(26, _interno * 0.36)), Tenue(0.95), ancho, true);
                FormattedText c = TextoQueCabe(pequeno, 12, Tenue(0.62), ancho);
                double alto = g.Height + (pequeno.Length > 0 ? c.Height : 0);
                dc.DrawText(g, new Point(_centro.X - g.Width / 2, _centro.Y - alto / 2));
                if (pequeno.Length > 0) dc.DrawText(c, new Point(_centro.X - c.Width / 2, _centro.Y - alto / 2 + g.Height));
            }
        }

        private void Segmento(DrawingContext dc, Brush b, double desde, double hasta, double radio, double interno)
        {
            double barrido = hasta - desde;
            if (barrido >= 359.9)
            {
                // Un solo dato: anillo completo
                double medio = (radio + interno) / 2;
                Pen anillo = new Pen(b, Math.Max(1, radio - interno));
                dc.DrawEllipse(null, anillo, _centro, medio, medio);
                return;
            }
            bool grande = barrido > 180;
            StreamGeometry g = new StreamGeometry();
            using (StreamGeometryContext c = g.Open())
            {
                c.BeginFigure(EnCirculo(_centro, radio, desde), true, true);
                c.ArcTo(EnCirculo(_centro, radio, hasta), new Size(radio, radio), 0, grande, SweepDirection.Clockwise, true, true);
                if (interno > 0.5)
                {
                    c.LineTo(EnCirculo(_centro, interno, hasta), true, true);
                    c.ArcTo(EnCirculo(_centro, interno, desde), new Size(interno, interno), 0, grande, SweepDirection.Counterclockwise, true, true);
                }
                else
                {
                    c.LineTo(_centro, true, true);
                }
            }
            g.Freeze();
            // Linea fina del color del fondo entre porciones
            Pen separador = new Pen(Background ?? Brushes.Transparent, 2);
            dc.DrawGeometry(b, Background != null ? separador : null, g);
        }
    }
}
