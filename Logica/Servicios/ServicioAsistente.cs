using System.Collections.Generic;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: responder preguntas frecuentes (el chatbot del proyecto web, ahora en C#).
    /// Las palabras clave se comparan sin tildes ni mayusculas.
    /// </summary>
    public class ServicioAsistente
    {
        private class Regla
        {
            public string[] Palabras { get; }
            public string Respuesta { get; }
            public Regla(string[] palabras, string respuesta) { Palabras = palabras; Respuesta = respuesta; }
        }

        private const string Defecto =
            "No estoy seguro de cómo ayudarte con eso. Puedes preguntarme sobre: emprendimientos, nómina, " +
            "inventario, ventas, finanzas, reportes, formalizar tu negocio o marketing.";

        private readonly List<Regla> _reglas = new List<Regla>
        {
            new Regla(new[]{"hola","buenas","saludos","hey"},
                "¡Hola! Soy tu asistente emprendedor. ¿En qué puedo ayudarte hoy?"),
            new Regla(new[]{"que es","para que","sirve","proposito","objetivo"},
                "SGAPE te ayuda a administrar tus emprendimientos: trabajadores y nómina, inventario, ventas con factura, ingresos y gastos, y reportes con gráficas."),
            new Regla(new[]{"emprendimiento","negocio","empresa"},
                "Para crear un emprendimiento ve a la sección Emprendimientos y pulsa 'Nuevo emprendimiento'. Registra nombre, descripción, sector y fecha de inicio."),
            new Regla(new[]{"nomina","salario","sueldo","cargo","trabajador","empleado"},
                "En Nómina primero crea los cargos (con su salario base) y luego registra a los trabajadores. Se descuenta 4% de salud y 4% de pensión."),
            new Regla(new[]{"inventario","producto","stock","existencia"},
                "En Inventario agrega productos con precio de compra, precio de venta y stock mínimo. Los que estén por debajo del mínimo se marcan en rojo."),
            new Regla(new[]{"venta","vender","factura","cliente"},
                "En Ventas elige los productos y cantidades, y pulsa 'Registrar venta'. El stock se descuenta solo y puedes generar la factura."),
            new Regla(new[]{"ingreso","ganancia","entrada"},
                "En Finanzas pulsa 'Nuevo ingreso' o 'Nuevo gasto' para registrar movimientos manuales. Las ventas se registran solas."),
            new Regla(new[]{"gasto","egreso","pago","costo","salida"},
                "Registra tus gastos en Finanzas > 'Nuevo gasto'. Descríbelos bien para llevar un mejor control."),
            new Regla(new[]{"balance","resultado","utilidad","perdida"},
                "El balance es ingresos menos gastos. Si es positivo, hay utilidades; si es negativo, revisa tus gastos o aumenta tus ventas."),
            new Regla(new[]{"reporte","grafica","estadistica"},
                "En Reportes puedes ver el resumen de ventas y generar un reporte HTML con gráficas (se abre en tu navegador)."),
            new Regla(new[]{"rut","dian","formalizar","camara de comercio","registro mercantil"},
                "Para formalizar tu negocio en Colombia: 1) Registro en la Cámara de Comercio, 2) RUT en la DIAN, 3) cuenta bancaria empresarial."),
            new Regla(new[]{"marketing","publicidad","redes","instagram","whatsapp"},
                "Para atraer clientes: crea tu perfil en Google Mi Negocio, publica en redes 3 veces por semana, usa WhatsApp Business y pide reseñas."),
            new Regla(new[]{"precio","cobrar","tarifa","margen"},
                "Precio = costo total × (1 + % de ganancia). En Inventario ves el margen de cada producto. Compara también con tu competencia."),
            new Regla(new[]{"credito","prestamo","financiacion","capital"},
                "Fuentes de financiación en Colombia: Fondo Emprender (SENA), Bancóldex, iNNpulsa Colombia y líneas de crédito de los bancos."),
            new Regla(new[]{"ayuda","no entiendo","instrucciones"},
                "Con gusto. Pregúntame por: emprendimientos, nómina, inventario, ventas, finanzas, reportes, formalización o marketing."),
            new Regla(new[]{"gracias","perfecto","excelente","genial"},
                "¡De nada! Aquí estoy para ayudarte a hacer crecer tu emprendimiento."),
            new Regla(new[]{"adios","hasta luego","chao","bye"},
                "¡Hasta pronto! Mucho éxito con tu emprendimiento.")
        };

        public string Responder(string mensaje)
        {
            string texto = Formato.Normalizar(mensaje).Trim();
            if (texto.Length == 0) return "Escribe tu pregunta y te ayudo.";

            string[] palabras = texto.Split(new[] { ' ', ',', '.', '?', '!', ';', ':' },
                System.StringSplitOptions.RemoveEmptyEntries);

            foreach (Regla regla in _reglas)
            {
                foreach (string clave in regla.Palabras)
                {
                    if (clave.Contains(" "))
                    {
                        if (texto.Contains(clave)) return regla.Respuesta;
                    }
                    else
                    {
                        foreach (string p in palabras)
                        {
                            if (p.StartsWith(clave, System.StringComparison.Ordinal)) return regla.Respuesta;
                        }
                    }
                }
            }
            return Defecto;
        }
    }
}