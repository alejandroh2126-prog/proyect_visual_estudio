using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Entidades;
using Logica.Utilidades;

namespace Logica.IA
{
    /// <summary>Datos del chat que pregunta. Salen de Oracle (pkg_sg_telegram), NUNCA de lo que diga el modelo.</summary>
    public class ContextoChat
    {
        public long ChatId { get; init; }
        public int UsuarioId { get; init; }
        public Emprendimiento Emprendimiento { get; init; } = new Emprendimiento();
    }

    /// <summary>Accion que modifica datos y espera que el usuario pulse "Confirmar".</summary>
    public class AccionPendiente
    {
        public long ChatId { get; init; }
        public int EmprendimientoId { get; init; }
        public bool EsIngreso { get; init; }
        public int CategoriaId { get; init; }
        public string Categoria { get; init; } = string.Empty;
        public decimal Monto { get; init; }
        public string Descripcion { get; init; } = string.Empty;
        public DateTime Creada { get; init; } = DateTime.UtcNow;
    }

    /// <summary>
    /// HERRAMIENTAS FIJAS que el modelo de Groq puede pedir (function calling).
    /// El modelo NO escribe SQL: solo elige una herramienta y sus argumentos; cada herramienta llama a un
    /// servicio de la capa Logica (que usa los paquetes, vistas y procedimientos de Oracle) y SIEMPRE con
    /// el emprendimiento del chat vinculado.
    /// </summary>
    public class Herramientas
    {
        private readonly FabricaServicios _s;
        public ConcurrentDictionary<string, AccionPendiente> Pendientes { get; } = new ConcurrentDictionary<string, AccionPendiente>();

        /// <summary>Si una herramienta pide enviar el PDF, el bot lo envia despues de responder.</summary>
        public bool PidioReporte { get; private set; }
        public string? IdConfirmacion { get; private set; }

        public Herramientas(FabricaServicios servicios) { _s = servicios; }

        public void Reiniciar() { PidioReporte = false; IdConfirmacion = null; }

        public static JsonArray Definiciones()
        {
            return new JsonArray
            {
                Funcion("resumen_emprendimiento", "Resumen del emprendimiento activo: trabajadores, productos, stock bajo, ventas del mes, ingresos, gastos, balance, nómina y hojas de vida nuevas.", null),
                Funcion("ventas", "Total vendido en un rango de fechas (formato aaaa-mm-dd). Sin fechas devuelve hoy, 7 días, mes y total histórico.",
                    Propiedades(("desde", "string", "Fecha inicial aaaa-mm-dd"), ("hasta", "string", "Fecha final aaaa-mm-dd"))),
                Funcion("productos_stock_bajo", "Productos con stock en o por debajo del mínimo.", null),
                Funcion("buscar_producto", "Precio, stock y margen de productos cuyo nombre contiene un texto.",
                    Propiedades(("texto", "string", "Parte del nombre del producto")), "texto"),
                Funcion("productos_mas_vendidos", "Ranking de productos más vendidos.",
                    Propiedades(("limite", "integer", "Cuántos productos (1 a 10)"))),
                Funcion("presupuestos", "Estado de los presupuestos de un mes: límite, gastado, porcentaje y estado OK/ALERTA/EXCEDIDO.",
                    Propiedades(("mes", "string", "Mes en formato aaaa-mm; si no se da, el mes actual"))),
                Funcion("gastos_por_categoria", "Gastos agrupados por categoría.",
                    Propiedades(("mes", "string", "Mes aaaa-mm; si no se da, todo el historial"))),
                Funcion("nomina", "Totales de nómina (mensual, quincenal, deducciones 8 %, neta) y la lista de trabajadores activos.", null),
                Funcion("vacantes", "Vacantes del emprendimiento con el número de hojas de vida recibidas.", null),
                Funcion("ayuda_sgape", "Preguntas frecuentes sobre cómo usar SGAPE (aplicación y bot).", null),
                Funcion("enviar_reporte_pdf", "Envía al chat el reporte completo del emprendimiento en PDF.", null),
                Funcion("proponer_movimiento", "Prepara un ingreso o gasto para registrar. NO lo guarda: el usuario debe confirmar con un botón.",
                    Propiedades(("tipo", "string", "ingreso o gasto"), ("categoria", "string", "Nombre de la categoría"),
                                ("monto", "number", "Monto en pesos colombianos, mayor que cero"), ("descripcion", "string", "Descripción corta")),
                    "tipo", "categoria", "monto")
            };
        }

        public string Ejecutar(string nombre, JsonObject? args, ContextoChat ctx)
        {
            int emp = ctx.Emprendimiento.Id;
            try
            {
                switch (nombre)
                {
                    case "resumen_emprendimiento":
                        return Json.Texto(_s.Reportes.Resumen(emp));

                    case "ventas":
                    {
                        string? desde = Texto(args, "desde"), hasta = Texto(args, "hasta");
                        if (desde == null && hasta == null) return Json.Texto(_s.Ventas.Resumen(emp));
                        DateTime d = Fecha(desde) ?? DateTime.Today, h = Fecha(hasta) ?? DateTime.Today;
                        if (h < d) (d, h) = (h, d);
                        decimal total = Indicadores.ReporteVentas(_s, emp, d, h);
                        return Json.Texto(new { desde = d.ToString("yyyy-MM-dd"), hasta = h.ToString("yyyy-MM-dd"), total });
                    }

                    case "productos_stock_bajo":
                        return Json.Texto(_s.Inventario.StockBajo(emp));

                    case "buscar_producto":
                    {
                        string texto = Texto(args, "texto") ?? string.Empty;
                        List<object> encontrados = new List<object>();
                        foreach (Producto p in _s.Inventario.Listar(emp))
                            if (Formato.Contiene(p.Nombre, texto) && encontrados.Count < 10)
                                encontrados.Add(new { p.Nombre, p.Categoria, p.PrecioVenta, p.PrecioCompra, p.Stock, p.StockMinimo, p.MargenPorcentaje });
                        return Json.Texto(encontrados);
                    }

                    case "productos_mas_vendidos":
                    {
                        int limite = Math.Clamp(Entero(args, "limite") ?? 5, 1, 10);
                        return Json.Texto(Indicadores.Top(_s, emp, limite));
                    }

                    case "presupuestos":
                    {
                        string mes = Texto(args, "mes") ?? Validar.MesActual();
                        return Json.Texto(_s.Presupuestos.Listar(emp, mes));
                    }

                    case "gastos_por_categoria":
                        return Json.Texto(Indicadores.Gastos(_s, emp, Texto(args, "mes")));

                    case "nomina":
                        return Json.Texto(new { resumen = _s.Nomina.Resumen(emp), trabajadores = _s.Nomina.Detalle(emp) });

                    case "vacantes":
                    {
                        List<object> lista = new List<object>();
                        foreach (Vacante v in _s.Vacantes.Listar(emp))
                            lista.Add(new { v.Id, v.Titulo, v.Cargo, v.Ciudad, v.Estado, hojas_de_vida = v.Postulaciones });
                        return Json.Texto(lista);
                    }

                    case "ayuda_sgape":
                        return Json.Texto(_s.Catalogos.PreguntasFrecuentes());

                    case "enviar_reporte_pdf":
                        PidioReporte = true;
                        return "{\"ok\":true,\"mensaje\":\"El PDF se enviará al chat.\"}";

                    case "proponer_movimiento":
                        return Proponer(args, ctx);

                    default:
                        return "{\"error\":\"Herramienta desconocida.\"}";
                }
            }
            catch (ReglaDeNegocioException ex)
            {
                return Json.Texto(new { error = ex.Message });
            }
        }

        private string Proponer(JsonObject? args, ContextoChat ctx)
        {
            string tipo = Formato.Normalizar(Texto(args, "tipo"));
            bool ingreso = tipo.StartsWith("ingreso");
            if (!ingreso && !tipo.StartsWith("gasto"))
                return Json.Texto(new { error = "El tipo debe ser ingreso o gasto." });

            decimal monto = Numero(args, "monto") ?? 0m;
            if (monto <= 0m || monto > 9_999_999_999m)
                return Json.Texto(new { error = "El monto debe ser mayor que cero." });

            string pedida = Formato.Normalizar(Texto(args, "categoria"));
            List<CategoriaMovimiento> categorias = _s.Catalogos.CategoriasManuales(ingreso ? CategoriaMovimiento.TipoIngreso : CategoriaMovimiento.TipoGasto);
            CategoriaMovimiento? cat = categorias.Find(c => Formato.Normalizar(c.Nombre) == pedida)
                                       ?? categorias.Find(c => pedida.Length > 2 && Formato.Normalizar(c.Nombre).Contains(pedida));
            if (cat == null)
            {
                List<string> nombres = new List<string>();
                foreach (CategoriaMovimiento c in categorias) nombres.Add(c.Nombre);
                return Json.Texto(new { error = "Categoría no válida.", categorias_validas = nombres });
            }

            string id = Guid.NewGuid().ToString("N").Substring(0, 12);
            Pendientes[id] = new AccionPendiente
            {
                ChatId = ctx.ChatId,
                EmprendimientoId = ctx.Emprendimiento.Id,
                EsIngreso = ingreso,
                CategoriaId = cat.Id,
                Categoria = cat.Nombre,
                Monto = Math.Round(monto, 0),
                Descripcion = (Texto(args, "descripcion") ?? string.Empty).Trim()
            };
            IdConfirmacion = id;
            return Json.Texto(new { pendiente_de_confirmacion = true, tipo = ingreso ? "ingreso" : "gasto", categoria = cat.Nombre, monto = Math.Round(monto, 0) });
        }

        // ---------- utilidades ----------

        private static JsonObject Funcion(string nombre, string descripcion, JsonObject? propiedades, params string[] requeridos)
        {
            JsonArray req = new JsonArray();
            foreach (string r in requeridos) req.Add(r);
            return new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = nombre,
                    ["description"] = descripcion,
                    ["parameters"] = new JsonObject
                    {
                        ["type"] = "object",
                        ["properties"] = propiedades ?? new JsonObject(),
                        ["required"] = req
                    }
                }
            };
        }

        private static JsonObject Propiedades(params (string nombre, string tipo, string descripcion)[] props)
        {
            JsonObject o = new JsonObject();
            foreach ((string nombre, string tipo, string descripcion) in props)
                o[nombre] = new JsonObject { ["type"] = tipo, ["description"] = descripcion };
            return o;
        }

        private static string? Texto(JsonObject? a, string clave)
        {
            if (a == null || !a.TryGetPropertyValue(clave, out JsonNode? n) || n == null) return null;
            string s = n.ToString().Trim();
            return s.Length == 0 ? null : s;
        }

        private static int? Entero(JsonObject? a, string clave)
        {
            string? s = Texto(a, clave);
            return s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : (int?)null;
        }

        private static decimal? Numero(JsonObject? a, string clave)
        {
            string? s = Texto(a, clave);
            if (s == null) return null;
            s = s.Replace("$", "").Replace(" ", "");
            return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal d) ? d : (decimal?)null;
        }

        private static DateTime? Fecha(string? s)
        {
            return s != null && DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime f) ? f : (DateTime?)null;
        }
    }

    /// <summary>Atajos a los indicadores de Oracle que no tienen un servicio propio.</summary>
    internal static class Indicadores
    {
        public static decimal ReporteVentas(FabricaServicios s, int emp, DateTime desde, DateTime hasta)
        {
            return s.Reportes.VentasEntre(emp, desde, hasta);
        }

        public static List<ValorEtiquetado> Top(FabricaServicios s, int emp, int limite) { return s.Reportes.TopProductos(emp, limite); }
        public static List<ValorEtiquetado> Gastos(FabricaServicios s, int emp, string? mes) { return s.Reportes.GastosPorCategoria(emp, mes); }
    }

    /// <summary>Conversacion con Groq: mensaje del usuario -> herramientas -> respuesta en texto.</summary>
    public class Asistente
    {
        private const string Instrucciones =
            "Eres el asistente de SGAPE, un sistema para administrar emprendimientos en Colombia. " +
            "Respondes en español, claro y breve (máximo 8 líneas), en texto plano sin Markdown. " +
            "Para cualquier dato del negocio (ventas, stock, nómina, presupuestos, finanzas, vacantes) USA SIEMPRE una herramienta; " +
            "nunca inventes cifras. Los montos están en pesos colombianos: escríbelos como $1.250.000. " +
            "Si una herramienta devuelve error, explícalo con palabras sencillas. " +
            "Para registrar un ingreso o gasto usa proponer_movimiento y di que debe confirmar con el botón; nunca digas que ya quedó guardado. " +
            "No puedes borrar ni modificar otros datos: para eso indica la sección de la aplicación. " +
            "Si preguntan algo que no es del negocio ni de SGAPE, responde en una línea que solo ayudas con SGAPE y con la administración del emprendimiento.";

        private readonly GroqCliente _groq;
        private readonly Herramientas _herramientas;
        private readonly JsonArray _definiciones = Herramientas.Definiciones();
        private readonly ConcurrentDictionary<long, List<JsonObject>> _historial = new ConcurrentDictionary<long, List<JsonObject>>();

        public Asistente(GroqCliente groq, Herramientas herramientas)
        {
            _groq = groq;
            _herramientas = herramientas;
        }

        public void Olvidar(long chatId) { _historial.TryRemove(chatId, out _); }

        public async Task<string> Responder(ContextoChat ctx, string pregunta, CancellationToken ct)
        {
            List<JsonObject> historial = _historial.GetOrAdd(ctx.ChatId, _ => new List<JsonObject>());

            JsonArray mensajes = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "system",
                    ["content"] = Instrucciones + " Hoy es " + DateTime.Now.ToString("yyyy-MM-dd") + ". Emprendimiento activo: " +
                                  ctx.Emprendimiento.Nombre + " (" + ctx.Emprendimiento.Sector + ", " + ctx.Emprendimiento.Ciudad + ")."
                }
            };
            foreach (JsonObject m in historial) mensajes.Add(m.DeepClone());
            JsonObject delUsuario = new JsonObject { ["role"] = "user", ["content"] = Recortar(pregunta, 1000) };
            mensajes.Add(delUsuario.DeepClone());

            string respuesta = "No pude responder. Intenta de nuevo.";
            for (int vuelta = 0; vuelta < 4; vuelta++)
            {
                JsonObject msg = await _groq.Completar(mensajes, _definiciones, ct);
                JsonArray? llamadas = msg["tool_calls"] as JsonArray;
                if (llamadas == null || llamadas.Count == 0)
                {
                    respuesta = msg["content"]?.ToString() ?? respuesta;
                    break;
                }

                mensajes.Add(msg.DeepClone());
                foreach (JsonNode? llamada in llamadas)
                {
                    string id = llamada?["id"]?.ToString() ?? string.Empty;
                    string nombre = llamada?["function"]?["name"]?.ToString() ?? string.Empty;
                    JsonObject? args = null;
                    try { args = JsonNode.Parse(llamada?["function"]?["arguments"]?.ToString() ?? "{}") as JsonObject; }
                    catch (System.Text.Json.JsonException) { }

                    string resultado = _herramientas.Ejecutar(nombre, args, ctx);
                    mensajes.Add(new JsonObject { ["role"] = "tool", ["tool_call_id"] = id, ["content"] = Recortar(resultado, 6000) });
                }
            }

            // Historial corto (ultimos 6 turnos) para que entienda "¿y el mes pasado?"
            historial.Add(delUsuario);
            historial.Add(new JsonObject { ["role"] = "assistant", ["content"] = respuesta });
            while (historial.Count > 12) historial.RemoveAt(0);
            return respuesta.Trim();
        }

        private static string Recortar(string s, int n) { return s.Length <= n ? s : s.Substring(0, n); }
    }

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

        /// <summary>Comprueba la clave y que el modelo exista (no gasta tokens).</summary>
        public async Task Probar(CancellationToken ct)
        {
            using HttpResponseMessage r = await _http.GetAsync("models", ct);
            int codigo = (int)r.StatusCode;
            if (codigo == 401 || codigo == 403)
                throw new ReglaDeNegocioException("Groq rechazó la clave. Cópiala completa desde console.groq.com → API Keys.");
            if (!r.IsSuccessStatusCode)
                throw new ReglaDeNegocioException("Groq respondió " + codigo + ". Inténtalo de nuevo en un momento.");

            // La lista trae los modelos que esta cuenta SI puede usar
            JsonArray? modelos = JsonNode.Parse(await r.Content.ReadAsStringAsync(ct))?["data"] as JsonArray;
            if (modelos != null)
            {
                foreach (JsonNode? m in modelos)
                    if (string.Equals(m?["id"]?.ToString(), _modelo, StringComparison.OrdinalIgnoreCase)) return;
                throw new ReglaDeNegocioException("La clave funciona, pero el modelo " + _modelo + " no está disponible para tu cuenta de Groq.");
            }
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

    /// <summary>
    /// Clave y modelo de Groq, para la aplicacion y para el bot. Se busca en este orden:
    /// 1) Variables de entorno GROQ_API_KEY y GROQ_MODELO (servidor del bot o pruebas).
    /// 2) La tabla sg_configuracion de Oracle: se guarda UNA vez desde la aplicacion y sirve en
    ///    cualquier computador que abra SGAPE. La clave NUNCA va en el codigo ni en Git.
    /// </summary>
    public class ConfiguracionGroq
    {
        public const string ModeloPredeterminado = "openai/gpt-oss-120b";

        public string Clave { get; set; } = string.Empty;
        public string Modelo { get; set; } = ModeloPredeterminado;

        public bool Lista { get { return Clave.Length > 0; } }

        /// <summary>True si la clave vino de GROQ_API_KEY (y no de Oracle).</summary>
        public bool DeVariableDeEntorno { get; private set; }

        /// <summary>
        /// Si la clave vino de la variable de entorno y Oracle no la tiene (o tiene otra), la copia a Oracle.
        /// Asi basta con arrancar el bot una vez para que la aplicacion funcione en cualquier computador.
        /// Devuelve true si la guardo. Si falla (sin permiso, sin tabla), no detiene nada.
        /// </summary>
        public static bool CompartirSiFalta(FabricaServicios servicios, ConfiguracionGroq cfg)
        {
            if (!cfg.Lista || !cfg.DeVariableDeEntorno) return false;
            try
            {
                string? enOracle = servicios.Configuracion.Leer(Logica.Servicios.ServicioConfiguracion.ClaveGroq);
                if (enOracle == cfg.Clave) return false;
                servicios.Configuracion.Guardar(Logica.Servicios.ServicioConfiguracion.ClaveGroq, cfg.Clave);
                servicios.Configuracion.Guardar(Logica.Servicios.ServicioConfiguracion.ModeloGroq, cfg.Modelo);
                return true;
            }
            catch (Exception) { return false; }
        }

        /// <summary>Hace consultas a Oracle: llamarlo fuera del hilo de la ventana.</summary>
        public static ConfiguracionGroq Cargar(FabricaServicios? servicios)
        {
            ConfiguracionGroq c = new ConfiguracionGroq();
            string? clave = Environment.GetEnvironmentVariable("GROQ_API_KEY");
            bool deEntorno = !string.IsNullOrWhiteSpace(clave);
            string? modelo = Environment.GetEnvironmentVariable("GROQ_MODELO");

            if (string.IsNullOrWhiteSpace(clave) && servicios != null)
            {
                clave = servicios.Configuracion.Leer(Logica.Servicios.ServicioConfiguracion.ClaveGroq);
                if (string.IsNullOrWhiteSpace(modelo))
                    modelo = servicios.Configuracion.Leer(Logica.Servicios.ServicioConfiguracion.ModeloGroq);
            }

            c.DeVariableDeEntorno = deEntorno;
            if (!string.IsNullOrWhiteSpace(clave)) c.Clave = clave.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(modelo)) c.Modelo = modelo.Trim();
            return c;
        }

        /// <summary>Prueba la clave con Groq y, si funciona, la guarda en Oracle para todos los computadores.</summary>
        public static async Task Guardar(FabricaServicios servicios, string clave, CancellationToken ct)
        {
            clave = (clave ?? string.Empty).Trim().Trim('"');
            if (clave.Length == 0) throw new ReglaDeNegocioException("Pega la clave de Groq.");

            string modelo = servicios.Configuracion.Leer(Logica.Servicios.ServicioConfiguracion.ModeloGroq) ?? ModeloPredeterminado;
            await new GroqCliente(clave, modelo).Probar(ct);

            servicios.Configuracion.Guardar(Logica.Servicios.ServicioConfiguracion.ClaveGroq, clave);
            servicios.Configuracion.Guardar(Logica.Servicios.ServicioConfiguracion.ModeloGroq, modelo);
        }
    }
}
