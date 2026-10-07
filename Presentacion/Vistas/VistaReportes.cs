using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Servicios;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.Vistas
{
    /// <summary>Estadisticas de ventas y generacion del reporte HTML con graficas.</summary>
    public class VistaReportes : VistaBase
    {
        private readonly TarjetaKpi _kHoy = new TarjetaKpi("Ventas de hoy", Tema.Verde);
        private readonly TarjetaKpi _kSemana = new TarjetaKpi("Últimos 7 días", Tema.VerdeOscuro);
        private readonly TarjetaKpi _kMes = new TarjetaKpi("Este mes", Tema.Dorado);
        private readonly TarjetaKpi _kTotal = new TarjetaKpi("Total histórico", Tema.Gris);
        private readonly DataGridView _tabla = Tema.Tabla("Top productos más vendidos", "Unidades");

        public VistaReportes(FabricaServicios fabrica) : base(fabrica)
        {
            _tabla.AlinearDerecha(1);

            Button generar = Tema.Boton("Generar reporte con gráficas (HTML)", Tema.Verde);
            Button carpeta = Tema.Boton("Abrir carpeta de reportes", Tema.Gris);
            generar.Click += delegate { Generar(); };
            carpeta.Click += delegate { Mensajes.Abrir(RutasApp.CarpetaReportes); };

            Label nota = Tema.Etiqueta("El reporte se abre en tu navegador. Las gráficas necesitan conexión a internet; las tablas funcionan sin ella.");
            nota.ForeColor = Tema.TextoSuave;

            TableLayoutPanel raiz = Tema.ColumnaVertical();
            raiz.Fila(Tema.TituloVista("Reportes y estadísticas"), false);
            raiz.Fila(Tema.FilaTarjetas(_kHoy, _kSemana, _kMes, _kTotal), false);
            raiz.Fila(Tema.Barra(generar, carpeta), false);
            raiz.Fila(nota, false);
            raiz.Fila(_tabla, true);
            Controls.Add(raiz);
        }

        protected override void Recargar()
        {
            _tabla.Rows.Clear();
            _kHoy.Valor = Formato.Moneda(0);
            _kSemana.Valor = Formato.Moneda(0);
            _kMes.Valor = Formato.Moneda(0);
            _kTotal.Valor = Formato.Moneda(0);
            if (Emprendimiento == null) return;

            int id = Emprendimiento.Id;
            Mensajes.Ejecutar(delegate
            {
                ResumenVentas r = Fabrica.Reportes.CalcularResumenVentas(id);
                _kHoy.Valor = Formato.Moneda(r.Hoy);
                _kSemana.Valor = Formato.Moneda(r.UltimosSieteDias);
                _kMes.Valor = Formato.Moneda(r.EsteMes);
                _kTotal.Valor = Formato.Moneda(r.TotalHistorico);
                foreach (KeyValuePair<string, int> kv in r.TopProductos)
                    _tabla.AgregarFila(kv.Key, kv.Key, kv.Value);
            });
        }

        private void Generar()
        {
            if (!ExigirEmprendimiento()) return;
            Emprendimiento emp = Emprendimiento!;
            string ruta = "";
            if (Mensajes.Ejecutar(delegate { ruta = Fabrica.Reportes.GenerarReporteHtml(emp); }))
            {
                Mensajes.Abrir(ruta);
            }
        }
    }
}