using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace Presentacion.Comun
{
    /// <summary>Lo que el usuario eligio en la seccion Apariencia (se guarda en este computador).</summary>
    public class PreferenciasApariencia
    {
        public bool Oscuro { get; set; }
        public string ColorPrincipal { get; set; } = "#00897B";
        public string ColorAcento { get; set; } = "#FFB300";

        /// <summary>Fondo de las pantallas en modo claro y en modo oscuro (vacio = el de Material Design).</summary>
        public string FondoClaro { get; set; } = "#F3F6F9";
        public string FondoOscuro { get; set; } = string.Empty;

        /// <summary>Tamaño de toda la interfaz (letras, botones y tablas): 1 = 100 %.</summary>
        public double Escala { get; set; } = 1.0;

        public bool MenuAbierto { get; set; } = true;
    }

    /// <summary>Un color que se puede elegir (muestras de la seccion Apariencia).</summary>
    public class OpcionColor
    {
        public string Nombre { get; }
        public string Hex { get; }
        public Brush Pincel { get; }

        public OpcionColor(string nombre, string hex)
        {
            Nombre = nombre;
            Hex = hex;
            Pincel = Apariencia.PincelDe(hex);
        }
    }

    /// <summary>
    /// Colores, modo claro/oscuro y tamaño de la interfaz. Usa PaletteHelper de Material Design:
    /// al cambiar el tema TODOS los botones, barras, tablas y la ventana se actualizan al instante.
    /// </summary>
    public static class Apariencia
    {
        public static PreferenciasApariencia Actual { get; private set; } = new PreferenciasApariencia();

        /// <summary>Se avisa cuando cambia algo (la ventana principal ajusta el menu).</summary>
        public static event Action? Cambio;

        public static readonly OpcionColor[] Principales =
        {
            new OpcionColor("Verde azulado", "#00897B"),
            new OpcionColor("Esmeralda", "#2E7D32"),
            new OpcionColor("Azul", "#1E88E5"),
            new OpcionColor("Azul marino", "#283593"),
            new OpcionColor("Índigo", "#5E35B1"),
            new OpcionColor("Morado", "#8E24AA"),
            new OpcionColor("Fucsia", "#D81B60"),
            new OpcionColor("Rojo", "#E53935"),
            new OpcionColor("Naranja", "#F4511E"),
            new OpcionColor("Ámbar", "#FF8F00"),
            new OpcionColor("Café", "#6D4C41"),
            new OpcionColor("Gris azulado", "#455A64"),
            new OpcionColor("Negro", "#212121")
        };

        public static readonly OpcionColor[] Acentos =
        {
            new OpcionColor("Ámbar", "#FFB300"),
            new OpcionColor("Naranja", "#FF7043"),
            new OpcionColor("Rosa", "#EC407A"),
            new OpcionColor("Lima", "#C0CA33"),
            new OpcionColor("Cian", "#00ACC1"),
            new OpcionColor("Verde", "#43A047"),
            new OpcionColor("Morado", "#7E57C2"),
            new OpcionColor("Azul", "#42A5F5")
        };

        public static readonly OpcionColor[] FondosClaros =
        {
            new OpcionColor("Gris nube", "#F3F6F9"),
            new OpcionColor("Blanco", "#FAFAFA"),
            new OpcionColor("Crema", "#FFF8EC"),
            new OpcionColor("Menta", "#EAF7F1"),
            new OpcionColor("Azul hielo", "#EAF2FB"),
            new OpcionColor("Lavanda", "#F2EEFB"),
            new OpcionColor("Rosa pálido", "#FCEFF3"),
            new OpcionColor("Arena", "#F4EFE6")
        };

        public static readonly OpcionColor[] FondosOscuros =
        {
            new OpcionColor("Carbón", "#121212"),
            new OpcionColor("Grafito", "#1E2227"),
            new OpcionColor("Azul noche", "#0F172A"),
            new OpcionColor("Verde bosque", "#0C1F1A"),
            new OpcionColor("Morado noche", "#1A1328"),
            new OpcionColor("Café oscuro", "#1F1915")
        };

        public static readonly double[] Escalas = { 0.9, 1.0, 1.1, 1.25 };

        private static string Archivo
        {
            get
            {
                string carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SGAPE");
                return Path.Combine(carpeta, "apariencia.json");
            }
        }

        /// <summary>Lee las preferencias guardadas y las aplica (al abrir la aplicacion).</summary>
        public static void Cargar()
        {
            try
            {
                if (File.Exists(Archivo))
                    Actual = JsonSerializer.Deserialize<PreferenciasApariencia>(File.ReadAllText(Archivo)) ?? new PreferenciasApariencia();
            }
            catch (Exception)
            {
                Actual = new PreferenciasApariencia();   // archivo dañado: se usan los colores de siempre
            }
            Aplicar();
        }

        public static void Guardar()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Archivo)!);
                File.WriteAllText(Archivo, JsonSerializer.Serialize(Actual, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception)
            {
                // Sin permiso de escritura: el cambio funciona igual hasta cerrar la aplicacion
            }
        }

        /// <summary>Cambia una preferencia, la aplica y la guarda.</summary>
        public static void Cambiar(Action<PreferenciasApariencia> cambio)
        {
            cambio(Actual);
            Aplicar();
            Guardar();
        }

        public static void Restablecer()
        {
            bool menu = Actual.MenuAbierto;
            Actual = new PreferenciasApariencia { MenuAbierto = menu };
            Aplicar();
            Guardar();
        }

        public static void Aplicar()
        {
            PreferenciasApariencia p = Actual;
            PaletteHelper paleta = new PaletteHelper();
            Theme tema = paleta.GetTheme();
            tema.SetBaseTheme(p.Oscuro ? BaseTheme.Dark : BaseTheme.Light);
            tema.SetPrimaryColor(ColorDe(p.ColorPrincipal, Color.FromRgb(0x00, 0x89, 0x7B)));
            tema.SetSecondaryColor(ColorDe(p.ColorAcento, Color.FromRgb(0xFF, 0xB3, 0x00)));

            string fondo = p.Oscuro ? p.FondoOscuro : p.FondoClaro;
            if (EsHex(fondo))
            {
                Color c = ColorDe(fondo, Colors.White);
                tema.Background = c;
                // Las tarjetas resaltan sobre el fondo: blancas en claro, un poco mas claras que el fondo en oscuro
                tema.Cards.Background = p.Oscuro ? Mezclar(c, Colors.White, 0.07) : Colors.White;
            }
            paleta.SetTheme(tema);

            Application.Current.Resources["EscalaInterfaz"] = new ScaleTransform(p.Escala, p.Escala);
            Cambio?.Invoke();
        }

        // ---------------- colores ----------------

        public static bool EsHex(string? texto)
        {
            string t = (texto ?? string.Empty).Trim().TrimStart('#');
            if (t.Length != 6) return false;
            return int.TryParse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
        }

        public static string NormalizarHex(string texto)
        {
            return "#" + texto.Trim().TrimStart('#').ToUpperInvariant();
        }

        public static Color ColorDe(string? hex, Color porDefecto)
        {
            if (!EsHex(hex)) return porDefecto;
            int v = int.Parse(hex!.Trim().TrimStart('#'), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return Color.FromRgb((byte)(v >> 16), (byte)(v >> 8), (byte)v);
        }

        public static Brush PincelDe(string hex)
        {
            SolidColorBrush b = new SolidColorBrush(ColorDe(hex, Colors.Gray));
            b.Freeze();
            return b;
        }

        private static Color Mezclar(Color a, Color b, double t)
        {
            return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }
    }
}
