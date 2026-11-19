using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Presentacion.Comun;

namespace Presentacion.ViewModels
{
    /// <summary>Una opcion de tamaño de la interfaz (90 %, 100 %, 110 %, 125 %).</summary>
    public class OpcionEscala
    {
        public string Nombre { get; init; } = string.Empty;
        public double Valor { get; init; }
        public string Clave { get { return Valor.ToString("0.##", CultureInfo.InvariantCulture); } }
    }

    /// <summary>
    /// Seccion Apariencia: modo claro u oscuro, color de los botones y del menu, color de acento,
    /// fondo de las pantallas y tamaño de letra. Todo se aplica al instante y se guarda en este computador.
    /// </summary>
    public partial class AparienciaViewModel : ViewModelBase
    {
        public OpcionColor[] Principales { get { return Apariencia.Principales; } }
        public OpcionColor[] Acentos { get { return Apariencia.Acentos; } }
        public OpcionColor[] Fondos { get { return Oscuro ? Apariencia.FondosOscuros : Apariencia.FondosClaros; } }

        public OpcionEscala[] Escalas { get; } =
        {
            new OpcionEscala { Nombre = "Pequeña", Valor = 0.9 },
            new OpcionEscala { Nombre = "Normal", Valor = 1.0 },
            new OpcionEscala { Nombre = "Grande", Valor = 1.1 },
            new OpcionEscala { Nombre = "Muy grande", Valor = 1.25 }
        };

        /// <summary>Datos de ejemplo para la grafica de la vista previa.</summary>
        public System.Collections.Generic.List<PuntoGrafica> Muestra { get; } = new System.Collections.Generic.List<PuntoGrafica>
        {
            new PuntoGrafica { Etiqueta = "Lun", Valor = 320000, Texto = "$ 320.000" },
            new PuntoGrafica { Etiqueta = "Mar", Valor = 450000, Texto = "$ 450.000" },
            new PuntoGrafica { Etiqueta = "Mié", Valor = 280000, Texto = "$ 280.000" },
            new PuntoGrafica { Etiqueta = "Jue", Valor = 610000, Texto = "$ 610.000" },
            new PuntoGrafica { Etiqueta = "Vie", Valor = 720000, Texto = "$ 720.000" },
            new PuntoGrafica { Etiqueta = "Sáb", Valor = 530000, Texto = "$ 530.000" }
        };

        [ObservableProperty] private bool _oscuro;
        [ObservableProperty] private string _hexPrincipal = string.Empty;
        [ObservableProperty] private string _hexFondo = string.Empty;
        [ObservableProperty] private string? _errorPrincipal;
        [ObservableProperty] private string? _errorFondo;

        // Lo elegido ahora (las muestras marcan con un check la que coincide)
        public string PrincipalActual { get { return Apariencia.Actual.ColorPrincipal; } }
        public string AcentoActual { get { return Apariencia.Actual.ColorAcento; } }
        public string FondoActual { get { return Oscuro ? Apariencia.Actual.FondoOscuro : Apariencia.Actual.FondoClaro; } }
        public string EscalaActual { get { return Apariencia.Actual.Escala.ToString("0.##", CultureInfo.InvariantCulture); } }

        private bool _sincronizando;

        public AparienciaViewModel()
        {
            // Si se cambia el modo desde la barra superior, esta pantalla se actualiza
            Apariencia.Cambio += Sincronizar;
            Sincronizar();
        }

        public override Task Cargar()
        {
            Sincronizar();
            return Task.CompletedTask;
        }

        private void Sincronizar()
        {
            _sincronizando = true;
            Oscuro = Apariencia.Actual.Oscuro;
            _sincronizando = false;
            HexPrincipal = Apariencia.Actual.ColorPrincipal;
            HexFondo = FondoActual;
            OnPropertyChanged(nameof(Fondos));
            OnPropertyChanged(nameof(PrincipalActual));
            OnPropertyChanged(nameof(AcentoActual));
            OnPropertyChanged(nameof(FondoActual));
            OnPropertyChanged(nameof(EscalaActual));
        }

        partial void OnOscuroChanged(bool value)
        {
            if (_sincronizando) return;
            Apariencia.Cambiar(p => p.Oscuro = value);
        }

        [RelayCommand]
        private void ModoClaro() { Oscuro = false; }

        [RelayCommand]
        private void ModoOscuro() { Oscuro = true; }

        [RelayCommand]
        private void ElegirPrincipal(OpcionColor? o)
        {
            if (o == null) return;
            ErrorPrincipal = null;
            Apariencia.Cambiar(p => p.ColorPrincipal = o.Hex);
        }

        [RelayCommand]
        private void ElegirAcento(OpcionColor? o)
        {
            if (o == null) return;
            Apariencia.Cambiar(p => p.ColorAcento = o.Hex);
        }

        [RelayCommand]
        private void ElegirFondo(OpcionColor? o)
        {
            if (o == null) return;
            ErrorFondo = null;
            bool oscuro = Oscuro;
            Apariencia.Cambiar(p => { if (oscuro) p.FondoOscuro = o.Hex; else p.FondoClaro = o.Hex; });
        }

        [RelayCommand]
        private void AplicarPrincipal()
        {
            if (!Apariencia.EsHex(HexPrincipal)) { ErrorPrincipal = "Escribe un color como #1E88E5 (6 letras o números después del #)."; return; }
            ErrorPrincipal = null;
            string hex = Apariencia.NormalizarHex(HexPrincipal);
            Apariencia.Cambiar(p => p.ColorPrincipal = hex);
        }

        [RelayCommand]
        private void AplicarFondo()
        {
            if (!Apariencia.EsHex(HexFondo)) { ErrorFondo = "Escribe un color como #F3F6F9 (6 letras o números después del #)."; return; }
            ErrorFondo = null;
            string hex = Apariencia.NormalizarHex(HexFondo);
            bool oscuro = Oscuro;
            Apariencia.Cambiar(p => { if (oscuro) p.FondoOscuro = hex; else p.FondoClaro = hex; });
        }

        [RelayCommand]
        private void ElegirEscala(OpcionEscala? e)
        {
            if (e == null) return;
            Apariencia.Cambiar(p => p.Escala = e.Valor);
        }

        [RelayCommand]
        private void Restablecer()
        {
            ErrorPrincipal = null;
            ErrorFondo = null;
            Apariencia.Restablecer();
            Notificador.Exito("Se restablecieron los colores de SGAPE.");
        }
    }
}
