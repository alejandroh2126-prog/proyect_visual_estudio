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
    /// Si no hay internet o n8n no responde, pasa la pregunta a la IA local (Ollama) y, si tampoco
    /// esta disponible, al asistente basico por palabras clave. El chat nunca se queda sin responder.
    /// </summary>
    public class ServicioAsistenteN8n
    {
        // Pega aqui la "Production URL" del nodo Webhook de n8n (el workflow debe estar publicado).
        private const string UrlWebhook = "https://wtfism.app.n8n.cloud/webhook/sgape-asistente";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        private readonly ServicioAsistenteIA _respaldo;
        private string _sesion = Guid.NewGuid().ToString("N"); // n8n recuerda la conversacion por sesion

        /// <summary>Indica cual de los tres asistentes respondio la ultima pregunta.</summary>
        public FuenteAsistente UltimaFuente { get; private set; } = FuenteAsistente.Nube;

        public ServicioAsistenteN8n(ServicioAsistenteIA respaldo)
        {
            _respaldo = respaldo;
        }

        /// <summary>
        /// Responde la pregunta. alRecibirTrozo se ejecuta al llegar texto: una sola vez con la respuesta
        /// completa si responde n8n, o varias veces si responde Ollama. No se usa si responde el modo basico.
        /// </summary>
        public async Task<string> ResponderAsync(string mensaje, Action<string>? alRecibirTrozo = null)
        {
            string sesion = _sesion;
            string pregunta = mensaje.Trim();
            if (pregunta.Length == 0) return "Escribe tu pregunta y te ayudo.";

            try
            {
                string respuesta = await PreguntarAlWebhookAsync(pregunta);
                if (sesion != _sesion) return "Conversación finalizada.";
                UltimaFuente = FuenteAsistente.Nube;
                alRecibirTrozo?.Invoke(respuesta);
                return respuesta;
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException ||
                                       ex is JsonException || ex is InvalidOperationException)
            {
                if (sesion != _sesion) return "Conversación finalizada.";
                string respuesta = await _respaldo.ResponderAsync(pregunta, alRecibirTrozo);
                if (sesion != _sesion) return "Conversación finalizada.";
                UltimaFuente = _respaldo.UltimaRespuestaFueDeRespaldo ? FuenteAsistente.Basico : FuenteAsistente.Local;
                return respuesta;
            }
        }

        public void ReiniciarConversacion()
        {
            _sesion = Guid.NewGuid().ToString("N");
            _respaldo.ReiniciarConversacion();
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
