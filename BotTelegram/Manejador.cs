using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Entidades;
using Logica;
using Logica.IA;
using Logica.Servicios;
using Logica.Utilidades;

namespace BotTelegram
{
    /// <summary>Pasos para enviar la hoja de vida (aspirantes: no necesitan cuenta ni la aplicacion).</summary>
    public enum PasoPostulacion { TipoDocumento, NumeroDocumento, Nombres, Apellidos, Telefono, Correo, Ciudad, Autorizacion, HojaDeVida }

    public class FlujoPostulacion
    {
        public Vacante Vacante { get; init; } = new Vacante();
        public PasoPostulacion Paso { get; set; } = PasoPostulacion.TipoDocumento;
        public Postulacion Datos { get; } = new Postulacion();
        public bool Autoriza { get; set; }
        public DateTime Inicio { get; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Recibe cada mensaje o boton de Telegram y decide que hacer.
    /// - Chats SIN vincular: /start, /ayuda, /vincular, /vacantes y el envio de hojas de vida.
    /// - Chats vinculados: comandos de consulta y preguntas libres al asistente de Groq.
    /// </summary>
    public class Manejador
    {
        private const string TextoAutorizacion =
            "Autorización de tratamiento de datos (Ley 1581 de 2012): autorizo a {0} a recolectar y usar mis datos personales y mi hoja de vida " +
            "únicamente para este proceso de selección. Puedo conocer, actualizar o pedir que se eliminen mis datos escribiendo al emprendimiento.";

        private readonly TelegramApi _tg;
        private readonly FabricaServicios _s;
        private readonly Asistente _asistente;
        private readonly Herramientas _herramientas;
        private readonly int _limitePreguntas;
        private readonly ConcurrentDictionary<long, FlujoPostulacion> _flujos = new ConcurrentDictionary<long, FlujoPostulacion>();
        private readonly ConcurrentDictionary<long, Queue<DateTime>> _preguntas = new ConcurrentDictionary<long, Queue<DateTime>>();

        public Manejador(TelegramApi tg, FabricaServicios servicios, Asistente asistente, Herramientas herramientas, int limitePreguntas)
        {
            _tg = tg;
            _s = servicios;
            _asistente = asistente;
            _herramientas = herramientas;
            _limitePreguntas = limitePreguntas;
        }

        public async Task Procesar(JsonNode actualizacion, CancellationToken ct)
        {
            if (actualizacion["callback_query"] is JsonObject boton)
            {
                long chat = boton["message"]?["chat"]?["id"]?.GetValue<long>() ?? 0;
                string dato = boton["data"]?.ToString() ?? string.Empty;
                await _tg.ResponderBoton(boton["id"]?.ToString() ?? string.Empty, ct);
                if (chat != 0) await Seguro(chat, () => AlPulsar(chat, dato, ct), ct);
                return;
            }

            if (actualizacion["message"] is not JsonObject mensaje) return;
            long chatId = mensaje["chat"]?["id"]?.GetValue<long>() ?? 0;
            if (chatId == 0) return;
            // Solo chats privados: el bot maneja datos de negocio y personales
            if (mensaje["chat"]?["type"]?.ToString() != "private")
            {
                await _tg.EnviarMensaje(chatId, "Por privacidad, SGAPE solo funciona en chats privados.", null, ct);
                return;
            }

            if (mensaje["document"] is JsonObject documento)
            {
                await Seguro(chatId, () => AlRecibirDocumento(chatId, documento, ct), ct);
                return;
            }

            string texto = mensaje["text"]?.ToString()?.Trim() ?? string.Empty;
            if (texto.Length == 0)
            {
                await _tg.EnviarMensaje(chatId, "Por ahora solo entiendo mensajes de texto y archivos PDF o Word.", null, ct);
                return;
            }
            await Seguro(chatId, () => AlRecibirTexto(chatId, texto, ct), ct);
        }

        /// <summary>Ningun error tumba el bot: los de negocio se muestran, los demas se registran.</summary>
        private async Task Seguro(long chatId, Func<Task> accion, CancellationToken ct)
        {
            try
            {
                await accion();
            }
            catch (ReglaDeNegocioException ex)
            {
                await _tg.EnviarMensaje(chatId, ex.Message, null, ct);
            }
            catch (LimiteGroqException)
            {
                await _tg.EnviarMensaje(chatId, "El asistente está muy ocupado en este momento. Usa los comandos (/resumen, /stock...) o inténtalo en un minuto.", null, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(DateTime.Now.ToString("s") + " ERROR chat " + chatId + ": " + ex);
                string detalle = ex.Message.Length > 300 ? ex.Message.Substring(0, 300) : ex.Message;
                await _tg.EnviarMensaje(chatId, "Ocurrió un error. Inténtalo de nuevo en unos segundos.\n\nDetalle técnico: " + detalle, null, ct);
            }
        }

        // =================================================================
        // TEXTO
        // =================================================================

        private async Task AlRecibirTexto(long chatId, string texto, CancellationToken ct)
        {
            string comando = texto.StartsWith("/") ? texto.Split(' ', 2)[0].Split('@')[0].ToLowerInvariant() : string.Empty;
            string argumento = texto.Contains(' ') ? texto.Substring(texto.IndexOf(' ') + 1).Trim() : string.Empty;

            if (comando == "/cancelar")
            {
                _flujos.TryRemove(chatId, out _);
                _asistente.Olvidar(chatId);
                await _tg.EnviarMensaje(chatId, "Listo, cancelado.", null, ct);
                return;
            }

            // Si esta enviando una hoja de vida, el texto es la respuesta al paso actual
            if (_flujos.TryGetValue(chatId, out FlujoPostulacion? flujo) && comando.Length == 0)
            {
                await PasoDelFlujo(chatId, flujo, texto, ct);
                return;
            }

            switch (comando)
            {
                case "/start":
                case "/ayuda":
                    await Ayuda(chatId, ct);
                    return;
                case "/vincular":
                    VinculoTelegram v = _s.Telegram.Vincular(chatId, argumento);
                    _asistente.Olvidar(chatId);
                    await _tg.EnviarMensaje(chatId, "¡Hola, " + v.NombreUsuario + "! Tu cuenta quedó vinculada. Escribe /resumen o pregúntame lo que necesites.", null, ct);
                    await Emprendimientos(chatId, ct);
                    return;
                case "/vacantes":
                    await Vacantes(chatId, ct);
                    return;
                case "/postular":
                    if (int.TryParse(argumento, out int idVacante)) await IniciarPostulacion(chatId, idVacante, ct);
                    else await Vacantes(chatId, ct);
                    return;
            }

            // A partir de aqui se necesita una cuenta vinculada
            ContextoChat? ctx = Contexto(chatId);
            if (ctx == null)
            {
                await _tg.EnviarMensaje(chatId,
                    "Este chat no está vinculado a una cuenta de SGAPE.\n\n" +
                    "• Si eres el dueño: en la aplicación entra a Telegram > Generar código y escribe aquí /vincular 123456.\n" +
                    "• Si buscas empleo: escribe /vacantes.", null, ct);
                return;
            }

            switch (comando)
            {
                case "/desvincular":
                    _s.Telegram.Desvincular(chatId);
                    _asistente.Olvidar(chatId);
                    await _tg.EnviarMensaje(chatId, "Chat desvinculado. Para volver a usarlo genera un código nuevo en la aplicación.", null, ct);
                    return;
                case "/emprendimientos":
                    await Emprendimientos(chatId, ct);
                    return;
                case "/resumen":
                    await Resumen(ctx, ct);
                    return;
                case "/reporte":
                    await EnviarReporte(ctx, ct);
                    return;
                case "/stock":
                    await Stock(ctx, ct);
                    return;
                case "/presupuestos":
                    await Presupuestos(ctx, ct);
                    return;
                case "/nomina":
                    await Nomina(ctx, ct);
                    return;
                case "/ventas":
                    await VentasDeHoy(ctx, ct);
                    return;
                case "/postulaciones":
                    await Postulaciones(ctx, ct);
                    return;
                case "/alertas":
                    await Alertas(ctx, argumento, ct);
                    return;
                case "":
                    await Preguntar(ctx, texto, ct);
                    return;
                default:
                    await _tg.EnviarMensaje(chatId, "No conozco ese comando. Escribe /ayuda.", null, ct);
                    return;
            }
        }

        private ContextoChat? Contexto(long chatId)
        {
            int? usuario = _s.Telegram.UsuarioDeChat(chatId);
            if (usuario == null) return null;
            int? emp = _s.Telegram.EmprendimientoDeChat(chatId);
            if (emp == null)
                throw new ReglaDeNegocioException("Tu cuenta no tiene emprendimientos activos. Crea uno en la aplicación y escribe /emprendimientos.");
            // Verificar vuelve a comprobar que el emprendimiento sea del usuario vinculado
            Emprendimiento e = _s.Emprendimientos.Verificar(usuario.Value, emp.Value);
            return new ContextoChat { ChatId = chatId, UsuarioId = usuario.Value, Emprendimiento = e };
        }

        private async Task Preguntar(ContextoChat ctx, string pregunta, CancellationToken ct)
        {
            if (!PuedePreguntar(ctx.ChatId))
            {
                await _tg.EnviarMensaje(ctx.ChatId, "Hiciste muchas preguntas seguidas. Espera unos minutos o usa los comandos (/resumen, /stock, /reporte).", null, ct);
                return;
            }
            await _tg.EscribiendoAccion(ctx.ChatId, ct);
            _herramientas.Reiniciar();
            string respuesta = await _asistente.Responder(ctx, pregunta, ct);

            List<List<Boton>>? botones = null;
            string? id = _herramientas.IdConfirmacion;
            if (id != null && _herramientas.Pendientes.TryGetValue(id, out AccionPendiente? p))
            {
                respuesta += "\n\n" + (p.EsIngreso ? "Ingreso" : "Gasto") + ": " + p.Categoria + " por " + Formato.Moneda(p.Monto) +
                             (p.Descripcion.Length > 0 ? " (" + p.Descripcion + ")" : string.Empty) + ". ¿Lo registro?";
                botones = new List<List<Boton>> { new List<Boton> { new Boton("✅ Confirmar", "ok:" + id), new Boton("❌ Cancelar", "no:" + id) } };
            }
            await _tg.EnviarMensaje(ctx.ChatId, respuesta, botones, ct);
            if (_herramientas.PidioReporte) await EnviarReporte(ctx, ct);
        }

        private bool PuedePreguntar(long chatId)
        {
            Queue<DateTime> cola = _preguntas.GetOrAdd(chatId, _ => new Queue<DateTime>());
            lock (cola)
            {
                while (cola.Count > 0 && cola.Peek() < DateTime.UtcNow.AddMinutes(-10)) cola.Dequeue();
                if (cola.Count >= _limitePreguntas) return false;
                cola.Enqueue(DateTime.UtcNow);
                return true;
            }
        }

        // =================================================================
        // COMANDOS DE CONSULTA (sin modelo de IA: van directo a Oracle)
        // =================================================================

        private async Task Ayuda(long chatId, CancellationToken ct)
        {
            bool vinculado = _s.Telegram.UsuarioDeChat(chatId) != null;
            StringBuilder sb = new StringBuilder("Soy el bot de SGAPE.\n\n");
            if (vinculado)
            {
                sb.AppendLine("/resumen - resumen del emprendimiento");
                sb.AppendLine("/reporte - reporte completo en PDF");
                sb.AppendLine("/stock - productos con stock bajo");
                sb.AppendLine("/presupuestos - presupuestos del mes");
                sb.AppendLine("/nomina - nómina del mes");
                sb.AppendLine("/ventas - ventas de hoy");
                sb.AppendLine("/postulaciones - hojas de vida por revisar");
                sb.AppendLine("/alertas - activar o desactivar los avisos automáticos");
                sb.AppendLine("/emprendimientos - cambiar de emprendimiento");
                sb.AppendLine("/desvincular - desvincular este chat");
                sb.AppendLine();
                sb.AppendLine("También puedes preguntarme, por ejemplo: \"¿cuánto vendí esta semana?\" o \"registra un gasto de 50000 en transporte\".");
                sb.AppendLine();
                sb.Append("🔔 Sin que escribas nada te aviso del stock bajo, los presupuestos al 80 %, las hojas de vida nuevas y el cierre de cada día.");
            }
            else
            {
                sb.AppendLine("• Dueño de un emprendimiento: genera un código en la aplicación (sección Telegram) y escribe /vincular 123456.");
                sb.AppendLine("• ¿Buscas empleo? Escribe /vacantes para ver las ofertas y enviar tu hoja de vida.");
            }
            await _tg.EnviarMensaje(chatId, sb.ToString(), null, ct);
        }

        private async Task Emprendimientos(long chatId, CancellationToken ct)
        {
            int? usuario = _s.Telegram.UsuarioDeChat(chatId);
            if (usuario == null) { await Ayuda(chatId, ct); return; }
            List<Emprendimiento> lista = _s.Emprendimientos.Listar(usuario.Value);
            if (lista.Count == 0)
            {
                await _tg.EnviarMensaje(chatId, "Todavía no tienes emprendimientos. Créalos en la aplicación de escritorio.", null, ct);
                return;
            }
            int? actual = _s.Telegram.EmprendimientoDeChat(chatId);
            List<List<Boton>> botones = new List<List<Boton>>();
            foreach (Emprendimiento e in lista)
                botones.Add(new List<Boton> { new Boton((e.Id == actual ? "✔ " : "") + e.Nombre, "emp:" + e.Id) });
            await _tg.EnviarMensaje(chatId, "¿Con qué emprendimiento quieres trabajar?", botones, ct);
        }

        private async Task Resumen(ContextoChat ctx, CancellationToken ct)
        {
            await _tg.EnviarMensaje(ctx.ChatId, Textos.Resumen(_s, ctx.Emprendimiento.Id), null, ct);
        }

        /// <summary>/alertas: activa o desactiva los avisos automaticos (sp_sg_telegram_alertas).</summary>
        private async Task Alertas(ContextoChat ctx, string argumento, CancellationToken ct)
        {
            string a = argumento.Trim().ToLowerInvariant();
            bool? activar = a == "on" || a == "si" || a == "sí" ? true : a == "off" || a == "no" ? false : (bool?)null;
            bool activas = _s.Telegram.CambiarAlertas(ctx.ChatId, activar);
            await _tg.EnviarMensaje(ctx.ChatId, activas
                ? "🔔 Alertas ACTIVADAS. Te aviso cuando un producto quede con stock bajo, cuando un presupuesto llegue al 80 % o se pase, " +
                  "cuando llegue una hoja de vida y cada noche con el cierre del día."
                : "🔕 Alertas desactivadas. Escribe /alertas para volver a activarlas.", null, ct);
        }

        /// <summary>/ventas: las ventas de hoy, una por linea.</summary>
        private async Task VentasDeHoy(ContextoChat ctx, CancellationToken ct)
        {
            List<Venta> hoy = _s.Ventas.Listar(ctx.Emprendimiento.Id).FindAll(v => v.Fecha.Date == DateTime.Today);
            if (hoy.Count == 0) { await _tg.EnviarMensaje(ctx.ChatId, "Hoy todavía no hay ventas registradas.", null, ct); return; }
            decimal total = 0m;
            StringBuilder sb = new StringBuilder("🛒 Ventas de hoy:\n\n");
            int n = 0;
            foreach (Venta v in hoy)
            {
                total += v.Total;
                if (++n <= 20) sb.AppendLine("• " + v.Fecha.ToString("HH:mm") + "  #" + v.Id + "  " + v.Productos + " producto(s)  " + Formato.Moneda(v.Total));
            }
            if (hoy.Count > 20) sb.AppendLine("… y " + (hoy.Count - 20) + " más.");
            sb.AppendLine().Append("Total: " + Formato.Moneda(total) + " en " + hoy.Count + " venta(s).");
            await _tg.EnviarMensaje(ctx.ChatId, sb.ToString(), null, ct);
        }

        /// <summary>/postulaciones: hojas de vida sin revisar, con boton para descargarlas.</summary>
        private async Task Postulaciones(ContextoChat ctx, CancellationToken ct)
        {
            StringBuilder sb = new StringBuilder();
            List<List<Boton>> botones = new List<List<Boton>>();
            foreach (Vacante v in _s.Vacantes.Listar(ctx.Emprendimiento.Id))
                foreach (Postulacion p in _s.Vacantes.Postulaciones(ctx.Emprendimiento.Id, v.Id))
                {
                    if (p.Estado != EstadosPostulacion.Recibida && p.Estado != EstadosPostulacion.EnRevision) continue;
                    sb.AppendLine("• " + p.Nombre + " " + p.Apellido + " → " + v.Titulo + " (" + Formato.Fecha(p.Fecha) + ")");
                    if (botones.Count < 10) botones.Add(new List<Boton> { new Boton("📄 " + p.Nombre + " " + p.Apellido, "cv:" + p.Id) });
                }
            if (sb.Length == 0) { await _tg.EnviarMensaje(ctx.ChatId, "No hay hojas de vida pendientes.", null, ct); return; }
            await _tg.EnviarMensaje(ctx.ChatId, "📄 Hojas de vida por revisar:\n\n" + sb + "\nToca un nombre para recibir el archivo. Para contratar, usa la sección Vacantes de la aplicación.", botones, ct);
        }

        private async Task EnviarHojaDeVida(long chatId, int postulacionId, CancellationToken ct)
        {
            ContextoChat? ctx = Contexto(chatId);
            if (ctx == null) { await Ayuda(chatId, ct); return; }
            // HojaDeVida comprueba que la postulacion sea del emprendimiento del chat y la marca "en revision"
            ArchivoCv cv = _s.Vacantes.HojaDeVida(ctx.Emprendimiento.Id, postulacionId);
            await _tg.EnviarDocumento(chatId, cv.Contenido, cv.Nombre, "Hoja de vida", ct);
        }

        private async Task EnviarReporte(ContextoChat ctx, CancellationToken ct)
        {
            await _tg.EnviarMensaje(ctx.ChatId, "Generando el reporte en PDF...", null, ct);
            byte[] pdf = _s.Reportes.ReportePdf(ctx.Emprendimiento);
            await _tg.EnviarDocumento(ctx.ChatId, pdf, ServicioReportes.NombreArchivoReporte(ctx.Emprendimiento),
                                      "Reporte de " + ctx.Emprendimiento.Nombre, ct);
        }

        private async Task Stock(ContextoChat ctx, CancellationToken ct)
        {
            List<ProductoStockBajo> lista = _s.Inventario.StockBajo(ctx.Emprendimiento.Id);
            if (lista.Count == 0) { await _tg.EnviarMensaje(ctx.ChatId, "✅ Ningún producto está por debajo del mínimo.", null, ct); return; }
            StringBuilder sb = new StringBuilder("⚠️ Productos con stock bajo:\n\n");
            foreach (ProductoStockBajo p in lista)
                sb.AppendLine("• " + p.Nombre + ": " + p.Stock + " (mínimo " + p.StockMinimo + ")");
            await _tg.EnviarMensaje(ctx.ChatId, sb.ToString(), null, ct);
        }

        private async Task Presupuestos(ContextoChat ctx, CancellationToken ct)
        {
            string mes = Validar.MesActual();
            List<Presupuesto> lista = _s.Presupuestos.Listar(ctx.Emprendimiento.Id, mes);
            if (lista.Count == 0) { await _tg.EnviarMensaje(ctx.ChatId, "No hay presupuestos para " + Formato.NombreMes(mes) + ".", null, ct); return; }
            StringBuilder sb = new StringBuilder("Presupuestos de " + Formato.NombreMes(mes) + ":\n\n");
            foreach (Presupuesto p in lista)
            {
                string icono = p.Estado == Presupuesto.EstadoExcedido ? "🔴" : p.Estado == Presupuesto.EstadoAlerta ? "🟠" : "🟢";
                sb.AppendLine(icono + " " + p.Categoria + ": " + Formato.Moneda(p.Gastado) + " de " + Formato.Moneda(p.MontoLimite) +
                              " (" + Formato.Porcentaje(p.PorcentajeUso) + ")");
            }
            await _tg.EnviarMensaje(ctx.ChatId, sb.ToString(), null, ct);
        }

        private async Task Nomina(ContextoChat ctx, CancellationToken ct)
        {
            ResumenNomina r = _s.Nomina.Resumen(ctx.Emprendimiento.Id);
            StringBuilder sb = new StringBuilder("👥 Nómina (" + r.TrabajadoresActivos + " activos)\n\n");
            sb.AppendLine("Mensual: " + Formato.Moneda(r.NominaMensual));
            sb.AppendLine("Quincena: " + Formato.Moneda(r.NominaQuincenal));
            sb.AppendLine("Deducciones (8 %): " + Formato.Moneda(r.TotalDeducciones));
            sb.AppendLine("Neta: " + Formato.Moneda(r.NominaNeta));
            await _tg.EnviarMensaje(ctx.ChatId, sb.ToString(), null, ct);
        }

        // =================================================================
        // BOTONES
        // =================================================================

        private async Task AlPulsar(long chatId, string dato, CancellationToken ct)
        {
            string[] partes = dato.Split(':', 2);
            string clave = partes[0], valor = partes.Length > 1 ? partes[1] : string.Empty;

            switch (clave)
            {
                case "emp":
                    if (!int.TryParse(valor, out int emp)) return;
                    _s.Telegram.FijarEmprendimiento(chatId, emp);   // Oracle valida que sea del usuario del chat
                    _asistente.Olvidar(chatId);
                    ContextoChat? ctx = Contexto(chatId);
                    await _tg.EnviarMensaje(chatId, "Ahora trabajas con " + ctx?.Emprendimiento.Nombre + ".", null, ct);
                    return;

                case "ok":
                case "no":
                    if (!_herramientas.Pendientes.TryRemove(valor, out AccionPendiente? p) || p.ChatId != chatId)
                    {
                        await _tg.EnviarMensaje(chatId, "Esa acción ya no está disponible.", null, ct);
                        return;
                    }
                    if (clave == "no") { await _tg.EnviarMensaje(chatId, "Cancelado. No se registró nada.", null, ct); return; }
                    if (DateTime.UtcNow - p.Creada > TimeSpan.FromMinutes(10))
                    {
                        await _tg.EnviarMensaje(chatId, "La confirmación venció. Vuelve a pedirlo.", null, ct);
                        return;
                    }
                    ContextoChat? c = Contexto(chatId);
                    if (c == null || c.Emprendimiento.Id != p.EmprendimientoId)
                    {
                        await _tg.EnviarMensaje(chatId, "Cambiaste de emprendimiento. Vuelve a pedirlo.", null, ct);
                        return;
                    }
                    _s.Finanzas.Registrar(p.EmprendimientoId, p.EsIngreso, p.CategoriaId, p.Descripcion, p.Monto, DateTime.Today);
                    await _tg.EnviarMensaje(chatId, "✅ " + (p.EsIngreso ? "Ingreso" : "Gasto") + " registrado: " + p.Categoria + " " + Formato.Moneda(p.Monto) + ".", null, ct);
                    return;

                case "vac":
                    if (int.TryParse(valor, out int vacante)) await IniciarPostulacion(chatId, vacante, ct);
                    return;

                case "cv":
                    if (int.TryParse(valor, out int postulacion)) await EnviarHojaDeVida(chatId, postulacion, ct);
                    return;

                case "td":
                case "ciu":
                case "aut":
                case "omitir":
                    if (_flujos.TryGetValue(chatId, out FlujoPostulacion? flujo)) await BotonDelFlujo(chatId, flujo, clave, valor, ct);
                    return;
            }
        }

        // =================================================================
        // VACANTES Y POSTULACION (para aspirantes, sin cuenta)
        // =================================================================

        private async Task Vacantes(long chatId, CancellationToken ct)
        {
            List<Vacante> lista = _s.Vacantes.Abiertas();
            if (lista.Count == 0) { await _tg.EnviarMensaje(chatId, "En este momento no hay vacantes abiertas.", null, ct); return; }

            StringBuilder sb = new StringBuilder("💼 Vacantes abiertas:\n\n");
            List<List<Boton>> botones = new List<List<Boton>>();
            int n = 0;
            foreach (Vacante v in lista)
            {
                if (++n > 15) break;
                sb.AppendLine("#" + v.Id + " · " + v.Titulo + " — " + v.Emprendimiento);
                sb.AppendLine("   " + v.Ciudad + " · Salario: " + Formato.Moneda(v.SalarioBase) +
                              (v.FechaCierre.HasValue ? " · Cierra: " + Formato.Fecha(v.FechaCierre.Value) : string.Empty));
                if (!string.IsNullOrWhiteSpace(v.Descripcion)) sb.AppendLine("   " + (v.Descripcion.Length > 160 ? v.Descripcion.Substring(0, 160) + "…" : v.Descripcion));
                sb.AppendLine();
                botones.Add(new List<Boton> { new Boton("Postularme a #" + v.Id + " " + v.Titulo, "vac:" + v.Id) });
            }
            await _tg.EnviarMensaje(chatId, sb.ToString(), botones, ct);
        }

        private async Task IniciarPostulacion(long chatId, int vacanteId, CancellationToken ct)
        {
            Vacante? v = _s.Vacantes.Obtener(vacanteId);
            if (v == null || !v.EstaAbierta) { await _tg.EnviarMensaje(chatId, "Esa vacante ya no está disponible. Escribe /vacantes.", null, ct); return; }

            FlujoPostulacion flujo = new FlujoPostulacion { Vacante = v };
            flujo.Datos.VacanteId = v.Id;
            flujo.Datos.TelegramChatId = chatId;
            _flujos[chatId] = flujo;

            List<List<Boton>> tipos = new List<List<Boton>>();
            List<Boton> fila = new List<Boton>();
            foreach (TipoDocumento t in _s.Catalogos.TiposDocumento())
            {
                fila.Add(new Boton(t.Codigo, "td:" + t.Codigo));
                if (fila.Count == 3) { tipos.Add(fila); fila = new List<Boton>(); }
            }
            if (fila.Count > 0) tipos.Add(fila);

            await _tg.EnviarMensaje(chatId,
                "Vas a postularte a \"" + v.Titulo + "\" en " + v.Emprendimiento + ".\nPuedes escribir /cancelar en cualquier momento.\n\n" +
                "Paso 1 de 9: elige tu tipo de documento.", tipos, ct);
        }

        private async Task BotonDelFlujo(long chatId, FlujoPostulacion f, string clave, string valor, CancellationToken ct)
        {
            if (clave == "td" && f.Paso == PasoPostulacion.TipoDocumento)
            {
                TipoDocumento? t = _s.Catalogos.TipoDocumento(valor);
                if (t == null) return;
                f.Datos.TipoDocumento = t.Codigo;
                f.Paso = PasoPostulacion.NumeroDocumento;
                await _tg.EnviarMensaje(chatId, "Paso 2 de 9: escribe el número de tu " + t.Nombre + ".\n" + t.Ayuda, null, ct);
            }
            else if (clave == "omitir" && f.Paso == PasoPostulacion.Correo)
            {
                f.Datos.Email = string.Empty;
                await PedirCiudad(chatId, f, ct);
            }
            else if (clave == "ciu" && f.Paso == PasoPostulacion.Ciudad)
            {
                Ciudad? c = _s.Catalogos.Ciudad(valor);
                if (c == null) return;
                f.Datos.CiudadCodigo = c.Codigo;
                f.Paso = PasoPostulacion.Autorizacion;
                await _tg.EnviarMensaje(chatId, "Paso 8 de 9:\n\n" + string.Format(TextoAutorizacion, f.Vacante.Emprendimiento),
                    new List<List<Boton>> { new List<Boton> { new Boton("✅ Acepto", "aut:si"), new Boton("No acepto", "aut:no") } }, ct);
            }
            else if (clave == "aut" && f.Paso == PasoPostulacion.Autorizacion)
            {
                if (valor != "si")
                {
                    _flujos.TryRemove(chatId, out _);
                    await _tg.EnviarMensaje(chatId, "Entendido. Sin la autorización no podemos recibir tu hoja de vida, así que no guardamos ningún dato.", null, ct);
                    return;
                }
                f.Autoriza = true;
                f.Paso = PasoPostulacion.HojaDeVida;
                await _tg.EnviarMensaje(chatId, "Paso 9 de 9: envía tu hoja de vida como archivo PDF o Word (.docx), máximo 5 MB.", null, ct);
            }
        }

        private async Task PasoDelFlujo(long chatId, FlujoPostulacion f, string texto, CancellationToken ct)
        {
            if (DateTime.UtcNow - f.Inicio > TimeSpan.FromMinutes(30))
            {
                _flujos.TryRemove(chatId, out _);
                await _tg.EnviarMensaje(chatId, "La postulación venció por inactividad. Escribe /vacantes para empezar de nuevo.", null, ct);
                return;
            }

            try
            {
                switch (f.Paso)
                {
                    case PasoPostulacion.TipoDocumento:
                        await _tg.EnviarMensaje(chatId, "Elige el tipo de documento con los botones de arriba.", null, ct);
                        return;
                    case PasoPostulacion.NumeroDocumento:
                        f.Datos.NumeroDocumento = Validar.Documento(_s.Catalogos.TipoDocumento(f.Datos.TipoDocumento), texto);
                        f.Paso = PasoPostulacion.Nombres;
                        await _tg.EnviarMensaje(chatId, "Paso 3 de 9: escribe tus nombres.", null, ct);
                        return;
                    case PasoPostulacion.Nombres:
                        f.Datos.Nombre = Validar.NombrePersona(texto, "Nombres");
                        f.Paso = PasoPostulacion.Apellidos;
                        await _tg.EnviarMensaje(chatId, "Paso 4 de 9: escribe tus apellidos.", null, ct);
                        return;
                    case PasoPostulacion.Apellidos:
                        f.Datos.Apellido = Validar.NombrePersona(texto, "Apellidos");
                        f.Paso = PasoPostulacion.Telefono;
                        await _tg.EnviarMensaje(chatId, "Paso 5 de 9: escribe tu número de celular (10 dígitos).", null, ct);
                        return;
                    case PasoPostulacion.Telefono:
                        f.Datos.Telefono = Validar.Telefono(texto, true);
                        f.Paso = PasoPostulacion.Correo;
                        await _tg.EnviarMensaje(chatId, "Paso 6 de 9: escribe tu correo electrónico.",
                            new List<List<Boton>> { new List<Boton> { new Boton("No tengo correo", "omitir:") } }, ct);
                        return;
                    case PasoPostulacion.Correo:
                        f.Datos.Email = Validar.Correo(texto, false);
                        await PedirCiudad(chatId, f, ct);
                        return;
                    case PasoPostulacion.Ciudad:
                        await SugerirCiudades(chatId, texto, ct);
                        return;
                    case PasoPostulacion.Autorizacion:
                        await _tg.EnviarMensaje(chatId, "Responde con los botones Acepto / No acepto.", null, ct);
                        return;
                    case PasoPostulacion.HojaDeVida:
                        await _tg.EnviarMensaje(chatId, "Ahora envía el archivo de tu hoja de vida (PDF o Word) con el clip 📎.", null, ct);
                        return;
                }
            }
            catch (ReglaDeNegocioException ex)
            {
                // El paso no avanza: se explica el error y se vuelve a pedir el dato
                await _tg.EnviarMensaje(chatId, ex.Message + "\nIntenta de nuevo.", null, ct);
            }
        }

        private async Task PedirCiudad(long chatId, FlujoPostulacion f, CancellationToken ct)
        {
            f.Paso = PasoPostulacion.Ciudad;
            await _tg.EnviarMensaje(chatId, "Paso 7 de 9: ¿en qué ciudad o municipio vives? Escribe el nombre (por ejemplo: Valledupar).", null, ct);
        }

        /// <summary>Texto predictivo en el bot: se buscan municipios parecidos y se muestran como botones.</summary>
        private async Task SugerirCiudades(long chatId, string texto, CancellationToken ct)
        {
            if (texto.Trim().Length < 3)
            {
                await _tg.EnviarMensaje(chatId, "Escribe al menos 3 letras del municipio.", null, ct);
                return;
            }
            List<Ciudad> opciones = _s.Catalogos.BuscarCiudades(texto, 8);
            if (opciones.Count == 0)
            {
                await _tg.EnviarMensaje(chatId, "No encontré ese municipio. Revisa cómo está escrito.", null, ct);
                return;
            }
            List<List<Boton>> botones = new List<List<Boton>>();
            foreach (Ciudad c in opciones) botones.Add(new List<Boton> { new Boton(c.NombreCompleto, "ciu:" + c.Codigo) });
            await _tg.EnviarMensaje(chatId, "Elige tu municipio:", botones, ct);
        }

        private async Task AlRecibirDocumento(long chatId, JsonObject documento, CancellationToken ct)
        {
            if (!_flujos.TryGetValue(chatId, out FlujoPostulacion? f) || f.Paso != PasoPostulacion.HojaDeVida)
            {
                await _tg.EnviarMensaje(chatId, "Para enviar una hoja de vida primero elige una vacante con /vacantes.", null, ct);
                return;
            }

            long tamano = documento["file_size"]?.GetValue<long>() ?? 0;
            if (tamano > ServicioVacantes.TamanoMaximoCv)
            {
                await _tg.EnviarMensaje(chatId, "El archivo pesa más de 5 MB. Envía una versión más liviana.", null, ct);
                return;
            }

            string nombre = documento["file_name"]?.ToString() ?? "hoja_de_vida";
            string tipo = documento["mime_type"]?.ToString() ?? string.Empty;
            if (tipo != ServicioVacantes.TipoPdf && tipo != ServicioVacantes.TipoDocx)
            {
                if (nombre.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) tipo = ServicioVacantes.TipoPdf;
                else if (nombre.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)) tipo = ServicioVacantes.TipoDocx;
                else { await _tg.EnviarMensaje(chatId, "Envía la hoja de vida en PDF o Word (.docx).", null, ct); return; }
            }

            byte[] contenido = await _tg.DescargarArchivo(documento["file_id"]?.ToString() ?? string.Empty, ct);
            ArchivoCv cv = new ArchivoCv { Nombre = nombre.Length > 200 ? nombre.Substring(nombre.Length - 200) : nombre, Tipo = tipo, Contenido = contenido };

            int id = _s.Vacantes.RegistrarPostulacion(f.Datos, cv, f.Autoriza);
            _flujos.TryRemove(chatId, out _);
            await _tg.EnviarMensaje(chatId,
                "✅ ¡Listo, " + f.Datos.Nombre + "! Recibimos tu hoja de vida para \"" + f.Vacante.Titulo + "\" (radicado #" + id + ").\n" +
                f.Vacante.Emprendimiento + " la revisará y te contactará al " + f.Datos.Telefono + ".", null, ct);
        }
    }
}
