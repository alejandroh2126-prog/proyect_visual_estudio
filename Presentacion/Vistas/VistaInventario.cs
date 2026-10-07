using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Entidades;
using Logica;
using Logica.Utilidades;
using Presentacion.Comun;
using Presentacion.Dialogos;

namespace Presentacion.Vistas
{
	/// <summary>Productos, stock y alertas de stock bajo.</summary>
	public class VistaInventario : VistaBase
	{
		private readonly TarjetaKpi _kProductos = new TarjetaKpi("Productos", Tema.VerdeOscuro);
		private readonly TarjetaKpi _kBajo = new TarjetaKpi("Con stock bajo", Tema.Rojo);
		private readonly TarjetaKpi _kValor = new TarjetaKpi("Valor del inventario (a costo)", Tema.Gris);
		private readonly DataGridView _tabla = Tema.Tabla("Producto", "Categoría", "Stock", "Mínimo", "P. compra", "P. venta", "Margen");

		public VistaInventario(FabricaServicios fabrica) : base(fabrica)
		{
			_tabla.AlinearDerecha(2, 3, 4, 5, 6);

			Button nuevo = Tema.Boton("Nuevo producto", Tema.Verde);
			Button editar = Tema.Boton("Editar", Tema.Gris);
			Button entrada = Tema.Boton("+ Entrada de stock", Tema.VerdeOscuro);
			Button salida = Tema.Boton("- Salida manual", Tema.Dorado);
			Button eliminar = Tema.Boton("Eliminar", Tema.Rojo);
			nuevo.Click += delegate { Nuevo(); };
			editar.Click += delegate { Editar(); };
			entrada.Click += delegate { Mover(true); };
			salida.Click += delegate { Mover(false); };
			eliminar.Click += delegate { Eliminar(); };
			_tabla.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) Editar(); };

			TableLayoutPanel raiz = Tema.ColumnaVertical();
			raiz.Fila(Tema.TituloVista("Inventario"), false);
			raiz.Fila(Tema.FilaTarjetas(_kProductos, _kBajo, _kValor), false);
			raiz.Fila(Tema.Barra(nuevo, editar, entrada, salida, eliminar), false);
			raiz.Fila(_tabla, true);
			Controls.Add(raiz);
		}

		protected override void Recargar()
		{
			_tabla.Rows.Clear();
			_kProductos.Valor = "0";
			_kBajo.Valor = "0";
			_kValor.Valor = Formato.Moneda(0);
			if (Emprendimiento == null) return;

			int id = Emprendimiento.Id;
			Mensajes.Ejecutar(delegate
			{
				int bajos = 0;
				decimal valor = 0m;
				List<Producto> productos = Fabrica.Inventario.Listar(id);
				foreach (Producto p in productos)
				{
					DataGridViewRow fila = _tabla.AgregarFila(p, p.Nombre, p.Categoria, p.Stock, p.StockMinimo,
						Formato.Moneda(p.PrecioCompra), Formato.Moneda(p.PrecioVenta),
						p.PorcentajeMargen.ToString("0.0") + "%");
					valor += p.Stock * p.PrecioCompra;
					if (p.TieneStockBajo)
					{
						bajos++;
						fila.DefaultCellStyle.ForeColor = Tema.Rojo;
						fila.DefaultCellStyle.BackColor = Tema.RojoClaro;
					}
				}
				_kProductos.Valor = productos.Count.ToString();
				_kBajo.Valor = bajos.ToString();
				_kValor.Valor = Formato.Moneda(valor);
			});
		}

		private void Nuevo()
		{
			if (!ExigirEmprendimiento()) return;
			using (DialogoProducto d = new DialogoProducto(Fabrica, Emprendimiento!.Id, null))
			{
				if (d.ShowDialog(this) == DialogResult.OK) Recargar();
			}
		}

		private void Editar()
		{
			if (!ExigirEmprendimiento()) return;
			Producto? p = _tabla.Seleccionado<Producto>();
			if (p == null) { Mensajes.Info("Selecciona un producto de la lista."); return; }
			using (DialogoProducto d = new DialogoProducto(Fabrica, Emprendimiento!.Id, p))
			{
				if (d.ShowDialog(this) == DialogResult.OK) Recargar();
			}
		}

		private void Mover(bool esEntrada)
		{
			Producto? p = _tabla.Seleccionado<Producto>();
			if (p == null) { Mensajes.Info("Selecciona un producto de la lista."); return; }

			string titulo = esEntrada ? "Entrada de stock" : "Salida manual de stock";
			string detalle = p.Nombre + " (stock actual: " + p.Stock + ")";
			Action<int> accion;
			if (esEntrada) accion = delegate (int cantidad) { Fabrica.Inventario.EntradaStock(p.Id, cantidad); };
			else accion = delegate (int cantidad) { Fabrica.Inventario.SalidaManual(p.Id, cantidad); };

			using (DialogoCantidad d = new DialogoCantidad(titulo, detalle, accion))
			{
				if (d.ShowDialog(this) == DialogResult.OK)
				{
					Recargar();
					AvisarStockBajo(p.Id);
				}
			}
		}

		private void AvisarStockBajo(int productoId)
		{
			foreach (Producto p in Fabrica.Inventario.Listar(Emprendimiento!.Id))
			{
				if (p.Id == productoId && p.TieneStockBajo)
					Mensajes.Info("Atención: '" + p.Nombre + "' quedó con stock bajo (" + p.Stock + ", mínimo " + p.StockMinimo + ").");
			}
		}

		private void Eliminar()
		{
			Producto? p = _tabla.Seleccionado<Producto>();
			if (p == null) { Mensajes.Info("Selecciona un producto de la lista."); return; }
			if (!Mensajes.Confirmar("¿Eliminar el producto '" + p.Nombre + "'?")) return;
			if (Mensajes.Ejecutar(delegate { Fabrica.Inventario.Eliminar(p.Id); })) Recargar();
		}
	}
}