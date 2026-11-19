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

        /// <summary>Maximo de preguntas al modelo por chat en 10 minutos (cuida el cupo de la clave de Groq).</summary>
        public int LimitePreguntas { get; private set; } = 20;

        /// <summary>Cada cuantos minutos se revisan las alertas automaticas (minimo 5).</summary>
        public int MinutosAlertas { get; private set; } = 15;

        /// <summary>Hora (0-23, Colombia) del cierre del dia. -1 lo desactiva.</summary>
        public int HoraResumen { get; private set; } = 19;

        public static ConfiguracionBot Cargar()
        {
            ConfiguracionBot c = new ConfiguracionBot
            {
                TokenTelegram = (Environment.GetEnvironmentVariable("TELEGRAM_BOT_TOKEN") ?? string.Empty).Trim()
            };
            // La clave y el modelo de Groq ya no son obligatorios aqui: ConfiguracionGroq los busca en
            // GROQ_API_KEY / GROQ_MODELO y, si no estan, en Oracle (los guarda la aplicacion, seccion Asistente).
            if (int.TryParse(Environment.GetEnvironmentVariable("SGAPE_LIMITE_PREGUNTAS"), out int limite) && limite > 0)
                c.LimitePreguntas = limite;
            if (int.TryParse(Environment.GetEnvironmentVariable("SGAPE_ALERTAS_MINUTOS"), out int minutos) && minutos >= 5)
                c.MinutosAlertas = minutos;
            if (int.TryParse(Environment.GetEnvironmentVariable("SGAPE_RESUMEN_HORA"), out int hora) && hora >= -1 && hora <= 23)
                c.HoraResumen = hora;

            if (string.IsNullOrWhiteSpace(c.TokenTelegram))
                throw new InvalidOperationException("Falta la variable de entorno TELEGRAM_BOT_TOKEN.");
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

        /// <summary>
        /// Teclado fijo debajo de la caja de texto (reply keyboard). Cada boton envia su texto como mensaje.
        /// Asi el aspirante solo ve "Vincular" y "Vacantes", y el dueño vinculado ve su menu completo.
        /// </summary>
        public async Task EnviarMenu(long chatId, string texto, List<List<string>> filas, CancellationToken ct = default)
        {
            JsonArray teclado = new JsonArray();
            foreach (List<string> fila in filas)
            {
                JsonArray f = new JsonArray();
                foreach (string b in fila) f.Add(new JsonObject { ["text"] = b });
                teclado.Add(f);
            }
            JsonObject cuerpo = new JsonObject
            {
                ["chat_id"] = chatId,
                ["text"] = texto,
                ["reply_markup"] = new JsonObject { ["keyboard"] = teclado, ["resize_keyboard"] = true, ["is_persistent"] = true }
            };
            await Llamar("sendMessage", cuerpo, ct);
        }

        // Menu de comandos (boton "Menú" de Telegram). Quien NO esta vinculado solo ve estos:
        private static readonly (string, string)[] ComandosPublicos =
        {
            ("vincular", "Vincular mi cuenta de SGAPE"), ("vacantes", "Ver vacantes y enviar hoja de vida"),
            ("ayuda", "Ayuda"), ("cancelar", "Cancelar lo que estás haciendo")
        };

        // ...y un chat vinculado ve el menu completo (se registra SOLO para ese chat).
        private static readonly (string, string)[] ComandosVinculado =
        {
            ("menu", "Mostrar los botones"), ("resumen", "Resumen del emprendimiento"), ("ventas", "Ventas de hoy"),
            ("stock", "Productos con stock bajo"), ("reporte", "Reporte en PDF"), ("presupuestos", "Presupuestos del mes"),
            ("nomina", "Nómina"), ("postulaciones", "Hojas de vida por revisar"), ("preguntas", "Preguntas sugeridas"),
            ("alertas", "Activar o desactivar avisos"), ("emprendimientos", "Cambiar de emprendimiento"),
            ("desvincular", "Desvincular este chat"), ("ayuda", "Ayuda"), ("cancelar", "Cancelar lo que estás haciendo")
        };

        private static JsonArray Lista((string, string)[] comandos)
        {
            JsonArray lista = new JsonArray();
            foreach ((string c, string d) in comandos) lista.Add(new JsonObject { ["command"] = c, ["description"] = d });
            return lista;
        }

        /// <summary>Menu por defecto (para todos los chats): solo vincular y vacantes.</summary>
        public async Task RegistrarComandos(CancellationToken ct)
        {
            await Llamar("setMyCommands", new JsonObject { ["commands"] = Lista(ComandosPublicos) }, ct);
        }

        /// <summary>Menu de un chat: completo si esta vinculado; si no, se borra y vuelve al menu publico.</summary>
        public async Task ComandosDeChat(long chatId, bool vinculado, CancellationToken ct = default)
        {
            JsonObject alcance = new JsonObject { ["type"] = "chat", ["chat_id"] = chatId };
            if (vinculado)
                await Llamar("setMyCommands", new JsonObject { ["commands"] = Lista(ComandosVinculado), ["scope"] = alcance }, ct);
            else
                await Llamar("deleteMyCommands", new JsonObject { ["scope"] = alcance }, ct);
        }

        /// <summary>Nombre de usuario del bot (getMe), por ejemplo "sgape_bot".</summary>
        public async Task<string> UsuarioDelBot(CancellationToken ct)
        {
            JsonNode? yo = await Llamar("getMe", new JsonObject(), ct);
            return yo?["username"]?.ToString() ?? string.Empty;
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
}
