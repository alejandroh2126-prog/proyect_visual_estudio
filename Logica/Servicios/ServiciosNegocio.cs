using System;
using System.Collections.Generic;
using Datos;
using Entidades;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>UNA SOLA MISION: cargos, trabajadores y nomina (los calculos los hace pkg_sg_nomina).</summary>
    public class ServicioNomina
    {
        private readonly ICargoDao _cargos;
        private readonly ITrabajadorDao _trabajadores;
        private readonly IReporteDao _reportes;
        private readonly ServicioCatalogos _catalogos;

        public ServicioNomina(ICargoDao cargos, ITrabajadorDao trabajadores, IReporteDao reportes, ServicioCatalogos catalogos)
        {
            _cargos = cargos;
            _trabajadores = trabajadores;
            _reportes = reportes;
            _catalogos = catalogos;
        }

        public List<Cargo> Cargos(int emprendimientoId) { return _cargos.ListarPorEmprendimiento(emprendimientoId); }
        public List<Trabajador> Trabajadores(int emprendimientoId) { return _trabajadores.ListarPorEmprendimiento(emprendimientoId); }
        public ResumenNomina Resumen(int emprendimientoId) { return _reportes.Nomina(emprendimientoId); }
        public List<FilaNomina> Detalle(int emprendimientoId) { return _reportes.DetalleNomina(emprendimientoId); }

        /// <summary>Mensaje si el salario no alcanza el minimo legal para el horario; null si esta bien.</summary>
        public static string? ErrorSalario(decimal salario, string? tipoHorario)
        {
            decimal minimo = SalarioMinimo.Para(tipoHorario);
            if (salario < minimo)
                return "Para " + (tipoHorario ?? Horarios.TiempoCompleto).ToLowerInvariant() + " el salario mínimo es " + Formato.Moneda(minimo) +
                       (minimo < SalarioMinimo.Smmlv2026 ? " (medio salario mínimo)." : " (salario mínimo 2026).");
            if (salario > 999_999_999m) return "El salario es demasiado alto.";
            return null;
        }

        public Cargo GuardarCargo(int emprendimientoId, Cargo c)
        {
            c.Descripcion = Validar.Opcional(c.Descripcion, "Descripción", 300);
            if (Array.IndexOf(Horarios.Todos, c.TipoHorario) < 0)
                throw new ReglaDeNegocioException("Elige el tipo de horario: tiempo completo, medio tiempo o por turnos.");
            string? errorSalario = ErrorSalario(c.SalarioBase, c.TipoHorario);
            if (errorSalario != null) throw new ReglaDeNegocioException(errorSalario);

            if (c.Id != 0)
            {
                // Editar: el nombre del cargo no cambia (los trabajadores y la nomina ya lo usan)
                Cargo? actual = _cargos.ObtenerPorId(c.Id);
                Propio(actual?.EmprendimientoId, emprendimientoId, "Cargo");
                c.Nombre = actual!.Nombre;
                c.EmprendimientoId = emprendimientoId;
                _cargos.Actualizar(c);
                return c;
            }

            c.Nombre = Validar.Requerido(c.Nombre, "Nombre del cargo", 100);

            foreach (Cargo otro in _cargos.ListarPorEmprendimiento(emprendimientoId))
                if (otro.Id != c.Id && string.Equals(otro.Nombre, c.Nombre, StringComparison.OrdinalIgnoreCase))
                    throw new ReglaDeNegocioException("Ya existe el cargo '" + otro.Nombre + "'.");

            c.EmprendimientoId = emprendimientoId;
            return _cargos.Insertar(c);
        }

        public void EliminarCargo(int emprendimientoId, int cargoId)
        {
            Cargo? c = _cargos.ObtenerPorId(cargoId);
            Propio(c?.EmprendimientoId, emprendimientoId, "Cargo");
            if (c!.TrabajadoresActivos > 0)
                throw new ReglaDeNegocioException("No se puede eliminar: el cargo tiene " + c.TrabajadoresActivos + " trabajador(es) activo(s).");
            _cargos.Eliminar(cargoId);
        }

        public Trabajador GuardarTrabajador(int emprendimientoId, Trabajador t)
        {
            t.Nombre = Validar.NombrePersona(t.Nombre, "Nombre");
            t.Apellido = Validar.NombrePersona(t.Apellido, "Apellido");
            t.NumeroDocumento = Validar.Documento(_catalogos.TipoDocumento(t.TipoDocumento), t.NumeroDocumento);
            t.Telefono = Validar.Telefono(t.Telefono, false);
            t.Email = Validar.Correo(t.Email, false);
            Validar.FechaNoFutura(t.FechaIngreso, "Fecha de ingreso");
            Validar.Seleccionado(t.CargoId, "Cargo");

            Cargo? cargo = _cargos.ObtenerPorId(t.CargoId);
            Propio(cargo?.EmprendimientoId, emprendimientoId, "Cargo");

            // El mismo numero no se repite aunque el tipo sea distinto (CC 123 y CE 123)
            Trabajador? repetido = DocumentoRepetido(_trabajadores.ListarPorEmprendimiento(emprendimientoId), t.Id, t.NumeroDocumento);
            if (repetido != null)
                throw new ReglaDeNegocioException("El número " + t.NumeroDocumento + " ya es de " + repetido.NombreCompleto +
                                                  " (" + repetido.TipoDocumento + "). No se puede repetir aunque el tipo de documento sea otro.");

            t.EmprendimientoId = emprendimientoId;
            if (t.Id == 0) return _trabajadores.Insertar(t);
            Propio(_trabajadores.ObtenerPorId(t.Id)?.EmprendimientoId, emprendimientoId, "Trabajador");
            _trabajadores.Actualizar(t);
            return t;
        }

        /// <summary>Trabajador que ya tiene ese numero de documento (sin importar el tipo), o null.</summary>
        public static Trabajador? DocumentoRepetido(IEnumerable<Trabajador> trabajadores, int idActual, string numero)
        {
            string n = (numero ?? string.Empty).Replace(".", "").Replace(" ", "").Trim().ToUpperInvariant();
            if (n.Length == 0) return null;
            foreach (Trabajador otro in trabajadores)
                if (otro.Id != idActual && string.Equals(otro.NumeroDocumento, n, StringComparison.OrdinalIgnoreCase)) return otro;
            return null;
        }

        public void CambiarEstado(int emprendimientoId, int trabajadorId)
        {
            Trabajador? t = _trabajadores.ObtenerPorId(trabajadorId);
            Propio(t?.EmprendimientoId, emprendimientoId, "Trabajador");
            _trabajadores.CambiarEstado(trabajadorId, t!.EstaActivo ? Estados.Inactivo : Estados.Activo);
        }

        public void EliminarTrabajador(int emprendimientoId, int trabajadorId)
        {
            Propio(_trabajadores.ObtenerPorId(trabajadorId)?.EmprendimientoId, emprendimientoId, "Trabajador");
            _trabajadores.Eliminar(trabajadorId);
        }

        internal static void Propio(int? emprendimientoDelRegistro, int emprendimientoId, string que)
        {
            if (emprendimientoDelRegistro == null || emprendimientoDelRegistro.Value != emprendimientoId)
                throw new ReglaDeNegocioException(que + " no encontrado.");
        }
    }

    /// <summary>UNA SOLA MISION: productos y stock (los movimientos de stock los hace sp_sg_mover_stock).</summary>
    public class ServicioInventario
    {
        private readonly IProductoDao _productos;

        public ServicioInventario(IProductoDao productos)
        {
            _productos = productos;
        }

        public List<Producto> Listar(int emprendimientoId) { return _productos.ListarPorEmprendimiento(emprendimientoId); }
        public List<ProductoStockBajo> StockBajo(int emprendimientoId) { return _productos.StockBajo(emprendimientoId); }

        public Producto Guardar(int emprendimientoId, Producto p)
        {
            p.Nombre = Validar.Requerido(p.Nombre, "Nombre", 150);
            p.Categoria = Validar.Opcional(p.Categoria, "Categoría", 100);
            Validar.NoNegativo(p.PrecioCompra, "Precio de compra");
            Validar.MayorQueCero(p.PrecioVenta, "Precio de venta");
            Validar.Maximo(p.PrecioVenta, 999_999_999m, "Precio de venta");
            Validar.NoNegativo(p.StockMinimo, "Stock mínimo");
            Validar.NoNegativo(p.Stock, "Stock inicial");

            foreach (Producto otro in _productos.ListarPorEmprendimiento(emprendimientoId))
                if (otro.Id != p.Id && string.Equals(otro.Nombre, p.Nombre, StringComparison.OrdinalIgnoreCase))
                    throw new ReglaDeNegocioException("Ya existe el producto '" + otro.Nombre + "'.");

            p.EmprendimientoId = emprendimientoId;
            if (p.Id == 0) return _productos.Insertar(p);
            ServicioNomina.Propio(_productos.ObtenerPorId(p.Id)?.EmprendimientoId, emprendimientoId, "Producto");
            _productos.Actualizar(p);
            return p;
        }

        /// <summary>Advertencia (no error) para mostrar antes de guardar.</summary>
        public static string? Advertencia(Producto p)
        {
            if (p.PrecioVenta > 0 && p.PrecioVenta < p.PrecioCompra)
                return "El precio de venta es menor que el de compra: venderías con pérdida.";
            return null;
        }

        public void Eliminar(int emprendimientoId, int productoId)
        {
            ServicioNomina.Propio(_productos.ObtenerPorId(productoId)?.EmprendimientoId, emprendimientoId, "Producto");
            _productos.Eliminar(productoId);
        }

        public void Entrada(int emprendimientoId, int productoId, int cantidad)
        {
            Validar.EnteroPositivo(cantidad, "Cantidad");
            ServicioNomina.Propio(_productos.ObtenerPorId(productoId)?.EmprendimientoId, emprendimientoId, "Producto");
            _productos.MoverStock(productoId, cantidad, true);
        }

        public void Salida(int emprendimientoId, int productoId, int cantidad)
        {
            Validar.EnteroPositivo(cantidad, "Cantidad");
            Producto? p = _productos.ObtenerPorId(productoId);
            ServicioNomina.Propio(p?.EmprendimientoId, emprendimientoId, "Producto");
            if (cantidad > p!.Stock)
                throw new ReglaDeNegocioException("Stock insuficiente. Disponible: " + p.Stock + ".");
            _productos.MoverStock(productoId, cantidad, false);
        }

        public int AjustarPrecios(int emprendimientoId, decimal porcentaje)
        {
            if (porcentaje == 0m) throw new ReglaDeNegocioException("Escribe un porcentaje distinto de cero.");
            if (porcentaje <= -100m || porcentaje > 500m)
                throw new ReglaDeNegocioException("El porcentaje debe estar entre -99 y 500.");
            return _productos.AjustarPrecios(emprendimientoId, porcentaje);
        }
    }

    /// <summary>UNA SOLA MISION: ventas. El registro completo lo hace Oracle en sp_sg_registrar_venta.</summary>
    public class ServicioVentas
    {
        private readonly IVentaDao _ventas;
        private readonly IProductoDao _productos;
        private readonly IReporteDao _reportes;

        public ServicioVentas(IVentaDao ventas, IProductoDao productos, IReporteDao reportes)
        {
            _ventas = ventas;
            _productos = productos;
            _reportes = reportes;
        }

        public List<Venta> Listar(int emprendimientoId) { return _ventas.ListarPorEmprendimiento(emprendimientoId); }

        public Venta Obtener(int emprendimientoId, int ventaId)
        {
            Venta? v = _ventas.ObtenerConDetalles(ventaId);
            ServicioNomina.Propio(v?.EmprendimientoId, emprendimientoId, "Venta");
            return v!;
        }

        /// <summary>
        /// Valida el carrito en C# (mensajes rapidos) y luego Oracle lo vuelve a validar y lo guarda
        /// todo en una sola transaccion: cabecera, detalle, stock e ingreso en finanzas.
        /// </summary>
        public Venta Registrar(int emprendimientoId, string? observacion, List<DetalleVenta> carrito)
        {
            if (carrito == null || carrito.Count == 0)
                throw new ReglaDeNegocioException("Agrega al menos un producto a la venta.");
            string obs = Validar.Opcional(observacion, "Observación", 500);

            Dictionary<int, Producto> productos = new Dictionary<int, Producto>();
            foreach (Producto p in _productos.ListarPorEmprendimiento(emprendimientoId)) productos[p.Id] = p;

            Dictionary<int, int> cantidades = new Dictionary<int, int>();
            foreach (DetalleVenta d in carrito)
            {
                Validar.EnteroPositivo(d.Cantidad, "Cantidad");
                if (!productos.ContainsKey(d.ProductoId))
                    throw new ReglaDeNegocioException("Un producto del carrito ya no existe.");
                cantidades[d.ProductoId] = (cantidades.TryGetValue(d.ProductoId, out int c) ? c : 0) + d.Cantidad;
            }
            foreach (KeyValuePair<int, int> kv in cantidades)
            {
                Producto p = productos[kv.Key];
                if (kv.Value > p.Stock)
                    throw new ReglaDeNegocioException("Stock insuficiente de '" + p.Nombre + "'. Disponible: " + p.Stock + ".");
            }

            int id = _ventas.Registrar(emprendimientoId, obs, carrito);
            return Obtener(emprendimientoId, id);
        }

        public ResumenVentas Resumen(int emprendimientoId)
        {
            DateTime hoy = DateTime.Today;
            return new ResumenVentas
            {
                Hoy = _reportes.VentasEntre(emprendimientoId, hoy, hoy),
                UltimosSieteDias = _reportes.VentasEntre(emprendimientoId, hoy.AddDays(-6), hoy),
                EsteMes = _reportes.VentasEntre(emprendimientoId, new DateTime(hoy.Year, hoy.Month, 1), hoy),
                TotalHistorico = _reportes.VentasEntre(emprendimientoId, new DateTime(1900, 1, 1), new DateTime(9999, 12, 31)),
                CantidadVentas = _ventas.ListarPorEmprendimiento(emprendimientoId).Count
            };
        }
    }

    /// <summary>UNA SOLA MISION: ingresos y gastos manuales y totales (pkg_sg_reportes).</summary>
    public class ServicioFinanzas
    {
        private readonly IMovimientoDao _movimientos;
        private readonly IObligacionSocioDao _obligaciones;
        private readonly IReporteDao _reportes;
        private readonly ServicioCatalogos _catalogos;

        public ServicioFinanzas(IMovimientoDao movimientos, IObligacionSocioDao obligaciones, IReporteDao reportes, ServicioCatalogos catalogos)
        {
            _movimientos = movimientos;
            _obligaciones = obligaciones;
            _reportes = reportes;
            _catalogos = catalogos;
        }

        public List<Movimiento> Listar(int emprendimientoId) { return _movimientos.ListarPorEmprendimiento(emprendimientoId); }
        public ResumenFinanciero Resumen(int emprendimientoId) { return _reportes.Finanzas(emprendimientoId); }

        public Movimiento Registrar(int emprendimientoId, bool esIngreso, int categoriaId, string? descripcion, decimal monto, DateTime fecha)
        {
            Movimiento m = Preparar(emprendimientoId, esIngreso, categoriaId, descripcion, monto, fecha);
            return _movimientos.Insertar(m);
        }

        /// <summary>
        /// Aporte de socio, inversion o prestamo con el porcentaje que hay que devolver.
        /// La devolucion queda pendiente o, si gastoInmediato, se registra ya como gasto "Devolución a socios".
        /// Todo ocurre en una sola transaccion de Oracle (sp_sg_registrar_aporte).
        /// </summary>
        public Movimiento RegistrarAporte(int emprendimientoId, int categoriaId, string? descripcion, decimal monto, DateTime fecha,
                                          string? beneficiario, decimal porcentaje, bool gastoInmediato, DateTime? fechaLimite = null)
        {
            Movimiento m = Preparar(emprendimientoId, true, categoriaId, descripcion, monto, fecha);
            CategoriaMovimiento cat = _catalogos.Categorias().Find(c => c.Id == categoriaId)!;
            if (!cat.PermiteDevolucion)
                throw new ReglaDeNegocioException("El porcentaje a devolver solo aplica a aportes de socios, inversiones y préstamos.");
            string socio = Validar.Requerido(beneficiario, "Socio o inversionista", 100);
            if (porcentaje <= 0 || porcentaje > 500)
                throw new ReglaDeNegocioException("El porcentaje a devolver debe ser mayor que 0 y máximo 500.");
            if (Math.Round(m.Monto * porcentaje / 100m, 2) <= 0)
                throw new ReglaDeNegocioException("Con ese porcentaje la devolución queda en $0. Revisa el monto o el porcentaje.");

            if (gastoInmediato) fechaLimite = null;
            if (fechaLimite.HasValue && fechaLimite.Value.Date < fecha.Date)
                throw new ReglaDeNegocioException("La fecha para devolver no puede ser anterior a la fecha del aporte.");

            m.Id = _obligaciones.RegistrarAporte(m, socio, Math.Round(porcentaje, 2), gastoInmediato, fechaLimite);
            return m;
        }

        public static decimal MontoDevolucion(decimal monto, decimal porcentaje) { return Math.Round(monto * porcentaje / 100m, 2); }

        public List<ObligacionSocio> Obligaciones(int emprendimientoId) { return _obligaciones.ListarPorEmprendimiento(emprendimientoId); }

        public void PagarObligacion(int emprendimientoId, int obligacionId, DateTime fecha)
        {
            Validar.FechaNoFutura(fecha, "Fecha de pago");
            _obligaciones.Pagar(emprendimientoId, obligacionId, fecha.Date == DateTime.Today ? DateTime.Now : fecha.Date.AddHours(12));
        }

        private Movimiento Preparar(int emprendimientoId, bool esIngreso, int categoriaId, string? descripcion, decimal monto, DateTime fecha)
        {
            Validar.Seleccionado(categoriaId, "Categoría");
            Validar.MayorQueCero(monto, "Monto");
            Validar.Maximo(monto, 9_999_999_999m, "Monto");
            Validar.FechaNoFutura(fecha, "Fecha");
            string desc = Validar.Opcional(descripcion, "Descripción", 300);

            string tipoCategoria = esIngreso ? CategoriaMovimiento.TipoIngreso : CategoriaMovimiento.TipoGasto;
            CategoriaMovimiento? cat = _catalogos.CategoriasManuales(tipoCategoria).Find(c => c.Id == categoriaId);
            if (cat == null)
                throw new ReglaDeNegocioException("Elige una categoría de " + (esIngreso ? "ingreso" : "gasto") + " de la lista.");

            return new Movimiento
            {
                EmprendimientoId = emprendimientoId,
                Tipo = esIngreso ? TipoMovimiento.Ingreso : TipoMovimiento.Gasto,
                CategoriaId = categoriaId,
                Categoria = cat.Nombre,
                Descripcion = desc,
                Monto = Math.Round(monto, 2),
                Fecha = fecha.Date == DateTime.Today ? DateTime.Now : fecha.Date.AddHours(12)
            };
        }

        public void Eliminar(int emprendimientoId, int movimientoId)
        {
            Movimiento? m = _movimientos.ObtenerPorId(movimientoId);
            ServicioNomina.Propio(m?.EmprendimientoId, emprendimientoId, "Movimiento");
            if (!m!.EsManual)
                throw new ReglaDeNegocioException(m.Referencia.StartsWith("DEVOLUCION-", StringComparison.Ordinal)
                    ? "Esta es la devolución de un aporte; se maneja desde la pestaña \"Por devolver\"."
                    : "Este movimiento lo creó una venta o el inventario; no se elimina a mano.");
            // Un aporte cuya devolucion ya se pago no se borra: el gasto de la devolucion quedaria sin su ingreso
            ObligacionSocio? o = m.EsIngreso ? _obligaciones.PorMovimiento(movimientoId) : null;
            if (o != null && !o.EsPendiente)
                throw new ReglaDeNegocioException("La devolución de este aporte ya se pagó a " + o.Beneficiario + "; no se puede eliminar el aporte.");
            _movimientos.Eliminar(movimientoId);   // si la devolucion estaba pendiente, se borra con el aporte (ON DELETE CASCADE)
        }
    }

    /// <summary>UNA SOLA MISION: presupuestos mensuales. El estado lo calcula sp_sg_presupuestos_estado.</summary>
    public class ServicioPresupuestos
    {
        private readonly IPresupuestoDao _presupuestos;
        private readonly ServicioCatalogos _catalogos;

        public ServicioPresupuestos(IPresupuestoDao presupuestos, ServicioCatalogos catalogos)
        {
            _presupuestos = presupuestos;
            _catalogos = catalogos;
        }

        public List<Presupuesto> Listar(int emprendimientoId, string mes)
        {
            return _presupuestos.EstadoDelMes(emprendimientoId, Validar.Mes(mes));
        }

        public void Guardar(int emprendimientoId, int presupuestoId, int categoriaId, decimal limite, string mes)
        {
            Validar.MayorQueCero(limite, "Límite mensual");
            Validar.Maximo(limite, 9_999_999_999m, "Límite mensual");

            if (presupuestoId > 0)
            {
                Presupuesto? actual = _presupuestos.ObtenerPorId(presupuestoId);
                ServicioNomina.Propio(actual?.EmprendimientoId, emprendimientoId, "Presupuesto");
                _presupuestos.ActualizarLimite(presupuestoId, limite);
                return;
            }

            string m = Validar.Mes(mes);
            Validar.Seleccionado(categoriaId, "Categoría");
            if (_catalogos.CategoriasDeGasto().Find(c => c.Id == categoriaId) == null)
                throw new ReglaDeNegocioException("Los presupuestos solo se definen para categorías de gasto.");
            if (_presupuestos.EstadoDelMes(emprendimientoId, m).Exists(p => p.CategoriaId == categoriaId))
                throw new ReglaDeNegocioException("Ya existe un presupuesto para esa categoría en " + Formato.NombreMes(m) + ". Edítalo.");

            _presupuestos.Insertar(new Presupuesto
            {
                EmprendimientoId = emprendimientoId,
                CategoriaId = categoriaId,
                MontoLimite = limite,
                Mes = m
            });
        }

        public void Eliminar(int emprendimientoId, int presupuestoId)
        {
            ServicioNomina.Propio(_presupuestos.ObtenerPorId(presupuestoId)?.EmprendimientoId, emprendimientoId, "Presupuesto");
            _presupuestos.Eliminar(presupuestoId);
        }
    }

    /// <summary>UNA SOLA MISION: vacantes y postulaciones (las hojas de vida llegan por Telegram).</summary>
    public class ServicioVacantes
    {
        public const int TamanoMaximoCv = 5 * 1024 * 1024;
        public const string TipoPdf = "application/pdf";
        public const string TipoDocx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

        private readonly IVacanteDao _vacantes;
        private readonly IPostulacionDao _postulaciones;
        private readonly ICargoDao _cargos;
        private readonly ServicioCatalogos _catalogos;

        public ServicioVacantes(IVacanteDao vacantes, IPostulacionDao postulaciones, ICargoDao cargos, ServicioCatalogos catalogos)
        {
            _vacantes = vacantes;
            _postulaciones = postulaciones;
            _cargos = cargos;
            _catalogos = catalogos;
        }

        public List<Vacante> Listar(int emprendimientoId) { return _vacantes.ListarPorEmprendimiento(emprendimientoId); }
        public List<Vacante> Abiertas() { return _vacantes.ListarAbiertas(); }
        public Vacante? Obtener(int id) { return _vacantes.ObtenerPorId(id); }

        public Vacante Guardar(int emprendimientoId, Vacante v)
        {
            v.Titulo = Validar.Requerido(v.Titulo, "Título", 120);
            v.Descripcion = Validar.Opcional(v.Descripcion, "Descripción", 1000);
            Validar.Seleccionado(v.CargoId, "Cargo");
            Validar.Seleccionado(v.CiudadCodigo, "Ciudad");
            if (v.FechaCierre.HasValue && v.FechaCierre.Value.Date < DateTime.Today)
                throw new ReglaDeNegocioException("La fecha de cierre no puede ser anterior a hoy.");
            ServicioNomina.Propio(_cargos.ObtenerPorId(v.CargoId)?.EmprendimientoId, emprendimientoId, "Cargo");

            v.EmprendimientoId = emprendimientoId;
            if (v.Id == 0) return _vacantes.Insertar(v);
            ServicioNomina.Propio(_vacantes.ObtenerPorId(v.Id)?.EmprendimientoId, emprendimientoId, "Vacante");
            _vacantes.Actualizar(v);
            return v;
        }

        public void AbrirOCerrar(int emprendimientoId, int vacanteId)
        {
            Vacante? v = _vacantes.ObtenerPorId(vacanteId);
            ServicioNomina.Propio(v?.EmprendimientoId, emprendimientoId, "Vacante");
            if (!v!.EstaAbierta && v.FechaCierre.HasValue && v.FechaCierre.Value.Date < DateTime.Today)
                throw new ReglaDeNegocioException("La fecha de cierre ya pasó. Edita la vacante y cambia la fecha antes de abrirla.");
            _vacantes.CambiarEstado(vacanteId, v.EstaAbierta ? Estados.Cerrada : Estados.Abierta);
        }

        public void Eliminar(int emprendimientoId, int vacanteId)
        {
            ServicioNomina.Propio(_vacantes.ObtenerPorId(vacanteId)?.EmprendimientoId, emprendimientoId, "Vacante");
            _vacantes.Eliminar(vacanteId);
        }

        public List<Postulacion> Postulaciones(int emprendimientoId, int vacanteId)
        {
            ServicioNomina.Propio(_vacantes.ObtenerPorId(vacanteId)?.EmprendimientoId, emprendimientoId, "Vacante");
            return _postulaciones.ListarPorVacante(vacanteId);
        }

        public ArchivoCv HojaDeVida(int emprendimientoId, int postulacionId)
        {
            Postulacion? p = _postulaciones.ObtenerPorId(postulacionId);
            ServicioNomina.Propio(p?.EmprendimientoId, emprendimientoId, "Postulación");
            ArchivoCv? cv = _postulaciones.ObtenerCv(postulacionId);
            if (cv == null) throw new ReglaDeNegocioException("La hoja de vida no está disponible.");
            if (p!.Estado == EstadosPostulacion.Recibida) _postulaciones.CambiarEstado(postulacionId, EstadosPostulacion.EnRevision);
            return cv;
        }

        public void Rechazar(int emprendimientoId, int postulacionId)
        {
            Postulacion? p = _postulaciones.ObtenerPorId(postulacionId);
            ServicioNomina.Propio(p?.EmprendimientoId, emprendimientoId, "Postulación");
            if (p!.Estado == EstadosPostulacion.Aceptada)
                throw new ReglaDeNegocioException("Este aspirante ya fue contratado.");
            _postulaciones.CambiarEstado(postulacionId, EstadosPostulacion.Rechazada);
        }

        /// <summary>sp_sg_contratar_postulante: lo crea como trabajador con el cargo de la vacante.</summary>
        public int Contratar(int emprendimientoId, int postulacionId, DateTime fechaIngreso)
        {
            Postulacion? p = _postulaciones.ObtenerPorId(postulacionId);
            ServicioNomina.Propio(p?.EmprendimientoId, emprendimientoId, "Postulación");
            if (fechaIngreso.Date < DateTime.Today.AddDays(-30) || fechaIngreso.Date > DateTime.Today.AddDays(90))
                throw new ReglaDeNegocioException("La fecha de ingreso debe estar entre hace 30 días y dentro de 90 días.");
            return _postulaciones.Contratar(postulacionId, fechaIngreso.Date);
        }

        /// <summary>Lo usa el bot cuando un aspirante termina de enviar sus datos y su hoja de vida.</summary>
        public int RegistrarPostulacion(Postulacion p, ArchivoCv cv, bool autorizaDatos)
        {
            if (!autorizaDatos)
                throw new ReglaDeNegocioException("Sin la autorización de tratamiento de datos no podemos recibir tu hoja de vida.");
            Vacante? v = _vacantes.ObtenerPorId(p.VacanteId);
            if (v == null || !v.EstaAbierta) throw new ReglaDeNegocioException("La vacante ya no está disponible.");

            p.Nombre = Validar.NombrePersona(p.Nombre, "Nombre");
            p.Apellido = Validar.NombrePersona(p.Apellido, "Apellido");
            p.NumeroDocumento = Validar.Documento(_catalogos.TipoDocumento(p.TipoDocumento), p.NumeroDocumento);
            p.Telefono = Validar.Telefono(p.Telefono, true);
            p.Email = Validar.Correo(p.Email, false);
            Validar.Seleccionado(p.CiudadCodigo, "Ciudad");

            if (cv.Contenido.Length == 0) throw new ReglaDeNegocioException("El archivo está vacío.");
            if (cv.Contenido.Length > TamanoMaximoCv) throw new ReglaDeNegocioException("La hoja de vida no puede pesar más de 5 MB.");
            if (cv.Tipo != TipoPdf && cv.Tipo != TipoDocx)
                throw new ReglaDeNegocioException("Envía la hoja de vida en PDF o Word (.docx).");
            if (cv.Tipo == TipoPdf && !(cv.Contenido.Length > 4 && cv.Contenido[0] == 0x25 && cv.Contenido[1] == 0x50 && cv.Contenido[2] == 0x44 && cv.Contenido[3] == 0x46))
                throw new ReglaDeNegocioException("El archivo no parece un PDF válido.");
            if (cv.Tipo == TipoDocx && !(cv.Contenido.Length > 2 && cv.Contenido[0] == 0x50 && cv.Contenido[1] == 0x4B))
                throw new ReglaDeNegocioException("El archivo no parece un documento Word válido.");
            cv.Nombre = Validar.Requerido(cv.Nombre, "Nombre del archivo", 200);

            foreach (Postulacion otra in _postulaciones.ListarPorVacante(p.VacanteId))
                if (otra.TipoDocumento == p.TipoDocumento && otra.NumeroDocumento == p.NumeroDocumento)
                    throw new ReglaDeNegocioException("Ya te postulaste a esta vacante con ese documento.");

            return _postulaciones.Insertar(p, cv);
        }
    }
}
