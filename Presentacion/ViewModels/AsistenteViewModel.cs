using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Entidades;
using Logica.IA;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.ViewModels
{
    /// <summary>Un globo del chat.</summary>
    public class MensajeChat
    {
        public string Texto { get; }
        public bool EsUsuario { get; }
        public string Hora { get; }

        public MensajeChat(string texto, bool esUsuario)
        {
            Texto = texto;
            EsUsuario = esUsuario;
            Hora = DateTime.Now.ToString("HH:mm");
        }
    }

    /// <summary>
    /// Asistente dentro de la aplicacion: el MISMO motor del bot de Telegram (Logica.IA).
    /// Groq solo elige entre 12 herramientas fijas; los datos salen de Oracle por los servicios de siempre,
    /// siempre del emprendimiento elegido arriba. Registrar un ingreso o gasto exige pulsar "Confirmar".
    /// </summary>
    public partial class AsistenteViewModel : ViewModelBase
    {
        public ObservableCollection<MensajeChat> Mensajes { get; } = new ObservableCollection<MensajeChat>();

        [ObservableProperty] private string? _pregunta;
        [ObservableProperty] private bool _pensando;
        [ObservableProperty] private bool _sinClave;
        [ObservableProperty] private string? _claveNueva;
        [ObservableProperty] private string? _textoPendiente;

        private Herramientas _herramientas = new Herramientas(App.Servicios);
        private Asistente? _asistente;
        private string? _idPendiente;
        private int? _empresaCargada;

        public bool HayPendiente { get { return !string.IsNullOrEmpty(TextoPendiente); } }

        partial void OnTextoPendienteChanged(string? value) { OnPropertyChanged(nameof(HayPendiente)); }

        public override async Task Cargar()
        {
            if (_asistente == null)
            {
                // La clave sale de Oracle (sg_configuracion): la misma para todos los computadores
                ConfiguracionGroq cfg = await Fondo.Run(() => ConfiguracionGroq.Cargar(App.Servicios));
                SinClave = !cfg.Lista;
                if (cfg.Lista) _asistente = new Asistente(new GroqCliente(cfg.Clave, cfg.Modelo), _herramientas);
            }

            // Otro emprendimiento = otra conversacion (el asistente nunca mezcla datos de dos negocios)
            int? emp = Sesion.Emprendimiento?.Id;
            if (emp != _empresaCargada)
            {
                _empresaCargada = emp;
                Mensajes.Clear();
                _asistente?.Olvidar(Sesion.IdUsuario);
                LimpiarPendiente();
                Mensajes.Add(new MensajeChat(
                    "Hola, soy el asistente de " + (Sesion.Emprendimiento?.Nombre ?? "tu emprendimiento") + ". " +
                    "Pregúntame por ventas, stock bajo, presupuestos, nómina, vacantes o cómo usar SGAPE. " +
                    "También puedo preparar un ingreso o gasto para que lo confirmes.", false));
            }
        }

        [RelayCommand]
        private async Task GuardarClave()
        {
            string clave = ClaveNueva ?? string.Empty;
            await Ejecutar(async () =>
            {
                // Primero se prueba con Groq; solo si funciona se guarda en Oracle para todos
                await Task.Run(() => ConfiguracionGroq.Guardar(App.Servicios, clave, CancellationToken.None));
                ClaveNueva = null;
                _asistente = null;
                await Cargar();
                Notificador.Exito("Clave guardada en la base de datos. Ya funciona en todos los computadores.");
            });
        }

        [RelayCommand]
        private async Task Sugerencia(string? texto)
        {
            Pregunta = texto;
            await Enviar();
        }

        [RelayCommand]
        private async Task Enviar()
        {
            string texto = (Pregunta ?? string.Empty).Trim();
            if (texto.Length == 0 || Pensando) return;
            if (_asistente == null) { Notificador.Error("Primero guarda la clave de Groq."); return; }
            if (!Sesion.HayEmprendimiento) { Notificador.Error("Primero crea o elige un emprendimiento."); return; }

            Pregunta = string.Empty;
            Mensajes.Add(new MensajeChat(texto, true));
            LimpiarPendiente();
            Pensando = true;
            try
            {
                ContextoChat ctx = new ContextoChat
                {
                    ChatId = Sesion.IdUsuario,
                    UsuarioId = Sesion.IdUsuario,
                    Emprendimiento = Sesion.Emprendimiento!
                };
                Asistente asistente = _asistente;
                _herramientas.Reiniciar();
                // Task.Run: las consultas a Oracle de las herramientas no deben congelar la ventana
                string respuesta = await Task.Run(() => asistente.Responder(ctx, texto, CancellationToken.None));
                if (_herramientas.PidioReporte)
                    respuesta += "\n\nEl PDF lo generas en la sección Reportes.";
                Mensajes.Add(new MensajeChat(respuesta, false));

                string? id = _herramientas.IdConfirmacion;
                if (id != null && _herramientas.Pendientes.TryGetValue(id, out AccionPendiente? p))
                {
                    _idPendiente = id;
                    TextoPendiente = (p.EsIngreso ? "Ingreso" : "Gasto") + ": " + p.Categoria + " por " + Formato.Moneda(p.Monto) +
                                     (p.Descripcion.Length > 0 ? " (" + p.Descripcion + ")" : string.Empty) + ". ¿Lo registro?";
                }
            }
            catch (ReglaDeNegocioException ex) { Mensajes.Add(new MensajeChat(ex.Message, false)); }
            catch (LimiteGroqException)
            {
                Mensajes.Add(new MensajeChat("El asistente está muy ocupado en este momento. Inténtalo en un minuto.", false));
            }
            catch (Exception ex)
            {
                string detalle = ex.Message.Length > 300 ? ex.Message.Substring(0, 300) : ex.Message;
                Mensajes.Add(new MensajeChat("No pude responder. Detalle técnico: " + detalle, false));
            }
            finally
            {
                Pensando = false;
            }
        }

        [RelayCommand]
        private async Task Confirmar()
        {
            if (_idPendiente == null) return;
            string id = _idPendiente;
            LimpiarPendiente();
            if (!_herramientas.Pendientes.TryRemove(id, out AccionPendiente? p) || p.ChatId != Sesion.IdUsuario)
            {
                Mensajes.Add(new MensajeChat("Esa acción ya no está disponible.", false));
                return;
            }
            if (DateTime.UtcNow - p.Creada > TimeSpan.FromMinutes(10) || p.EmprendimientoId != Sesion.Emprendimiento?.Id)
            {
                Mensajes.Add(new MensajeChat("La confirmación venció o cambiaste de emprendimiento. Vuelve a pedirlo.", false));
                return;
            }
            try
            {
                await Fondo.Run(() => App.Servicios.Finanzas.Registrar(p.EmprendimientoId, p.EsIngreso, p.CategoriaId, p.Descripcion, p.Monto, DateTime.Today));
                Mensajes.Add(new MensajeChat("✅ " + (p.EsIngreso ? "Ingreso" : "Gasto") + " registrado: " + p.Categoria + " " + Formato.Moneda(p.Monto) + ".", false));
            }
            catch (ReglaDeNegocioException ex) { Mensajes.Add(new MensajeChat(ex.Message, false)); }
            catch (Exception ex) { Mensajes.Add(new MensajeChat("No se pudo registrar: " + ex.Message, false)); }
        }

        [RelayCommand]
        private void Cancelar()
        {
            if (_idPendiente != null) _herramientas.Pendientes.TryRemove(_idPendiente, out _);
            LimpiarPendiente();
            Mensajes.Add(new MensajeChat("Cancelado. No se registró nada.", false));
        }

        [RelayCommand]
        private void NuevaConversacion()
        {
            _empresaCargada = null;
            _ = Cargar();
        }

        private void LimpiarPendiente()
        {
            _idPendiente = null;
            TextoPendiente = null;
        }
    }
}
