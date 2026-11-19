using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Logica;
using Logica.IA;

namespace BotTelegram
{
    /// <summary>
    /// Bot de Telegram de SGAPE. Pensado para correr 24/7 en un servidor (VM gratis de Oracle Cloud) como servicio de systemd.
    /// Variables de entorno: TELEGRAM_BOT_TOKEN, GROQ_API_KEY, GROQ_MODELO (opcional), SGAPE_ALERTAS_MINUTOS (15), SGAPE_RESUMEN_HORA (19),
    /// SGAPE_DB_USUARIO, SGAPE_DB_CLAVE, SGAPE_DB_DATASOURCE, SGAPE_DB_WALLET.
    /// </summary>
    public static class Program
    {
        /// <summary>Guarda el motivo del fallo junto al ejecutable (sgape-bot-error.log). Nunca incluye claves ni tokens.</summary>
        private static void GuardarError(string texto)
        {
            try { System.IO.File.AppendAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "sgape-bot-error.log"), DateTime.Now.ToString("s") + " " + texto + Environment.NewLine); }
            catch (Exception) { }
        }

        public static async Task<int> Main()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            // Cancelar() ignora el caso en que el programa ya termino y cts ya se liberó (antes lanzaba ObjectDisposedException al salir)
            void Cancelar() { try { cts.Cancel(); } catch (ObjectDisposedException) { } }
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; Cancelar(); };
            AppDomain.CurrentDomain.ProcessExit += (s, e) => Cancelar();   // systemctl stop

            ConfiguracionBot config;
            FabricaServicios servicios;
            ConfiguracionGroq groq;
            try
            {
                config = ConfiguracionBot.Cargar();
                servicios = new FabricaServicios();   // lee SGAPE_DB_* y verifica los objetos de Oracle
                servicios.Mantenimiento();
                groq = ConfiguracionGroq.Cargar(servicios);   // GROQ_API_KEY o, si no esta, la guardada desde la aplicacion
                if (!groq.Lista)
                    throw new InvalidOperationException("Falta la clave de Groq: guárdala desde la aplicación (sección Asistente) o en la variable GROQ_API_KEY.");
                // La clave de las variables del bot queda en Oracle: la aplicacion la usa sin pedirla en cualquier computador
                if (ConfiguracionGroq.CompartirSiFalta(servicios, groq))
                    Console.WriteLine("Clave de Groq copiada a Oracle: el asistente de la aplicación ya la tiene.");
            }
            catch (Exception ex)
            {
                string motivo = "No se pudo iniciar el bot: " + ex.Message;
                Console.Error.WriteLine(motivo);
                GuardarError(motivo);
                // Con el depurador de Visual Studio la consola se cierra sola: se deja abierta para poder leer el motivo
                if (System.Diagnostics.Debugger.IsAttached)
                {
                    Console.Error.WriteLine("Presiona Enter para cerrar.");
                    try { Console.ReadLine(); } catch (Exception) { }
                }
                return 1;
            }

            TelegramApi telegram = new TelegramApi(config.TokenTelegram);
            Herramientas herramientas = new Herramientas(servicios);
            Asistente asistente = new Asistente(new GroqCliente(groq.Clave, groq.Modelo), herramientas);
            Manejador manejador = new Manejador(telegram, servicios, asistente, herramientas, config.LimitePreguntas);
            Vigilante vigilante = new Vigilante(telegram, servicios, config.MinutosAlertas, config.HoraResumen);

            try { await telegram.RegistrarComandos(cts.Token); }
            catch (Exception ex) { Console.Error.WriteLine("Aviso: no se registraron los comandos: " + ex.Message); }

            // El nombre del bot queda en Oracle: la aplicacion arma el enlace "Abrir en Telegram" (vincular con un toque)
            try
            {
                string usuarioBot = await telegram.UsuarioDelBot(cts.Token);
                if (usuarioBot.Length > 0 && servicios.Configuracion.Leer(Logica.Servicios.ServicioConfiguracion.UsuarioBotTelegram) != usuarioBot)
                    servicios.Configuracion.Guardar(Logica.Servicios.ServicioConfiguracion.UsuarioBotTelegram, usuarioBot);
            }
            catch (Exception ex) { Console.Error.WriteLine("Aviso: no se guardó el nombre del bot: " + ex.Message); }

            Console.WriteLine(DateTime.Now.ToString("s") + " Bot de SGAPE iniciado con el modelo " + groq.Modelo +
                              ". Alertas cada " + config.MinutosAlertas + " min; cierre del día a las " + config.HoraResumen + " h.");
            long siguiente = 0;
            DateTime ultimoMantenimiento = DateTime.UtcNow;

            while (!cts.IsCancellationRequested)
            {
                try
                {
                    // Long polling: Telegram espera hasta 50 s a que llegue un mensaje
                    JsonArray actualizaciones = await telegram.ObtenerActualizaciones(siguiente, cts.Token);
                    foreach (JsonNode? a in actualizaciones)
                    {
                        if (a == null) continue;
                        siguiente = (a["update_id"]?.GetValue<long>() ?? siguiente) + 1;
                        await manejador.Procesar(a, cts.Token);
                    }

                    // Avisos automaticos (stock bajo, presupuestos, hojas de vida, cierre del dia)
                    await vigilante.Revisar(cts.Token);

                    if (DateTime.UtcNow - ultimoMantenimiento > TimeSpan.FromHours(6))
                    {
                        servicios.Mantenimiento();   // sp_sg_limpiar_bitacora y sp_sg_cerrar_vacantes_vencidas
                        ultimoMantenimiento = DateTime.UtcNow;
                    }
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Sin internet, Telegram caido u otro error: se registra, se espera y se reintenta (el bot no se cae)
                    Console.Error.WriteLine(DateTime.Now.ToString("s") + " Reintentando: " + ex.Message);
                    try { await Task.Delay(TimeSpan.FromSeconds(5), cts.Token); } catch (OperationCanceledException) { break; }
                }
            }

            Console.WriteLine("Bot detenido.");
            return 0;
        }
    }
}
