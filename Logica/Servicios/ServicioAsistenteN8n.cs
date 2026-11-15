using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: enviar la pregunta al workflow de n8n (Gemini + base de conocimiento en Oracle)
    /// y devolver su respuesta. Es la misma "inteligencia" del bot de Telegram.
    /// Si no hay internet o n8n no responde, usa el asistente basico por palabras clave:
    /// el chat nunca se queda sin responder.
    /// </summary>
    public class ServicioAsistenteN8n
    {
        // Pega aqui la "Production URL" del nodo Webhook de n8n (el workflow debe estar publicado).
        private const string UrlWebhook = "https://wtfism.app.n8n.cloud/webhook/sgape-asistente";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private readonly ServicioAsistente _respaldo;
        private readonly string _sesion = Guid.NewGuid().ToString("N"); // n8n recuerda la conversacion por sesion

        /// <summary>Indica cual de los dos asistentes respondio la ultima pregunta.</summary>
        public FuenteAsistente UltimaFuente { get; private set; } = FuenteAsistente.Nube;

        public ServicioAsistenteN8n(ServicioAsistente respaldo)
        {
            _respaldo = respaldo;
        }

        public async Task<string> ResponderAsync(string mensaje)
        {
            string pregunta = mensaje.Trim();
            if (pregunta.Length == 0) return "Escribe tu pregunta y te ayudo.";

            try
            {
                string respuesta = await PreguntarAlWebhookAsync(pregunta);
                UltimaFuente = FuenteAsistente.Nube;
                return respuesta;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException ||
                                       ex is JsonException || ex is InvalidOperationException)
            {
                UltimaFuente = FuenteAsistente.Basico;
                return _respaldo.Responder(pregunta);
            }
        }

        private async Task<string> PreguntarAlWebhookAsync(string pregunta)
        {
            string cuerpo = JsonSerializer.Serialize(new { pregunta = pregunta, sesion = _sesion });

            using (StringContent contenido = new StringContent(cuerpo, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage respuesta = await _http.PostAsync(UrlWebhook, contenido))
            {
                respuesta.EnsureSuccessStatusCode();

                using (JsonDocument doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()))
                {
                    JsonElement raiz = doc.RootElement;
                    if (raiz.ValueKind == JsonValueKind.Array && raiz.GetArrayLength() > 0) raiz = raiz[0];

                    if (raiz.ValueKind == JsonValueKind.Object && raiz.TryGetProperty("output", out JsonElement salida))
                    {
                        string texto = (salida.GetString() ?? "").Trim();
                        if (texto.Length > 0) return texto;
                    }
                    throw new InvalidOperationException("n8n devolvio una respuesta vacia.");
                }
            }
        }
    }
}
