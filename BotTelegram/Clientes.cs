using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace BotTelegram
{
    /// <summary>
    /// Configuracion del bot. TODO sale de variables de entorno: el token de Telegram y la clave de Groq
    /// nunca van en el codigo, ni en el repositorio, ni se pegan en un chat.
    /// </summary>
    public class ConfiguracionBot
    {
        public string TokenTelegram { get; private set; } = string.Empty;
        public string ClaveGroq { get; private set; } = string.Empty;
        public string ModeloGroq { get; private set; } = "llama-3.3-70b-versatile";

        /// <summary>Maximo de preguntas al modelo por chat en 10 minutos (cuida el cupo de la clave de Groq).</summary>
        public int LimitePreguntas { get; private set; } = 20;

        public static ConfiguracionBot Cargar()
        {
            ConfiguracionBot c = new ConfiguracionBot
            {
                TokenTelegram = Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? string.Empty,
                ClaveGroq = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? string.Empty
            };
            string? modelo = Environment.GetEnvironmentVariable("GROQ_MODELO");
            if (!string.IsNullOrWhiteSpace(modelo)) c.ModeloGroq = modelo.Trim();
            if (int.TryParse(Environment.GetEnvironmentVariable("SGAPE_LIMITE_PREGUNTAS"), out int limite) && limite > 0)
                c.LimitePreguntas = limite;

            if (string.IsNullOrWhiteSpace(c.TokenTelegram))
                throw new InvalidOperationException("Falta la variable de entorno TELEGRAM_BOT_TOKEN.");
            if (string.IsNullOrWhiteSpace(c.ClaveGroq))
                throw new InvalidOperationException("Falta la variable de entorno GROQ_API_KEY.");
            return c;
        }
    }

    // =====================================================================
    // TELEGRAM BOT API (HTTP directo, sin librerias: getUpdates, sendMessage, sendDocument, getFile)
    // =====================================================================

    public class Boton
    {
        public string Texto { get; }
        public string Dato { get; }
        public Boton(string texto, string dato) { Texto = texto; Dato = dato; }
    }

    public class TelegramApi
    {
        private readonly HttpClient _http;
        private readonly string _token;

        public TelegramApi(string token)
        {
            _token = token;
            _http = new HttpClient { BaseAddress = new Uri("https://api.telegram.org/bot" + token + "/"), Timeout = TimeSpan.FromSeconds(70) };
        }

        private async Task<JsonNode?> Llamar(string metodo, JsonObject cuerpo, CancellationToken ct)
        {
            using StringContent contenido = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage r = await _http.PostAsync(metodo, contenido, ct);
            string texto = await r.Content.ReadAsStringAsync(ct);
            JsonNode? json = JsonNode.Parse(texto);
            if (json?["ok"]?.GetValue<bool>() != true)
                throw new InvalidOperationException("Telegram " + metodo + ": " + (json?["description"]?.ToString() ?? texto));
            return json["result"];
        }

        public async Task<JsonArray> ObtenerActualizaciones(long desde, CancellationToken ct)
        {
            JsonObject cuerpo = new JsonObject
            {
                ["offset"] = desde,
                ["timeout"] = 50,
                ["allowed_updates"] = new JsonArray("message", "callback_query")
            };
            return (await Llamar("getUpdates", cuerpo, ct)) as JsonArray ?? new JsonArray();
        }

        public async Task EnviarMensaje(long chatId, string texto, List<List<Boton>>? botones = null, CancellationToken ct = default)
        {
            // Telegram admite hasta 4096 caracteres por mensaje
            foreach (string parte in Partir(texto, 4000))
            {
                JsonObject cuerpo = new JsonObject { ["chat_id"] = chatId, ["text"] = parte };
                if (botones != null) cuerpo["reply_markup"] = Teclado(botones);
                await Llamar("sendMessage", cuerpo, ct);
                botones = null;   // los botones solo van en la primera parte
            }
        }

        public async Task EnviarDocumento(long chatId, byte[] contenido, string nombreArchivo, string leyenda, CancellationToken ct = default)
        {
            using MultipartFormDataContent form = new MultipartFormDataContent();
            form.Add(new StringContent(chatId.ToString()), "chat_id");
            form.Add(new StringContent(leyenda), "caption");
            ByteArrayContent archivo = new ByteArrayContent(contenido);
            archivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(archivo, "document", nombreArchivo);
            using HttpResponseMessage r = await _http.PostAsync("sendDocument", form, ct);
            if (!r.IsSuccessStatusCode)
                throw new InvalidOperationException("Telegram sendDocument: " + await r.Content.ReadAsStringAsync(ct));
        }

        public async Task ResponderBoton(string callbackId, CancellationToken ct = default)
        {
            await Llamar("answerCallbackQuery", new JsonObject { ["callback_query_id"] = callbackId }, ct);
        }

        public async Task EscribiendoAccion(long chatId, CancellationToken ct = default)
        {
            try { await Llamar("sendChatAction", new JsonObject { ["chat_id"] = chatId, ["action"] = "typing" }, ct); }
            catch (Exception) { }
        }

        /// <summary>Descarga un archivo que envio el usuario (getFile + descarga). Limite de Telegram: 20 MB.</summary>
        public async Task<byte[]> DescargarArchivo(string fileId, CancellationToken ct = default)
        {
            JsonNode? info = await Llamar("getFile", new JsonObject { ["file_id"] = fileId }, ct);
            string ruta = info?["file_path"]?.ToString() ?? throw new InvalidOperationException("Telegram no devolvió la ruta del archivo.");
            return await _http.GetByteArrayAsync(new Uri("https://api.telegram.org/file/bot" + _token + "/" + ruta), ct);
        }

        public async Task RegistrarComandos(CancellationToken ct)
        {
            JsonArray comandos = new JsonArray();
            foreach ((string c, string d) in new[]
            {
                ("resumen", "Resumen del emprendimiento"), ("reporte", "Reporte en PDF"), ("stock", "Productos con stock bajo"),
                ("presupuestos", "Presupuestos del mes"), ("nomina", "Nómina"), ("emprendimientos", "Cambiar de emprendimiento"),
                ("vacantes", "Ver vacantes y enviar hoja de vida"), ("vincular", "Vincular tu cuenta de SGAPE"),
                ("ayuda", "Ayuda"), ("cancelar", "Cancelar lo que estás haciendo")
            })
                comandos.Add(new JsonObject { ["command"] = c, ["description"] = d });
            await Llamar("setMyCommands", new JsonObject { ["commands"] = comandos }, ct);
        }

        private static JsonObject Teclado(List<List<Boton>> filas)
        {
            JsonArray teclado = new JsonArray();
            foreach (List<Boton> fila in filas)
            {
                JsonArray f = new JsonArray();
                foreach (Boton b in fila) f.Add(new JsonObject { ["text"] = b.Texto, ["callback_data"] = b.Dato });
                teclado.Add(f);
            }
            return new JsonObject { ["inline_keyboard"] = teclado };
        }

        private static IEnumerable<string> Partir(string texto, int tamano)
        {
            if (string.IsNullOrEmpty(texto)) { yield return "(sin texto)"; yield break; }
            for (int i = 0; i < texto.Length; i += tamano)
                yield return texto.Substring(i, Math.Min(tamano, texto.Length - i));
        }
    }

    // =====================================================================
    // GROQ (API compatible con OpenAI: /chat/completions con herramientas)
    // =====================================================================

    public class GroqCliente
    {
        private readonly HttpClient _http;
        private readonly string _modelo;

        public GroqCliente(string clave, string modelo)
        {
            _modelo = modelo;
            _http = new HttpClient { BaseAddress = new Uri("https://api.groq.com/openai/v1/"), Timeout = TimeSpan.FromSeconds(60) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", clave);
        }

        /// <summary>Devuelve el mensaje del asistente (puede traer tool_calls).</summary>
        public async Task<JsonObject> Completar(JsonArray mensajes, JsonArray herramientas, CancellationToken ct)
        {
            JsonObject cuerpo = new JsonObject
            {
                ["model"] = _modelo,
                ["messages"] = mensajes.DeepClone(),
                ["tools"] = herramientas.DeepClone(),
                ["tool_choice"] = "auto",
                ["temperature"] = 0.2,
                ["max_tokens"] = 900
            };
            using StringContent contenido = new StringContent(cuerpo.ToJsonString(), Encoding.UTF8, "application/json");
            using HttpResponseMessage r = await _http.PostAsync("chat/completions", contenido, ct);
            string texto = await r.Content.ReadAsStringAsync(ct);
            if ((int)r.StatusCode == 429)
                throw new LimiteGroqException();
            if (!r.IsSuccessStatusCode)
                throw new InvalidOperationException("Groq respondió " + (int)r.StatusCode + ": " + Recortar(texto, 300));

            JsonNode? json = JsonNode.Parse(texto);
            return json?["choices"]?[0]?["message"] as JsonObject
                   ?? throw new InvalidOperationException("Groq no devolvió respuesta.");
        }

        private static string Recortar(string s, int n) { return s.Length <= n ? s : s.Substring(0, n); }
    }

    public class LimiteGroqException : Exception
    {
        public LimiteGroqException() : base("Se alcanzó el límite de uso de Groq.") { }
    }

    internal static class Json
    {
        public static readonly JsonSerializerOptions Opciones = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string Texto(object o) { return JsonSerializer.Serialize(o, Opciones); }
    }
}
