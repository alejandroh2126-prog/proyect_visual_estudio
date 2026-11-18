using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Entidades;
using Logica.Servicios;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.ViewModels
{
    // =====================================================================
    // VACANTES Y POSTULACIONES
    // =====================================================================
    public partial class VacantesViewModel : CrudViewModel<Vacante>
    {
        public ObservableCollection<Postulacion> Postulaciones { get; } = new ObservableCollection<Postulacion>();

        [ObservableProperty] private Postulacion? _postulacionSeleccionada;

        public VacantesViewModel()
        {
            PropertyChanged += async (s, e) =>
            {
                if (e.PropertyName == nameof(Seleccionado)) await CargarPostulaciones();
            };
        }

        protected override string Nombre { get { return "vacante"; } }

        protected override Task<List<Vacante>> Consultar()
        {
            int emp = Sesion.IdEmprendimiento;
            return Fondo.Run(() => App.Servicios.Vacantes.Listar(emp));
        }

        protected override bool Coincide(Vacante v, string f) { return Formato.Contiene(v.Titulo, f) || Formato.Contiene(v.Cargo, f); }

        protected override string DescribirParaEliminar(Vacante v) { return "la vacante \"" + v.Titulo + "\" y sus " + v.Postulaciones + " postulaciones"; }

        protected override async Task<FormularioViewModel?> CrearFormulario(Vacante? v)
        {
            int emp = Sesion.IdEmprendimiento;
            List<Cargo> cargos = await Fondo.Run(() => App.Servicios.Nomina.Cargos(emp));
            if (cargos.Count == 0) throw new ReglaDeNegocioException("Primero crea el cargo en Nómina > Cargos.");
            List<Ciudad> ciudades = App.Servicios.Catalogos.Ciudades();

            FormularioViewModel f = new FormularioViewModel(v == null ? "Nueva vacante" : "Editar vacante", async form =>
            {
                Vacante datos = new Vacante
                {
                    Id = v?.Id ?? 0,
                    CargoId = form["cargo"].Elegido<Cargo>()?.Id ?? 0,
                    Titulo = form["titulo"].ValorTexto,
                    Descripcion = form["descripcion"].ValorTexto,
                    CiudadCodigo = form["ciudad"].Elegido<Ciudad>()?.Codigo ?? string.Empty,
                    FechaCierre = form["cierre"].Fecha
                };
                await Fondo.Run(() => App.Servicios.Vacantes.Guardar(emp, datos));
            });

            CampoFormulario titulo = new CampoFormulario("titulo", "Título de la vacante", TipoCampo.Texto) { MaxLongitud = 120, Texto = v?.Titulo ?? string.Empty };
            f.Agregar(new CampoFormulario("cargo", "Cargo", TipoCampo.Lista)
            {
                Opciones = cargos,
                Seleccion = v == null ? null : cargos.Find(c => c.Id == v.CargoId),
                AlSeleccionar = o => { if (o is Cargo c && titulo.ValorTexto.Length == 0) titulo.Texto = c.Nombre; }
            });
            f.Agregar(titulo);
            f.Agregar(new CampoFormulario("ciudad", "Ciudad", TipoCampo.ListaPredictiva)
            {
                Opciones = ciudades,
                Seleccion = ciudades.Find(c => c.Codigo == (v?.CiudadCodigo ?? Sesion.Emprendimiento?.CiudadCodigo))
            });
            f.Agregar(new CampoFormulario("descripcion", "Descripción (funciones, horario, requisitos)", TipoCampo.TextoLargo)
            {
                Requerido = false, MaxLongitud = 1000, Texto = v?.Descripcion ?? string.Empty
            });
            f.Agregar(new CampoFormulario("cierre", "Fecha de cierre", TipoCampo.Fecha)
            {
                Requerido = false, Fecha = v?.FechaCierre, Ayuda = "Opcional. Ese día la vacante se cierra sola."
            });
            return f;
        }

        protected override Task EliminarElemento(Vacante v)
        {
            int emp = Sesion.IdEmprendimiento;
            return Fondo.Run(() => App.Servicios.Vacantes.Eliminar(emp, v.Id));
        }

        private async Task CargarPostulaciones()
        {
            Postulaciones.Clear();
            if (Seleccionado == null) return;
            int emp = Sesion.IdEmprendimiento;
            int vacante = Seleccionado.Id;
            await Ejecutar(async () =>
            {
                foreach (Postulacion p in await Fondo.Run(() => App.Servicios.Vacantes.Postulaciones(emp, vacante))) Postulaciones.Add(p);
            });
        }

        [RelayCommand]
        private async Task AbrirCerrar()
        {
            if (Seleccionado == null) { Notificador.Error("Selecciona una vacante."); return; }
            int emp = Sesion.IdEmprendimiento;
            int id = Seleccionado.Id;
            await Ejecutar(() => Fondo.Run(() => App.Servicios.Vacantes.AbrirOCerrar(emp, id)));
            await Recargar();
        }

        [RelayCommand]
        private async Task VerHojaDeVida()
        {
            if (PostulacionSeleccionada == null) { Notificador.Error("Selecciona una postulación."); return; }
            Postulacion p = PostulacionSeleccionada;
            int emp = Sesion.IdEmprendimiento;
            await Ejecutar(async () =>
            {
                ArchivoCv cv = await Fondo.Run(() => App.Servicios.Vacantes.HojaDeVida(emp, p.Id));
                string filtro = cv.Tipo == ServicioVacantes.TipoPdf ? "PDF (*.pdf)|*.pdf" : "Word (*.docx)|*.docx";
                string? ruta = Archivos.GuardarComo(cv.Nombre, cv.Contenido, filtro);
                if (ruta != null) Archivos.Abrir(ruta);
            });
            await CargarPostulaciones();
        }

        [RelayCommand]
        private async Task Rechazar()
        {
            if (PostulacionSeleccionada == null) { Notificador.Error("Selecciona una postulación."); return; }
            Postulacion p = PostulacionSeleccionada;
            if (!await Notificador.Confirmar("Rechazar postulación", "¿Rechazar la postulación de " + p.NombreCompleto + "?", "Rechazar")) return;
            int emp = Sesion.IdEmprendimiento;
            await Ejecutar(() => Fondo.Run(() => App.Servicios.Vacantes.Rechazar(emp, p.Id)));
            await CargarPostulaciones();
        }

        [RelayCommand]
        private async Task Contratar()
        {
            if (PostulacionSeleccionada == null) { Notificador.Error("Selecciona una postulación."); return; }
            Postulacion p = PostulacionSeleccionada;
            int emp = Sesion.IdEmprendimiento;
            FormularioViewModel f = new FormularioViewModel("Contratar a " + p.NombreCompleto, async form =>
            {
                DateTime fecha = form["fecha"].Fecha ?? DateTime.Today;
                await Fondo.Run(() => App.Servicios.Vacantes.Contratar(emp, p.Id, fecha));
            }) { TextoBoton = "Contratar" };
            f.Agregar(new CampoFormulario("fecha", "Fecha de ingreso", TipoCampo.Fecha)
            {
                Fecha = DateTime.Today,
                Ayuda = "Se crea como trabajador activo con el cargo de la vacante."
            });
            if (await Notificador.Formulario(f))
            {
                Notificador.Exito(p.NombreCompleto + " ya aparece en Nómina > Trabajadores.");
                await CargarPostulaciones();
            }
        }
    }

    // =====================================================================
    // INVENTARIO
    // =====================================================================
    public partial class InventarioViewModel : CrudViewModel<Producto>
    {
        [ObservableProperty] private int _conStockBajo;
        [ObservableProperty] private decimal _valorInventario;

        protected override string Nombre { get { return "producto"; } }

        private const string SinMovimiento = "Sin cambio";
        private const string MovEntrada = "Entrada (compra o llegada de mercancía)";
        private const string MovSalida = "Salida (daño, vencimiento o uso)";

        protected override Task<List<Producto>> Consultar()
        {
            int emp = Sesion.IdEmprendimiento;
            return Fondo.Run(() => App.Servicios.Inventario.Listar(emp));
        }

        protected override Task DespuesDeCargar()
        {
            int bajos = 0;
            decimal valor = 0m;
            foreach (Producto p in Elementos) { if (p.StockBajo) bajos++; valor += p.ValorInventario; }
            ConStockBajo = bajos;
            ValorInventario = valor;
            return Task.CompletedTask;
        }

        protected override bool Coincide(Producto p, string f) { return Formato.Contiene(p.Nombre, f) || Formato.Contiene(p.Categoria, f); }

        protected override async Task<FormularioViewModel?> CrearFormulario(Producto? p)
        {
            int emp = Sesion.IdEmprendimiento;
            List<string> categorias = await Fondo.Run(() => App.Servicios.Catalogos.CategoriasDeProducto(emp));

            FormularioViewModel f = new FormularioViewModel(p == null ? "Nuevo producto" : "Editar producto", async form =>
            {
                Producto datos = new Producto
                {
                    Id = p?.Id ?? 0,
                    Nombre = form["nombre"].ValorTexto,
                    Categoria = form["categoria"].ValorTexto,
                    PrecioCompra = form["compra"].ValorMoneda,
                    PrecioVenta = form["venta"].ValorMoneda,
                    StockMinimo = form["minimo"].ValorEntero,
                    Stock = p == null ? form["stock"].ValorEntero : p.Stock
                };

                // Al editar: movimiento de stock opcional (antes eran los botones Entrada y Salida)
                string? movimiento = p == null ? null : form["movimiento"].Seleccion as string;
                int cantidad = p == null ? 0 : form["cantidad"].ValorEntero;
                bool mueve = movimiento != null && movimiento != SinMovimiento;
                if (mueve && cantidad <= 0)
                    throw new ReglaDeNegocioException("Escribe cuántas unidades entran o salen.");
                if (!mueve && cantidad > 0)
                    throw new ReglaDeNegocioException("Elige si las " + cantidad + " unidades son una entrada o una salida.");
                if (mueve && movimiento == MovSalida && cantidad > p!.Stock)
                    throw new ReglaDeNegocioException("No puedes sacar " + cantidad + " unidades: solo hay " + p.Stock + ".");

                await Fondo.Run(() => App.Servicios.Inventario.Guardar(emp, datos));
                if (mueve)
                {
                    int id = p!.Id;
                    // sp_sg_mover_stock: la entrada registra la compra como gasto en Finanzas
                    if (movimiento == MovEntrada) await Fondo.Run(() => App.Servicios.Inventario.Entrada(emp, id, cantidad));
                    else await Fondo.Run(() => App.Servicios.Inventario.Salida(emp, id, cantidad));
                }
                string? advertencia = ServicioInventario.Advertencia(datos);
                if (advertencia != null) Notificador.Error(advertencia);
            });

            f.Agregar(new CampoFormulario("nombre", "Nombre", TipoCampo.Texto) { MaxLongitud = 150, Texto = p?.Nombre ?? string.Empty });
            f.Agregar(new CampoFormulario("categoria", "Categoría", TipoCampo.TextoPredictivo)
            {
                Requerido = false, MaxLongitud = 100, Opciones = categorias, Texto = p?.Categoria ?? string.Empty,
                Ayuda = "Escribe o elige una de las que ya usas."
            });
            f.Agregar(new CampoFormulario("compra", "Precio de compra", TipoCampo.Moneda) { MaxLongitud = 15, Texto = p == null ? string.Empty : Math.Round(p.PrecioCompra, 0).ToString() });
            f.Agregar(new CampoFormulario("venta", "Precio de venta", TipoCampo.Moneda) { MaxLongitud = 15, Texto = p == null ? string.Empty : Math.Round(p.PrecioVenta, 0).ToString() });
            if (p == null)
                f.Agregar(new CampoFormulario("stock", "Stock inicial", TipoCampo.Entero) { MaxLongitud = 7, Texto = "0", Ayuda = "Después cambia con Editar (entrada o salida) o con las ventas." });
            f.Agregar(new CampoFormulario("minimo", "Stock mínimo (alerta)", TipoCampo.Entero) { MaxLongitud = 7, Texto = (p?.StockMinimo ?? 0).ToString() });
            if (p != null)
            {
                f.Agregar(new CampoFormulario("movimiento", "Movimiento de stock", TipoCampo.Lista)
                {
                    Requerido = false,
                    Opciones = new[] { SinMovimiento, MovEntrada, MovSalida },
                    Seleccion = SinMovimiento,
                    Ayuda = "Stock actual: " + p.Stock + ". La entrada se registra como compra en Finanzas (" + Formato.Moneda(p.PrecioCompra) + " c/u)."
                });
                f.Agregar(new CampoFormulario("cantidad", "Cantidad", TipoCampo.Entero) { Requerido = false, MaxLongitud = 7, Texto = string.Empty });
            }
            return f;
        }

        protected override Task EliminarElemento(Producto p)
        {
            int emp = Sesion.IdEmprendimiento;
            return Fondo.Run(() => App.Servicios.Inventario.Eliminar(emp, p.Id));
        }

        [RelayCommand]
        private async Task AjustarPrecios()
        {
            int emp = Sesion.IdEmprendimiento;
            int afectados = 0;
            FormularioViewModel f = new FormularioViewModel("Ajustar precios de venta", async form =>
            {
                decimal porcentaje = form["porcentaje"].ValorDecimal;
                afectados = await Fondo.Run(() => App.Servicios.Inventario.AjustarPrecios(emp, porcentaje));
            }) { TextoBoton = "Aplicar" };
            f.Agregar(new CampoFormulario("porcentaje", "Porcentaje", TipoCampo.Decimal)
            {
                MaxLongitud = 6,
                PermiteNegativos = true,
                Ayuda = "5 sube 5 %, -10 baja 10 %. Cada cambio queda en la auditoría de precios."
            });
            if (await Notificador.Formulario(f))
            {
                Notificador.Exito("Precios ajustados en " + afectados + " productos.");
                await Recargar();
            }
        }
    }

    // =====================================================================
    // VENTAS
    // =====================================================================
    public partial class LineaCarrito : ObservableObject
    {
        public int ProductoId { get; init; }
        public string Nombre { get; init; } = string.Empty;
        public decimal Precio { get; init; }
        [ObservableProperty] [NotifyPropertyChangedFor(nameof(Subtotal))] private int _cantidad;
        public decimal Subtotal { get { return Precio * Cantidad; } }
    }

    public partial class VentasViewModel : ViewModelBase
    {
        public ObservableCollection<Producto> Productos { get; } = new ObservableCollection<Producto>();
        public ObservableCollection<LineaCarrito> Carrito { get; } = new ObservableCollection<LineaCarrito>();
        public ObservableCollection<Venta> Historial { get; } = new ObservableCollection<Venta>();

        [ObservableProperty] private Producto? _productoElegido;
        [ObservableProperty] private string _cantidad = "1";
        [ObservableProperty] private string _observacion = string.Empty;
        [ObservableProperty] private decimal _total;
        [ObservableProperty] private Venta? _ventaSeleccionada;
        [ObservableProperty] private Venta? _detalle;
        [ObservableProperty] private ResumenVentas? _resumen;

        public override async Task Cargar()
        {
            await Ejecutar(async () =>
            {
                int emp = Sesion.IdEmprendimiento;
                List<Producto> productos = await Fondo.Run(() => App.Servicios.Inventario.Listar(emp));
                Productos.Clear();
                foreach (Producto p in productos) if (p.Stock > 0) Productos.Add(p);
                List<Venta> ventas = await Fondo.Run(() => App.Servicios.Ventas.Listar(emp));
                Historial.Clear();
                foreach (Venta v in ventas) Historial.Add(v);
                Resumen = await Fondo.Run(() => App.Servicios.Ventas.Resumen(emp));
            });
        }

        [RelayCommand]
        private void Agregar()
        {
            if (ProductoElegido == null) { Notificador.Error("Busca y elige un producto de la lista."); return; }
            if (!int.TryParse(Cantidad, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) || n <= 0)
            {
                Notificador.Error("La cantidad debe ser un número entero mayor que cero.");
                return;
            }

            LineaCarrito? existente = null;
            foreach (LineaCarrito l in Carrito) if (l.ProductoId == ProductoElegido.Id) existente = l;
            int enCarrito = existente?.Cantidad ?? 0;
            if (enCarrito + n > ProductoElegido.Stock)
            {
                Notificador.Error("Solo hay " + ProductoElegido.Stock + " unidades de " + ProductoElegido.Nombre + ".");
                return;
            }

            if (existente != null) existente.Cantidad += n;
            else Carrito.Add(new LineaCarrito { ProductoId = ProductoElegido.Id, Nombre = ProductoElegido.Nombre, Precio = ProductoElegido.PrecioVenta, Cantidad = n });
            ProductoElegido = null;
            Cantidad = "1";
            CalcularTotal();
        }

        [RelayCommand]
        private void Quitar(LineaCarrito? linea)
        {
            if (linea == null) return;
            Carrito.Remove(linea);
            CalcularTotal();
        }

        [RelayCommand]
        private void Vaciar()
        {
            Carrito.Clear();
            Observacion = string.Empty;
            CalcularTotal();
        }

        private void CalcularTotal()
        {
            decimal t = 0m;
            foreach (LineaCarrito l in Carrito) t += l.Subtotal;
            Total = t;
        }

        /// <summary>sp_sg_registrar_venta: todo o nada (cabecera, detalle, stock e ingreso).</summary>
        [RelayCommand]
        private async Task Registrar()
        {
            if (Carrito.Count == 0) { Notificador.Error("Agrega al menos un producto."); return; }
            List<DetalleVenta> detalles = new List<DetalleVenta>();
            foreach (LineaCarrito l in Carrito) detalles.Add(new DetalleVenta { ProductoId = l.ProductoId, Cantidad = l.Cantidad });
            int emp = Sesion.IdEmprendimiento;
            string obs = Observacion;
            Venta? venta = null;
            await Ejecutar(async () => { venta = await Fondo.Run(() => App.Servicios.Ventas.Registrar(emp, obs, detalles)); });
            if (venta == null) return;

            Notificador.Exito("Venta #" + venta.Id + " registrada por " + Formato.Moneda(venta.Total) + ".");
            Vaciar();
            await Cargar();
            if (await Notificador.Confirmar("Venta registrada", "¿Quieres guardar la factura en PDF?", "Guardar factura"))
                await GuardarFactura(venta);
        }

        partial void OnVentaSeleccionadaChanged(Venta? value)
        {
            _ = CargarDetalle(value);
        }

        private async Task CargarDetalle(Venta? v)
        {
            Detalle = null;
            if (v == null) return;
            int emp = Sesion.IdEmprendimiento;
            await Ejecutar(async () => { Detalle = await Fondo.Run(() => App.Servicios.Ventas.Obtener(emp, v.Id)); });
        }

        [RelayCommand]
        private async Task Factura()
        {
            if (Detalle == null) { Notificador.Error("Selecciona una venta del historial."); return; }
            await GuardarFactura(Detalle);
        }

        private async Task GuardarFactura(Venta venta)
        {
            Emprendimiento e = Sesion.Emprendimiento!;
            await Ejecutar(async () =>
            {
                byte[] pdf = await Fondo.Run(() => App.Servicios.Reportes.FacturaPdf(e, venta));
                string? ruta = Archivos.GuardarComo(ServicioReportes.NombreArchivoFactura(venta), pdf, "PDF (*.pdf)|*.pdf");
                if (ruta != null) Archivos.Abrir(ruta);
            });
        }
    }

    // =====================================================================
    // FINANZAS
    // =====================================================================
    public partial class FinanzasViewModel : ViewModelBase
    {
        private readonly List<Movimiento> _todos = new List<Movimiento>();

        public ObservableCollection<Movimiento> Movimientos { get; } = new ObservableCollection<Movimiento>();
        public string[] Filtros { get; } = { "Todos", "Ingresos", "Gastos" };

        [ObservableProperty] private ResumenFinanciero? _resumen;
        [ObservableProperty] private Movimiento? _seleccionado;
        [ObservableProperty] private string _filtro = "Todos";

        /// <summary>
        /// Presupuestos vive DENTRO de Finanzas (pestaña): los movimientos son lo que ya pasó y el presupuesto
        /// es el límite que te pones por categoría de gasto. Cada gasto que registras aquí mueve su presupuesto.
        /// </summary>
        public PresupuestosViewModel Presupuestos { get; } = new PresupuestosViewModel();

        public override async Task Cargar()
        {
            await Ejecutar(async () =>
            {
                int emp = Sesion.IdEmprendimiento;
                Resumen = await Fondo.Run(() => App.Servicios.Finanzas.Resumen(emp));
                List<Movimiento> lista = await Fondo.Run(() => App.Servicios.Finanzas.Listar(emp));
                _todos.Clear();
                _todos.AddRange(lista);
                Filtrar();
            });
            await Presupuestos.Cargar();
        }

        partial void OnFiltroChanged(string value) { Filtrar(); }

        private void Filtrar()
        {
            Movimientos.Clear();
            foreach (Movimiento m in _todos)
                if (Filtro == "Todos" || (Filtro == "Ingresos" && m.EsIngreso) || (Filtro == "Gastos" && !m.EsIngreso))
                    Movimientos.Add(m);
        }

        [RelayCommand]
        private Task NuevoIngreso() { return Nuevo(true); }

        [RelayCommand]
        private Task NuevoGasto() { return Nuevo(false); }

        private async Task Nuevo(bool ingreso)
        {
            int emp = Sesion.IdEmprendimiento;
            List<CategoriaMovimiento> categorias = App.Servicios.Catalogos.CategoriasManuales(
                ingreso ? CategoriaMovimiento.TipoIngreso : CategoriaMovimiento.TipoGasto);

            FormularioViewModel f = new FormularioViewModel(ingreso ? "Nuevo ingreso" : "Nuevo gasto", async form =>
            {
                int categoria = form["categoria"].Elegido<CategoriaMovimiento>()?.Id ?? 0;
                string descripcion = form["descripcion"].ValorTexto;
                decimal monto = form["monto"].ValorMoneda;
                DateTime fecha = form["fecha"].Fecha ?? DateTime.Today;
                await Fondo.Run(() => App.Servicios.Finanzas.Registrar(emp, ingreso, categoria, descripcion, monto, fecha));
            });
            f.Agregar(new CampoFormulario("categoria", "Categoría", TipoCampo.Lista) { Opciones = categorias });
            f.Agregar(new CampoFormulario("monto", "Monto", TipoCampo.Moneda) { MaxLongitud = 15 });
            f.Agregar(new CampoFormulario("fecha", "Fecha", TipoCampo.Fecha) { Fecha = DateTime.Today, Ayuda = "No puede ser futura." });
            f.Agregar(new CampoFormulario("descripcion", "Descripción", TipoCampo.Texto) { Requerido = false, MaxLongitud = 300 });

            if (await Notificador.Formulario(f))
            {
                Notificador.Exito(ingreso ? "Ingreso registrado." : "Gasto registrado.");
                await Cargar();
            }
        }

        [RelayCommand]
        private async Task Eliminar()
        {
            if (Seleccionado == null) { Notificador.Error("Selecciona un movimiento."); return; }
            Movimiento m = Seleccionado;
            if (!m.EsManual) { Notificador.Error("Este movimiento lo creó una venta o el inventario; no se elimina a mano."); return; }
            if (!await Notificador.Confirmar("Eliminar movimiento", "¿Eliminar " + m.Categoria + " por " + Formato.Moneda(m.Monto) + "?", "Eliminar")) return;
            int emp = Sesion.IdEmprendimiento;
            await Ejecutar(() => Fondo.Run(() => App.Servicios.Finanzas.Eliminar(emp, m.Id)));
            await Cargar();
        }
    }

    // =====================================================================
    // PRESUPUESTOS (la misma ventana generica crea y edita)
    // =====================================================================
    public partial class PresupuestosViewModel : CrudViewModel<Presupuesto>
    {
        public List<string> Meses { get; }

        [ObservableProperty] private string _mes = Validar.MesActual();

        public PresupuestosViewModel()
        {
            Meses = new List<string>();
            DateTime inicio = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            for (int i = -6; i <= 3; i++) Meses.Add(inicio.AddMonths(i).ToString("yyyy-MM", CultureInfo.InvariantCulture));
        }

        partial void OnMesChanged(string value) { _ = Recargar(); }

        protected override string Nombre { get { return "presupuesto"; } }

        protected override Task<List<Presupuesto>> Consultar()
        {
            int emp = Sesion.IdEmprendimiento;
            string mes = Mes;
            return Fondo.Run(() => App.Servicios.Presupuestos.Listar(emp, mes));
        }

        protected override bool Coincide(Presupuesto p, string f) { return Formato.Contiene(p.Categoria, f); }

        protected override string DescribirParaEliminar(Presupuesto p) { return "el presupuesto de " + p.Categoria + " de " + Formato.NombreMes(p.Mes); }

        protected override Task<FormularioViewModel?> CrearFormulario(Presupuesto? p)
        {
            int emp = Sesion.IdEmprendimiento;
            string mes = Mes;
            List<CategoriaMovimiento> disponibles = App.Servicios.Catalogos.CategoriasDeGasto();
            if (p == null) disponibles = disponibles.FindAll(c => !Elementos_TieneCategoria(c.Id));

            FormularioViewModel f = new FormularioViewModel(
                p == null ? "Nuevo presupuesto de " + Formato.NombreMes(mes) : "Editar presupuesto de " + p.Categoria,
                async form =>
                {
                    int categoria = p?.CategoriaId ?? (form["categoria"].Elegido<CategoriaMovimiento>()?.Id ?? 0);
                    decimal limite = form["limite"].ValorMoneda;
                    await Fondo.Run(() => App.Servicios.Presupuestos.Guardar(emp, p?.Id ?? 0, categoria, limite, mes));
                });

            if (p == null)
                f.Agregar(new CampoFormulario("categoria", "Categoría de gasto", TipoCampo.Lista)
                {
                    Opciones = disponibles,
                    Ayuda = "Solo aparecen las categorías que aún no tienen presupuesto este mes."
                });
            f.Agregar(new CampoFormulario("limite", "Límite mensual", TipoCampo.Moneda)
            {
                MaxLongitud = 15,
                Texto = p == null ? string.Empty : Math.Round(p.MontoLimite, 0).ToString(),
                Ayuda = "Recibirás alerta al llegar al 80 %."
            });
            return Task.FromResult<FormularioViewModel?>(f);
        }

        private bool Elementos_TieneCategoria(int categoriaId)
        {
            foreach (Presupuesto x in Elementos) if (x.CategoriaId == categoriaId) return true;
            return false;
        }

        protected override Task EliminarElemento(Presupuesto p)
        {
            int emp = Sesion.IdEmprendimiento;
            return Fondo.Run(() => App.Servicios.Presupuestos.Eliminar(emp, p.Id));
        }
    }

    // =====================================================================
    // REPORTES (PDF)
    // =====================================================================
    public partial class ReportesViewModel : ViewModelBase
    {
        [ObservableProperty] private DatosReporte? _datos;

        public override async Task Cargar()
        {
            await Ejecutar(async () =>
            {
                Emprendimiento e = Sesion.Emprendimiento!;
                Datos = await Fondo.Run(() => App.Servicios.Reportes.Reunir(e));
            });
        }

        [RelayCommand]
        private async Task GenerarPdf()
        {
            Emprendimiento e = Sesion.Emprendimiento!;
            await Ejecutar(async () =>
            {
                byte[] pdf = await Fondo.Run(() => App.Servicios.Reportes.ReportePdf(e));
                string? ruta = Archivos.GuardarComo(ServicioReportes.NombreArchivoReporte(e), pdf, "PDF (*.pdf)|*.pdf");
                if (ruta != null) Archivos.Abrir(ruta);
            });
        }
    }

    // =====================================================================
    // TELEGRAM (vincular el bot con la cuenta)
    // =====================================================================
    public partial class TelegramViewModel : ViewModelBase
    {
        [ObservableProperty] private string? _codigo;
        [ObservableProperty] private string? _vence;
        [ObservableProperty] private int _chatsVinculados;

        public override async Task Cargar()
        {
            await Ejecutar(async () =>
            {
                int usuario = Sesion.IdUsuario;
                ChatsVinculados = await Fondo.Run(() => App.Servicios.Telegram.ChatsVinculados(usuario));
            });
        }

        [RelayCommand]
        private async Task GenerarCodigo()
        {
            await Ejecutar(async () =>
            {
                int usuario = Sesion.IdUsuario;
                Codigo = await Fondo.Run(() => App.Servicios.Telegram.GenerarCodigo(usuario));
                Vence = "Vence a las " + DateTime.Now.AddMinutes(10).ToString("HH:mm") + ". Escríbele al bot: /vincular " + Codigo;
            });
        }

        [RelayCommand]
        private void Copiar()
        {
            if (string.IsNullOrEmpty(Codigo)) return;
            try { Clipboard.SetText("/vincular " + Codigo); Notificador.Exito("Copiado. Pégalo en el chat del bot."); }
            catch (Exception) { Notificador.Error("No se pudo copiar. Escribe el código a mano."); }
        }
    }
}
