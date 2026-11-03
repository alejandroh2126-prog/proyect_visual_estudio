using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: conversar con un modelo de IA local (Ollama) para responder dudas de SGAPE.
    /// Funciona sin internet ni tokens: Ollama corre en el mismo computador (http://localhost:11434).
    /// Si Ollama no esta abierto o falla, usa el ServicioAsistente de siempre (respuestas por palabras clave),
    /// asi el chat nunca se queda sin responder.
    /// </summary>
    public class ServicioAsistenteIA
    {
        private const string UrlOllama = "http://localhost:11434";
        private const string Modelo = "llama3.2";   // cambia por el modelo que tengas: ollama list
        private const int MaxMensajesHistorial = 10; // el modelo "recuerda" las ultimas 10 intervenciones

        private static readonly HttpClient _http = new HttpClient
        {
            BaseAddress = new Uri(UrlOllama),
            Timeout = TimeSpan.FromMinutes(2)
        };

        private class MensajeChat
        {
            [JsonPropertyName("role")] public string Rol { get; set; }
            [JsonPropertyName("content")] public string Contenido { get; set; }
            public MensajeChat(string rol, string contenido) { Rol = rol; Contenido = contenido; }
        }

        private readonly ServicioAsistente _respaldo;
        private readonly List<MensajeChat> _historial = new List<MensajeChat>();
        private Process? _procesoOllama;   // solo existe si SGAPE tuvo que abrir Ollama
        private Task? _tareaInicio;

        /// <summary>True si la ultima respuesta vino del asistente basico porque Ollama no estaba disponible.</summary>
        public bool UltimaRespuestaFueDeRespaldo { get; private set; }

        public ServicioAsistenteIA(ServicioAsistente respaldo)
        {
            _respaldo = respaldo;
        }

        public void ReiniciarConversacion()
        {
            _historial.Clear();
        }

        /// <summary>
        /// Abre Ollama en segundo plano (sin ventana) si todavia no esta corriendo.
        /// Se llama una vez al iniciar SGAPE; no bloquea la pantalla.
        /// </summary>
        public void IniciarOllama()
        {
            _tareaInicio = IniciarOllamaAsync();
        }

        /// <summary>Cierra Ollama al salir de SGAPE, pero SOLO si fue SGAPE quien lo abrio.</summary>
        public void DetenerOllama()
        {
            try
            {
                if (_procesoOllama != null && !_procesoOllama.HasExited)
                    _procesoOllama.Kill(true);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
            {
                // Ya estaba cerrado: no hay nada que hacer.
            }
        }

        private async Task IniciarOllamaAsync()
        {
            try
            {
                if (await OllamaRespondeAsync()) return; // ya estaba abierto (por ejemplo, desde la bandeja de Windows)

                string? ruta = BuscarEjecutableOllama();
                if (ruta == null) return;                // Ollama no esta instalado: el chat usara el modo basico

                _procesoOllama = Process.Start(new ProcessStartInfo
                {
                    FileName = ruta,
                    Arguments = "serve",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

                for (int i = 0; i < 30; i++)             // espera hasta 15 segundos a que responda
                {
                    await Task.Delay(500);
                    if (await OllamaRespondeAsync()) return;
                }
            }
            catch (Exception)
            {
                // Iniciar Ollama es un extra: si falla, el chat sigue funcionando en modo basico.
            }
        }

        private static async Task<bool> OllamaRespondeAsync()
        {
            try
            {
                using (CancellationTokenSource limite = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                using (HttpResponseMessage r = await _http.GetAsync("/", limite.Token))
                {
                    return r.IsSuccessStatusCode;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                return false;
            }
        }

        private static string? BuscarEjecutableOllama()
        {
            string instalado = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs", "Ollama", "ollama.exe");
            if (File.Exists(instalado)) return instalado;

            string? path = Environment.GetEnvironmentVariable("PATH");
            if (path == null) return null;
            foreach (string carpeta in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string candidato = Path.Combine(carpeta.Trim(), "ollama.exe");
                if (File.Exists(candidato)) return candidato;
            }
            return null;
        }

        /// <summary>
        /// Responde la pregunta. Si se pasa alRecibirTrozo, la respuesta llega por pedazos mientras se genera
        /// (se ejecuta en el hilo de la pantalla porque aqui no se usa ConfigureAwait(false)).
        /// </summary>
        public async Task<string> ResponderAsync(string mensaje, Action<string>? alRecibirTrozo = null)
        {
            string pregunta = mensaje.Trim();
            if (pregunta.Length == 0) return "Escribe tu pregunta y te ayudo.";

            // Si Ollama se esta abriendo en este momento, se espera a que termine de arrancar.
            if (_tareaInicio != null) await _tareaInicio;

            StringBuilder total = new StringBuilder();
            try
            {
                string cuerpo = ConstruirCuerpo(pregunta);
                using (HttpRequestMessage peticion = new HttpRequestMessage(HttpMethod.Post, "/api/chat"))
                {
                    peticion.Content = new StringContent(cuerpo, Encoding.UTF8, "application/json");
                    using (HttpResponseMessage respuesta = await _http.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead))
                    {
                        respuesta.EnsureSuccessStatusCode();
                        using (Stream flujo = await respuesta.Content.ReadAsStreamAsync())
                        using (StreamReader lector = new StreamReader(flujo))
                        {
                            string? linea;
                            while ((linea = await lector.ReadLineAsync()) != null)
                            {
                                if (linea.Length == 0) continue;
                                using (JsonDocument doc = JsonDocument.Parse(linea))
                                {
                                    if (doc.RootElement.TryGetProperty("message", out JsonElement msg) &&
                                        msg.TryGetProperty("content", out JsonElement contenido))
                                    {
                                        string trozo = contenido.GetString() ?? "";
                                        if (trozo.Length == 0) continue;
                                        total.Append(trozo);
                                        alRecibirTrozo?.Invoke(trozo);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException ||
                                       ex is IOException || ex is JsonException)
            {
                // Si ya llego algo de texto, se entrega lo que hay; si no, se usa el asistente basico.
                if (total.Length == 0)
                {
                    UltimaRespuestaFueDeRespaldo = true;
                    return _respaldo.Responder(pregunta);
                }
            }

            string texto = total.ToString().Trim();
            if (texto.Length == 0)
            {
                UltimaRespuestaFueDeRespaldo = true;
                return _respaldo.Responder(pregunta);
            }

            UltimaRespuestaFueDeRespaldo = false;
            _historial.Add(new MensajeChat("user", pregunta));
            _historial.Add(new MensajeChat("assistant", texto));
            while (_historial.Count > MaxMensajesHistorial) _historial.RemoveAt(0);
            return texto;
        }

        private string ConstruirCuerpo(string pregunta)
        {
            List<MensajeChat> mensajes = new List<MensajeChat>();
            mensajes.Add(new MensajeChat("system", ManualSgape.Instrucciones));
            mensajes.AddRange(_historial);
            mensajes.Add(new MensajeChat("user", pregunta));

            return JsonSerializer.Serialize(new
            {
                model = Modelo,
                stream = true,
                keep_alive = "10m",
                options = new { num_ctx = 4096, temperature = 0.2 },
                messages = mensajes
            });
        }
    }
}
