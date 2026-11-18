using System;
using System.Collections.Generic;
using Entidades;
using Logica.Utilidades;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Logica.Pdf
{
    /// <summary>Estilos comunes de los PDF (colores de SGAPE, celdas de tabla, encabezado y pie).</summary>
    internal static class EstiloPdf
    {
        public const string Primario = "#00695C";     // verde azulado
        public const string PrimarioClaro = "#E0F2F1";
        public const string Alerta = "#E65100";
        public const string Peligro = "#C62828";
        public const string Exito = "#2E7D32";
        public const string Texto = "#212121";
        public const string Gris = "#757575";

        private static bool _licencia;

        public static void Licencia()
        {
            if (_licencia) return;
            QuestPDF.Settings.License = LicenseType.Community;
            _licencia = true;
        }

        public static IContainer Encabezado(IContainer c)
        {
            return c.Background(Primario).PaddingVertical(5).PaddingHorizontal(6);
        }

        public static IContainer Celda(IContainer c)
        {
            return c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(4).PaddingHorizontal(6);
        }

        public static void Pie(IContainer c, string texto)
        {
            c.AlignCenter().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Gris));
                t.Span(texto + "  ·  Página ");
                t.CurrentPageNumber();
                t.Span(" de ");
                t.TotalPages();
            });
        }

        public static void Titulo(ColumnDescriptor col, string texto)
        {
            col.Item().PaddingTop(12).PaddingBottom(4).Text(texto).FontSize(13).Bold().FontColor(Primario);
        }

        public static void SinDatos(ColumnDescriptor col, string texto)
        {
            col.Item().PaddingVertical(4).Text(texto).Italic().FontColor(Gris);
        }
    }

    /// <summary>Reporte estadistico del emprendimiento en PDF (reemplaza el antiguo reporte HTML).</summary>
    public static class DocumentoReporte
    {
        public static byte[] Generar(DatosReporte d)
        {
            EstiloPdf.Licencia();

            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(32);
                    page.DefaultTextStyle(s => s.FontSize(10).FontColor(EstiloPdf.Texto));

                    page.Header().Column(h =>
                    {
                        h.Item().Text("Reporte del emprendimiento").FontSize(9).FontColor(EstiloPdf.Gris);
                        h.Item().Text(d.Emprendimiento.Nombre).FontSize(20).Bold().FontColor(EstiloPdf.Primario);
                        h.Item().Text(d.Emprendimiento.Sector + "  ·  " + d.Emprendimiento.Ciudad +
                                      "  ·  Generado el " + Formato.FechaHora(d.Generado)).FontSize(9).FontColor(EstiloPdf.Gris);
                        h.Item().PaddingTop(6).LineHorizontal(1).LineColor(EstiloPdf.Primario);
                    });

                    page.Content().PaddingTop(8).Column(col =>
                    {
                        col.Spacing(4);

                        // ---- Indicadores ----
                        col.Item().Row(row =>
                        {
                            row.Spacing(8);
                            Indicador(row.RelativeItem(), "Ventas del mes", Formato.Moneda(d.Ventas.EsteMes), EstiloPdf.Primario);
                            Indicador(row.RelativeItem(), "Ingresos totales", Formato.Moneda(d.Finanzas.TotalIngresos), EstiloPdf.Exito);
                            Indicador(row.RelativeItem(), "Gastos totales", Formato.Moneda(d.Finanzas.TotalGastos), EstiloPdf.Peligro);
                            Indicador(row.RelativeItem(), "Balance", Formato.Moneda(d.Finanzas.Balance),
                                      d.Finanzas.Balance >= 0 ? EstiloPdf.Exito : EstiloPdf.Peligro);
                        });
                        col.Item().PaddingTop(6).Row(row =>
                        {
                            row.Spacing(8);
                            Indicador(row.RelativeItem(), "Ventas hoy", Formato.Moneda(d.Ventas.Hoy), EstiloPdf.Primario);
                            Indicador(row.RelativeItem(), "Últimos 7 días", Formato.Moneda(d.Ventas.UltimosSieteDias), EstiloPdf.Primario);
                            Indicador(row.RelativeItem(), "Valor del inventario", Formato.Moneda(d.ValorInventario), EstiloPdf.Primario);
                            Indicador(row.RelativeItem(), "Nómina neta mensual", Formato.Moneda(d.Nomina.NominaNeta), EstiloPdf.Primario);
                        });

                        // ---- Graficas de barras ----
                        EstiloPdf.Titulo(col, "Ventas de los últimos 6 meses");
                        List<ValorEtiquetado> meses = new List<ValorEtiquetado>();
                        foreach (ValorEtiquetado v in d.VentasPorMes)
                            meses.Add(new ValorEtiquetado { Etiqueta = Formato.NombreMes(v.Etiqueta), Valor = v.Valor });
                        Barras(col, meses, EstiloPdf.Primario, true);

                        EstiloPdf.Titulo(col, "Productos más vendidos");
                        if (d.TopProductos.Count == 0) EstiloPdf.SinDatos(col, "Todavía no hay ventas.");
                        else
                        {
                            List<ValorEtiquetado> top = new List<ValorEtiquetado>();
                            foreach (ValorEtiquetado v in d.TopProductos)
                                top.Add(new ValorEtiquetado { Etiqueta = v.Etiqueta + " (" + v.Cantidad + " und.)", Valor = v.Valor });
                            Barras(col, top, "#26A69A", true);
                        }

                        EstiloPdf.Titulo(col, "Gastos por categoría");
                        if (d.GastosPorCategoria.Count == 0) EstiloPdf.SinDatos(col, "No hay gastos registrados.");
                        else Barras(col, d.GastosPorCategoria, EstiloPdf.Alerta, true);

                        // ---- Presupuestos del mes ----
                        EstiloPdf.Titulo(col, "Presupuestos de " + Formato.NombreMes(Validar.MesActual()));
                        if (d.Presupuestos.Count == 0) EstiloPdf.SinDatos(col, "No hay presupuestos para este mes.");
                        else
                        {
                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1.3f); c.RelativeColumn(1.5f); });
                                t.Header(h =>
                                {
                                    foreach (string e in new[] { "Categoría", "Límite", "Gastado", "Uso", "Estado" })
                                        h.Cell().Element(EstiloPdf.Encabezado).Text(e).FontColor(Colors.White).Bold();
                                });
                                foreach (Presupuesto p in d.Presupuestos)
                                {
                                    string color = p.Estado == Presupuesto.EstadoExcedido ? EstiloPdf.Peligro
                                                 : p.Estado == Presupuesto.EstadoAlerta ? EstiloPdf.Alerta : EstiloPdf.Exito;
                                    t.Cell().Element(EstiloPdf.Celda).Text(p.Categoria);
                                    t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(p.MontoLimite));
                                    t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(p.Gastado));
                                    t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Porcentaje(p.PorcentajeUso));
                                    t.Cell().Element(EstiloPdf.Celda).Text(p.Estado).Bold().FontColor(color);
                                }
                            });
                        }

                        // ---- Stock bajo ----
                        EstiloPdf.Titulo(col, "Productos con stock bajo");
                        if (d.StockBajo.Count == 0) EstiloPdf.SinDatos(col, "Ningún producto está por debajo del mínimo.");
                        else
                        {
                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(2); c.RelativeColumn(1); c.RelativeColumn(1); c.RelativeColumn(1); });
                                t.Header(h =>
                                {
                                    foreach (string e in new[] { "Producto", "Categoría", "Stock", "Mínimo", "Faltan" })
                                        h.Cell().Element(EstiloPdf.Encabezado).Text(e).FontColor(Colors.White).Bold();
                                });
                                foreach (ProductoStockBajo p in d.StockBajo)
                                {
                                    t.Cell().Element(EstiloPdf.Celda).Text(p.Nombre);
                                    t.Cell().Element(EstiloPdf.Celda).Text(p.Categoria);
                                    t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(p.Stock.ToString()).FontColor(EstiloPdf.Peligro).Bold();
                                    t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(p.StockMinimo.ToString());
                                    t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Math.Max(p.Faltante, 0).ToString());
                                }
                            });
                        }

                        // ---- Nomina ----
                        EstiloPdf.Titulo(col, "Nómina (" + d.Nomina.TrabajadoresActivos + " trabajadores activos)");
                        if (d.DetalleNomina.Count == 0) EstiloPdf.SinDatos(col, "No hay trabajadores activos.");
                        else
                        {
                            col.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2.2f); c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(2); });
                                t.Header(h =>
                                {
                                    foreach (string e in new[] { "Trabajador", "Cargo", "Salario", "Deducciones", "Neto" })
                                        h.Cell().Element(EstiloPdf.Encabezado).Text(e).FontColor(Colors.White).Bold();
                                });
                                foreach (FilaNomina f in d.DetalleNomina)
                                {
                                    t.Cell().Element(EstiloPdf.Celda).Text(f.Trabajador);
                                    t.Cell().Element(EstiloPdf.Celda).Text(f.Cargo);
                                    t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(f.SalarioBase));
                                    t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(f.Deducciones));
                                    t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(f.Neto));
                                }
                                t.Cell().ColumnSpan(2).Element(EstiloPdf.Celda).Text("Total mensual").Bold();
                                t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(d.Nomina.NominaMensual)).Bold();
                                t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(d.Nomina.TotalDeducciones)).Bold();
                                t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(d.Nomina.NominaNeta)).Bold();
                            });
                        }
                    });

                    page.Footer().Element(c => EstiloPdf.Pie(c, "SGAPE · Sistema de Gestión y Administración de Emprendimientos"));
                });
            }).GeneratePdf();
        }

        private static void Indicador(IContainer c, string etiqueta, string valor, string color)
        {
            c.Border(1).BorderColor(Colors.Grey.Lighten2).Background(Colors.Grey.Lighten5).Padding(8).Column(col =>
            {
                col.Item().Text(etiqueta).FontSize(8).FontColor(EstiloPdf.Gris);
                col.Item().Text(valor).FontSize(13).Bold().FontColor(color);
            });
        }

        /// <summary>Grafica de barras horizontales hecha con cajas (sin librerias de graficas).</summary>
        private static void Barras(ColumnDescriptor col, List<ValorEtiquetado> datos, string color, bool moneda)
        {
            decimal maximo = 0m;
            foreach (ValorEtiquetado v in datos) if (v.Valor > maximo) maximo = v.Valor;
            const float anchoMaximo = 260f;

            foreach (ValorEtiquetado v in datos)
            {
                float ancho = maximo <= 0m ? 0f : (float)(v.Valor / maximo) * anchoMaximo;
                col.Item().PaddingVertical(1).Row(row =>
                {
                    row.ConstantItem(150).Text(v.Etiqueta).FontSize(9);
                    row.ConstantItem(anchoMaximo + 6).AlignMiddle().Row(barra =>
                    {
                        if (ancho >= 1f) barra.ConstantItem(ancho).Height(10).Background(color);
                        else barra.ConstantItem(1).Height(10).Background(Colors.Grey.Lighten2);
                    });
                    row.RelativeItem().AlignRight().Text(moneda ? Formato.Moneda(v.Valor) : v.Valor.ToString("0")).FontSize(9);
                });
            }
        }
    }

    /// <summary>Factura de venta en PDF. No lleva cantidad total de unidades: solo el valor total.</summary>
    public static class DocumentoFactura
    {
        public static byte[] Generar(Emprendimiento e, Venta v)
        {
            EstiloPdf.Licencia();

            return Document.Create(doc =>
            {
                doc.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(24);
                    page.DefaultTextStyle(s => s.FontSize(9).FontColor(EstiloPdf.Texto));

                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(e.Nombre).FontSize(16).Bold().FontColor(EstiloPdf.Primario);
                            c.Item().Text(e.Sector).FontColor(EstiloPdf.Gris);
                            c.Item().Text(e.Ciudad).FontColor(EstiloPdf.Gris);
                        });
                        row.ConstantItem(130).Background(EstiloPdf.PrimarioClaro).Padding(6).Column(c =>
                        {
                            c.Item().AlignRight().Text("FACTURA DE VENTA").Bold().FontColor(EstiloPdf.Primario);
                            c.Item().AlignRight().Text("N.º " + v.Id.ToString("00000")).FontSize(12).Bold();
                            c.Item().AlignRight().Text(Formato.FechaHora(v.Fecha));
                        });
                    });

                    page.Content().PaddingTop(14).Column(col =>
                    {
                        if (!string.IsNullOrWhiteSpace(v.Observacion))
                            col.Item().PaddingBottom(8).Text(t => { t.Span("Observación: ").Bold(); t.Span(v.Observacion); });

                        col.Item().Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(4); c.RelativeColumn(1); c.RelativeColumn(2); c.RelativeColumn(2); });
                            t.Header(h =>
                            {
                                h.Cell().Element(EstiloPdf.Encabezado).Text("Producto").FontColor(Colors.White).Bold();
                                h.Cell().Element(EstiloPdf.Encabezado).AlignRight().Text("Cant.").FontColor(Colors.White).Bold();
                                h.Cell().Element(EstiloPdf.Encabezado).AlignRight().Text("Precio").FontColor(Colors.White).Bold();
                                h.Cell().Element(EstiloPdf.Encabezado).AlignRight().Text("Subtotal").FontColor(Colors.White).Bold();
                            });
                            foreach (DetalleVenta d in v.Detalles)
                            {
                                t.Cell().Element(EstiloPdf.Celda).Text(d.ProductoNombre);
                                t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(d.Cantidad.ToString());
                                t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(d.PrecioUnitario));
                                t.Cell().Element(EstiloPdf.Celda).AlignRight().Text(Formato.Moneda(d.Subtotal));
                            }
                        });

                        decimal total = 0m;
                        foreach (DetalleVenta d in v.Detalles) total += d.Subtotal;
                        col.Item().PaddingTop(8).AlignRight().Background(EstiloPdf.PrimarioClaro).Padding(8).Text(t =>
                        {
                            t.Span("TOTAL  ").Bold().FontSize(11);
                            t.Span(Formato.Moneda(total)).Bold().FontSize(13).FontColor(EstiloPdf.Primario);
                        });

                        col.Item().PaddingTop(20).AlignCenter().Text("¡Gracias por su compra!").Italic().FontColor(EstiloPdf.Gris);
                    });

                    page.Footer().Element(c => EstiloPdf.Pie(c, "Generado con SGAPE"));
                });
            }).GeneratePdf();
        }
    }
}
