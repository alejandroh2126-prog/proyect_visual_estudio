using System;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Logica;

namespace BotTelegram
{
    /// <summary>
    /// Bot de Telegram de SGAPE. Pensado para correr 24/7 en un servidor (VM gratis de Oracle Cloud) como servicio de systemd.
    /// Variables de entorno: TELEGRAM_BOT_TOKEN, GROQ_API_KEY, GROQ_MODELO (opcional),
    /// SGAPE_DB_USUARIO, SGAPE_DB_CLAVE, SGAPE_DB_DATASOURCE, SGAPE_DB_WALLET.
    /// </summary>
    public static class Program
    {
        public static async Task<int> Main()
        {
            using CancellationTokenSource cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
            AppDomain.CurrentDomain.ProcessExit += (s, e) => cts.Cancel();   // systemctl stop

            ConfiguracionBot config;
            FabricaServicios servicios;
            try
            {
                config = ConfiguracionBot.Cargar();
                servicios = new FabricaServicios();   // lee SGAPE_DB_* y verifica los objetos de Oracle
                servicios.Mantenimiento();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("No se pudo iniciar el bot: " + ex.Message);
                return 1;
            }

            TelegramApi telegram = new TelegramApi(config.TokenTelegram);
            Herramientas herramientas = new Herramientas(servicios);
            Asistente asistente = new Asistente(new GroqCliente(config.ClaveGroq, config.ModeloGroq), herramientas);
            Manejador manejador = new Manejador(telegram, servicios, asistente, herramientas, config.LimitePreguntas);

            try { await telegram.RegistrarComandos(cts.Token); }
            catch (Exception ex) { Console.Error.WriteLine("Aviso: no se registraron los comandos: " + ex.Message); }

            Console.WriteLine(DateTime.Now.ToString("s") + " Bot de SGAPE iniciado con el modelo " + config.ModeloGroq + ".");
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
