using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Entidades;
using Logica;
using Logica.Utilidades;

namespace BotTelegram
{
    /// <summary>
    /// Avisos AUTOMATICOS: el dueño no tiene que escribir ni tener el PC encendido.
    /// Cada SGAPE_ALERTAS_MINUTOS (15 por defecto) revisa en Oracle, para cada chat con alertas activas
    /// (vista vw_sg_telegram_alertas):
    ///   - productos que acaban de quedar con stock bajo   (sp_sg_productos_stock_bajo)
    ///   - presupuestos que pasan a ALERTA (80 %) o EXCEDIDO (sp_sg_presupuestos_estado)
    ///   - hojas de vida nuevas en sus vacantes             (vw_sg_postulaciones)
    /// y una vez al dia, a la hora SGAPE_RESUMEN_HORA (19 por defecto, hora de Colombia), manda el cierre del dia.
    /// Solo avisa lo NUEVO: si un producto sigue bajo, no repite el aviso cada 15 minutos.
    /// </summary>
    public class Vigilante
    {
        private readonly TelegramApi _tg;
        private readonly FabricaServicios _s;
        private readonly TimeSpan _cada;
        private readonly int _horaResumen;
        private readonly TimeZoneInfo _zona;

        // Lo ultimo que se aviso a cada chat (en memoria: tras reiniciar el bot solo se repite una vez)
        private readonly Dictionary<long, HashSet<int>> _stockAvisado = new Dictionary<long, HashSet<int>>();
        private readonly Dictionary<long, Dictionary<int, string>> _presupuestoAvisado = new Dictionary<long, Dictionary<int, string>>();
        private readonly Dictionary<long, int> _ultimaPostulacion = new Dictionary<long, int>();
        private readonly Dictionary<long, DateTime> _resumenEnviado = new Dictionary<long, DateTime>();

        private DateTime _ultimaRevision = DateTime.MinValue;
        private bool _avisoFaltaScript;

        public Vigilante(TelegramApi tg, FabricaServicios servicios, int minutos, int horaResumen)
        {
            _tg = tg;
            _s = servicios;
            _cada = TimeSpan.FromMinutes(Math.Max(5, minutos));
            _horaResumen = horaResumen;
            _zona = ZonaColombia();
        }

        private static TimeZoneInfo ZonaColombia()
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota"); }               // Linux (la VM)
            catch (Exception)
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time"); } // Windows
                catch (Exception) { return TimeZoneInfo.Local; }
            }
        }

        public DateTime Ahora { get { return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zona); } }

        /// <summary>Se llama en cada vuelta del ciclo principal; solo trabaja cuando toca.</summary>
        public async Task Revisar(CancellationToken ct)
        {
            if (DateTime.UtcNow - _ultimaRevision < _cada) return;
            _ultimaRevision = DateTime.UtcNow;

            List<ChatAlerta> chats;
            try
            {
                chats = _s.Telegram.ChatsConAlertas();
            }
            catch (Exception ex)
            {
                if (!_avisoFaltaScript)
                {
                    Console.Error.WriteLine(DateTime.Now.ToString("s") + " Alertas desactivadas: " + ex.Message +
                                            " (¿falta ejecutar sql/sgape_v2_alertas_bot.sql?)");
                    _avisoFaltaScript = true;
                }
                return;
            }

            foreach (ChatAlerta chat in chats)
            {
                try
                {
                    await StockBajo(chat, ct);
                    await Presupuestos(chat, ct);
                    await HojasDeVida(chat, ct);
                    await ResumenDiario(chat, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    // Un chat con problemas (por ejemplo, bloqueó el bot) no detiene los avisos de los demas
                    Console.Error.WriteLine(DateTime.Now.ToString("s") + " Alerta chat " + chat.ChatId + ": " + ex.Message);
                }
            }
        }

        private async Task StockBajo(ChatAlerta chat, CancellationToken ct)
        {
            List<ProductoStockBajo> bajos = _s.Inventario.StockBajo(chat.EmprendimientoId);
            HashSet<int> antes = Obtener(_stockAvisado, chat.ChatId);
            HashSet<int> ahora = new HashSet<int>();
            StringBuilder sb = new StringBuilder();
            foreach (ProductoStockBajo p in bajos)
            {
                ahora.Add(p.ProductoId);
                if (!antes.Contains(p.ProductoId))
                    sb.AppendLine("• " + p.Nombre + ": quedan " + p.Stock + " (mínimo " + p.StockMinimo + "). Pide " + Math.Max(p.Faltante, 1) + ".");
            }
            _stockAvisado[chat.ChatId] = ahora;   // si se repone y vuelve a bajar, se avisa otra vez
            if (sb.Length > 0)
                await _tg.EnviarMensaje(chat.ChatId, "⚠️ Stock bajo en " + chat.Emprendimiento + ":\n\n" + sb + "\nEscribe /stock para ver todos.", null, ct);
        }

        private async Task Presupuestos(ChatAlerta chat, CancellationToken ct)
        {
            string mes = Validar.MesActual();
            Dictionary<int, string> antes = Obtener(_presupuestoAvisado, chat.ChatId);
            Dictionary<int, string> ahora = new Dictionary<int, string>();
            StringBuilder sb = new StringBuilder();
            foreach (Presupuesto p in _s.Presupuestos.Listar(chat.EmprendimientoId, mes))
            {
                ahora[p.Id] = p.Estado;
                if (p.Estado == Presupuesto.EstadoOk) continue;
                if (antes.TryGetValue(p.Id, out string? previo) && previo == p.Estado) continue;
                string icono = p.Estado == Presupuesto.EstadoExcedido ? "🔴 Te pasaste" : "🟠 Vas en el " + Formato.Porcentaje(p.PorcentajeUso);
                sb.AppendLine(icono + " en " + p.Categoria + ": " + Formato.Moneda(p.Gastado) + " de " + Formato.Moneda(p.MontoLimite) + ".");
            }
            _presupuestoAvisado[chat.ChatId] = ahora;
            if (sb.Length > 0)
                await _tg.EnviarMensaje(chat.ChatId, "💸 Presupuestos de " + Formato.NombreMes(mes) + " (" + chat.Emprendimiento + "):\n\n" + sb, null, ct);
        }

        private async Task HojasDeVida(ChatAlerta chat, CancellationToken ct)
        {
            int maximo = 0;
            List<Postulacion> nuevas = new List<Postulacion>();
            bool primeraVez = !_ultimaPostulacion.TryGetValue(chat.ChatId, out int ultima);
            foreach (Vacante v in _s.Vacantes.Listar(chat.EmprendimientoId))
            {
                foreach (Postulacion p in _s.Vacantes.Postulaciones(chat.EmprendimientoId, v.Id))
                {
                    if (p.Id > maximo) maximo = p.Id;
                    if (!primeraVez && p.Id > ultima) nuevas.Add(p);
                }
            }
            _ultimaPostulacion[chat.ChatId] = Math.Max(maximo, primeraVez ? 0 : ultima);
            if (nuevas.Count == 0) return;

            StringBuilder sb = new StringBuilder("📄 " + (nuevas.Count == 1 ? "Llegó una hoja de vida" : "Llegaron " + nuevas.Count + " hojas de vida") + ":\n\n");
            List<List<Boton>> botones = new List<List<Boton>>();
            foreach (Postulacion p in nuevas)
            {
                sb.AppendLine("• " + p.Nombre + " " + p.Apellido + " → " + p.Vacante);
                if (botones.Count < 5) botones.Add(new List<Boton> { new Boton("Ver hoja de vida de " + p.Nombre, "cv:" + p.Id) });
            }
            await _tg.EnviarMensaje(chat.ChatId, sb.ToString(), botones, ct);
        }

        private async Task ResumenDiario(ChatAlerta chat, CancellationToken ct)
        {
            if (_horaResumen < 0) return;   // SGAPE_RESUMEN_HORA=-1 lo apaga
            DateTime ahora = Ahora;
            if (ahora.Hour < _horaResumen) return;
            if (_resumenEnviado.TryGetValue(chat.ChatId, out DateTime dia) && dia == ahora.Date) return;
            _resumenEnviado[chat.ChatId] = ahora.Date;
            await _tg.EnviarMensaje(chat.ChatId, "🌙 Cierre del día\n\n" + Textos.Resumen(_s, chat.EmprendimientoId) +
                                                 "\n\nPara dejar de recibir avisos escribe /alertas.", null, ct);
        }

        private static T Obtener<T>(Dictionary<long, T> mapa, long chat) where T : new()
        {
            if (!mapa.TryGetValue(chat, out T? valor)) { valor = new T(); mapa[chat] = valor; }
            return valor;
        }
    }

    /// <summary>Textos que usan tanto los comandos como los avisos automaticos (se escriben una sola vez).</summary>
    public static class Textos
    {
        public static string Resumen(FabricaServicios s, int emprendimientoId)
        {
            ResumenEmprendimiento r = s.Reportes.Resumen(emprendimientoId);
            ResumenVentas v = s.Ventas.Resumen(emprendimientoId);
            return
                "📊 " + r.Nombre + "\n\n" +
                "Ventas hoy: " + Formato.Moneda(v.Hoy) + "\n" +
                "Ventas 7 días: " + Formato.Moneda(v.UltimosSieteDias) + "\n" +
                "Ventas del mes: " + Formato.Moneda(v.EsteMes) + " (" + r.VentasDelMes + " ventas)\n\n" +
                "Ingresos: " + Formato.Moneda(r.Ingresos) + "\n" +
                "Gastos: " + Formato.Moneda(r.Gastos) + "\n" +
                "Balance: " + Formato.Moneda(r.Balance) + "\n\n" +
                "Trabajadores activos: " + r.TrabajadoresActivos + " · Nómina: " + Formato.Moneda(r.NominaMensual) + "\n" +
                "Productos: " + r.Productos + " · Con stock bajo: " + r.ProductosStockBajo + "\n" +
                "Hojas de vida nuevas: " + r.PostulacionesNuevas;
        }
    }
}
