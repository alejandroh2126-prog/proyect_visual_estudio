using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Entidades;
using Logica.Utilidades;
using Presentacion.Comun;

namespace Presentacion.ViewModels
{
    // =====================================================================
    // INICIO
    // =====================================================================
    public partial class InicioViewModel : ViewModelBase
    {
        [ObservableProperty] private ResumenEmprendimiento? _resumen;
        [ObservableProperty] private ResumenVentas? _ventas;
        [ObservableProperty] private bool _hayEmprendimiento;

        public ObservableCollection<ProductoStockBajo> StockBajo { get; } = new ObservableCollection<ProductoStockBajo>();
        public ObservableCollection<Presupuesto> Alertas { get; } = new ObservableCollection<Presupuesto>();
        public ObservableCollection<ResumenEmprendimiento> Todos { get; } = new ObservableCollection<ResumenEmprendimiento>();

        public string Saludo { get { return "Hola, " + (Sesion.Usuario?.Nombre ?? string.Empty); } }

        public override async Task Cargar()
        {
            await Ejecutar(async () =>
            {
                HayEmprendimiento = Sesion.HayEmprendimiento;
                int usuario = Sesion.IdUsuario;
                List<ResumenEmprendimiento> todos = await Task.Run(() => App.Servicios.Emprendimientos.Resumenes(usuario));
                Todos.Clear();
                foreach (ResumenEmprendimiento r in todos) Todos.Add(r);

                StockBajo.Clear();
                Alertas.Clear();
                if (!HayEmprendimiento) { Resumen = null; Ventas = null; return; }

                int emp = Sesion.IdEmprendimiento;
                Resumen = await Task.Run(() => App.Servicios.Reportes.Resumen(emp));
                Ventas = await Task.Run(() => App.Servicios.Ventas.Resumen(emp));
                foreach (ProductoStockBajo p in await Task.Run(() => App.Servicios.Inventario.StockBajo(emp))) StockBajo.Add(p);
                foreach (Presupuesto p in await Task.Run(() => App.Servicios.Presupuestos.Listar(emp, Validar.MesActual())))
                    if (p.Estado != Presupuesto.EstadoOk) Alertas.Add(p);
            });
        }

        [RelayCommand]
        private Task Recargar() { return Cargar(); }
    }

    // =====================================================================
    // EMPRENDIMIENTOS
    // =====================================================================
    public partial class EmprendimientosViewModel : CrudViewModel<Emprendimiento>
    {
        private readonly Func<Task> _alCambiar;

        public EmprendimientosViewModel(Func<Task> alCambiar)
        {
            _alCambiar = alCambiar;
        }

        protected override string Nombre { get { return "emprendimiento"; } }

        protected override Task<List<Emprendimiento>> Consultar()
        {
            int usuario = Sesion.IdUsuario;
            return Task.Run(() => App.Servicios.Emprendimientos.Listar(usuario));
        }

        protected override bool Coincide(Emprendimiento e, string f)
        {
            return Formato.Contiene(e.Nombre, f) || Formato.Contiene(e.Sector, f) || Formato.Contiene(e.Ciudad, f);
        }

        protected override string DescribirParaEliminar(Emprendimiento e)
        {
            return "\"" + e.Nombre + "\" con TODOS sus cargos, trabajadores, productos, ventas, movimientos, presupuestos y vacantes";
        }

        protected override async Task DespuesDeCargar() { await _alCambiar(); }

        protected override Task<FormularioViewModel?> CrearFormulario(Emprendimiento? e)
        {
            bool nuevo = e == null;
            List<Sector> sectores = App.Servicios.Catalogos.Sectores();
            List<Ciudad> ciudades = App.Servicios.Catalogos.Ciudades();

            FormularioViewModel f = new FormularioViewModel(nuevo ? "Nuevo emprendimiento" : "Editar emprendimiento", async form =>
            {
                Emprendimiento datos = new Emprendimiento
                {
                    Id = e?.Id ?? 0,
                    Nombre = form["nombre"].ValorTexto,
                    Descripcion = form["descripcion"].ValorTexto,
                    SectorId = form["sector"].Elegido<Sector>()?.Id ?? 0,
                    CiudadCodigo = form["ciudad"].Elegido<Ciudad>()?.Codigo ?? string.Empty,
                    FechaInicio = form["fecha"].Fecha ?? DateTime.Today,
                    Estado = nuevo ? Estados.Activo : (form["estado"].Seleccion as string ?? Estados.Activo)
                };
                int usuario = Sesion.IdUsuario;
                await Task.Run(() => App.Servicios.Emprendimientos.Guardar(usuario, datos));
            });

            f.Agregar(new CampoFormulario("nombre", "Nombre del emprendimiento", TipoCampo.Texto) { MaxLongitud = 150, Texto = e?.Nombre ?? string.Empty });
            f.Agregar(new CampoFormulario("sector", "Sector", TipoCampo.Lista)
            {
                Opciones = sectores,
                Seleccion = e == null ? null : sectores.Find(s => s.Id == e.SectorId),
                Ayuda = "Define los cargos sugeridos en Nómina."
            });
            f.Agregar(new CampoFormulario("ciudad", "Ciudad", TipoCampo.ListaPredictiva)
            {
                Opciones = ciudades,
                Seleccion = e == null ? null : ciudades.Find(c => c.Codigo == e.CiudadCodigo),
                Ayuda = "Empieza a escribir: \"Valle\" sugiere Valledupar (Cesar)."
            });
            f.Agregar(new CampoFormulario("fecha", "Fecha de inicio", TipoCampo.Fecha) { Fecha = e?.FechaInicio ?? DateTime.Today });
            f.Agregar(new CampoFormulario("descripcion", "Descripción", TipoCampo.TextoLargo) { Requerido = false, MaxLongitud = 500, Texto = e?.Descripcion ?? string.Empty });
            if (!nuevo)
                f.Agregar(new CampoFormulario("estado", "Estado", TipoCampo.Lista)
                {
                    Opciones = new[] { Estados.Activo, Estados.Inactivo },
                    Seleccion = e!.Estado
                });
            return Task.FromResult<FormularioViewModel?>(f);
        }

        protected override Task EliminarElemento(Emprendimiento e)
        {
            int usuario = Sesion.IdUsuario;
            return Task.Run(() => App.Servicios.Emprendimientos.Eliminar(usuario, e.Id));
        }
    }

    // =====================================================================
    // NOMINA: cargos, trabajadores y resumen
    // =====================================================================
    public partial class CargosViewModel : CrudViewModel<Cargo>
    {
        private readonly Func<Task> _alCambiar;

        public CargosViewModel(Func<Task> alCambiar) { _alCambiar = alCambiar; }

        protected override string Nombre { get { return "cargo"; } }

        protected override Task<List<Cargo>> Consultar()
        {
            int emp = Sesion.IdEmprendimiento;
            return Task.Run(() => App.Servicios.Nomina.Cargos(emp));
        }

        protected override bool Coincide(Cargo c, string f) { return Formato.Contiene(c.Nombre, f); }

        protected override async Task<FormularioViewModel?> CrearFormulario(Cargo? c)
        {
            int sector = Sesion.Emprendimiento?.SectorId ?? 0;
            List<CargoPlantilla> plantillas = await Task.Run(() => App.Servicios.Catalogos.PlantillasDeCargo(sector));

            FormularioViewModel f = new FormularioViewModel(c == null ? "Nuevo cargo" : "Editar cargo", async form =>
            {
                Cargo datos = new Cargo
                {
                    Id = c?.Id ?? 0,
                    PlantillaId = form["plantilla"].Elegido<CargoPlantilla>()?.Id,
                    Nombre = form["nombre"].ValorTexto,
                    SalarioBase = form["salario"].ValorMoneda,
                    Descripcion = form["descripcion"].ValorTexto
                };
                int emp = Sesion.IdEmprendimiento;
                await Task.Run(() => App.Servicios.Nomina.GuardarCargo(emp, datos));
            });

            CampoFormulario nombre = new CampoFormulario("nombre", "Nombre del cargo", TipoCampo.Texto) { MaxLongitud = 100, Texto = c?.Nombre ?? string.Empty };
            CampoFormulario salario = new CampoFormulario("salario", "Salario base mensual", TipoCampo.Moneda)
            {
                MaxLongitud = 15,
                Texto = c == null ? string.Empty : Math.Round(c.SalarioBase, 0).ToString(),
                Ayuda = "Se descuenta 4 % salud + 4 % pensión."
            };
            CampoFormulario descripcion = new CampoFormulario("descripcion", "Descripción", TipoCampo.TextoLargo) { Requerido = false, MaxLongitud = 300, Texto = c?.Descripcion ?? string.Empty };

            f.Agregar(new CampoFormulario("plantilla", "Cargo sugerido para tu sector", TipoCampo.ListaPredictiva)
            {
                Requerido = false,
                Opciones = plantillas,
                Seleccion = c?.PlantillaId == null ? null : plantillas.Find(p => p.Id == c.PlantillaId),
                Ayuda = "Elige uno y se llenan el nombre y el salario sugerido.",
                AlSeleccionar = o =>
                {
                    if (o is CargoPlantilla p)
                    {
                        nombre.Texto = p.Nombre;
                        salario.Texto = Math.Round(p.SalarioSugerido, 0).ToString();
                        if (descripcion.ValorTexto.Length == 0) descripcion.Texto = p.Descripcion;
                    }
                }
            });
            f.Agregar(nombre);
            f.Agregar(salario);
            f.Agregar(descripcion);
            return f;
        }

        protected override async Task EliminarElemento(Cargo c)
        {
            int emp = Sesion.IdEmprendimiento;
            await Task.Run(() => App.Servicios.Nomina.EliminarCargo(emp, c.Id));
        }

        protected override async Task DespuesDeCargar() { await _alCambiar(); }
    }

    public partial class TrabajadoresViewModel : CrudViewModel<Trabajador>
    {
        private readonly Func<Task> _alCambiar;

        public TrabajadoresViewModel(Func<Task> alCambiar) { _alCambiar = alCambiar; }

        protected override string Nombre { get { return "trabajador"; } }

        protected override Task<List<Trabajador>> Consultar()
        {
            int emp = Sesion.IdEmprendimiento;
            return Task.Run(() => App.Servicios.Nomina.Trabajadores(emp));
        }

        protected override bool Coincide(Trabajador t, string f)
        {
            return Formato.Contiene(t.NombreCompleto, f) || t.NumeroDocumento.Contains(f) || Formato.Contiene(t.Cargo, f);
        }

        protected override string DescribirParaEliminar(Trabajador t) { return "a " + t.NombreCompleto + " (si dejó de trabajar, mejor márcalo como inactivo)"; }

        protected override async Task<FormularioViewModel?> CrearFormulario(Trabajador? t)
        {
            int emp = Sesion.IdEmprendimiento;
            List<Cargo> cargos = await Task.Run(() => App.Servicios.Nomina.Cargos(emp));
            if (cargos.Count == 0)
                throw new ReglaDeNegocioException("Primero crea al menos un cargo en la pestaña Cargos.");
            List<TipoDocumento> tipos = App.Servicios.Catalogos.TiposDocumento();
            List<Ciudad> ciudades = App.Servicios.Catalogos.Ciudades();

            FormularioViewModel f = new FormularioViewModel(t == null ? "Nuevo trabajador" : "Editar trabajador", async form =>
            {
                Trabajador datos = new Trabajador
                {
                    Id = t?.Id ?? 0,
                    TipoDocumento = form["tipo"].Elegido<TipoDocumento>()?.Codigo ?? string.Empty,
                    NumeroDocumento = form["numero"].ValorTexto,
                    Nombre = form["nombre"].ValorTexto,
                    Apellido = form["apellido"].ValorTexto,
                    CargoId = form["cargo"].Elegido<Cargo>()?.Id ?? 0,
                    Telefono = form["telefono"].ValorTexto,
                    Email = form["email"].ValorTexto,
                    CiudadCodigo = form["ciudad"].Elegido<Ciudad>()?.Codigo ?? string.Empty,
                    FechaIngreso = form["fecha"].Fecha ?? DateTime.Today,
                    Estado = t?.Estado ?? Estados.Activo
                };
                await Task.Run(() => App.Servicios.Nomina.GuardarTrabajador(emp, datos));
            });

            TipoDocumento? tipoActual = tipos.Find(x => x.Codigo == (t?.TipoDocumento ?? "CC"));
            CampoFormulario numero = new CampoFormulario("numero", "Número de documento", TipoCampo.Texto)
            {
                MaxLongitud = 20,
                Texto = t?.NumeroDocumento ?? string.Empty,
                Ayuda = tipoActual?.Ayuda
            };
            f.Agregar(new CampoFormulario("tipo", "Tipo de documento", TipoCampo.Lista)
            {
                Opciones = tipos,
                Seleccion = tipoActual,
                AlSeleccionar = o => { if (o is TipoDocumento td) numero.Ayuda = td.Ayuda; }
            });
            f.Agregar(numero);
            f.Agregar(new CampoFormulario("nombre", "Nombres", TipoCampo.Texto) { MaxLongitud = 100, Texto = t?.Nombre ?? string.Empty });
            f.Agregar(new CampoFormulario("apellido", "Apellidos", TipoCampo.Texto) { MaxLongitud = 100, Texto = t?.Apellido ?? string.Empty });
            f.Agregar(new CampoFormulario("cargo", "Cargo", TipoCampo.Lista)
            {
                Opciones = cargos,
                Seleccion = t == null ? (cargos.Count == 1 ? cargos[0] : null) : cargos.Find(x => x.Id == t.CargoId)
            });
            f.Agregar(new CampoFormulario("telefono", "Celular", TipoCampo.Entero) { Requerido = false, MaxLongitud = 10, Texto = t?.Telefono ?? string.Empty, Ayuda = "10 números, sin espacios." });
            f.Agregar(new CampoFormulario("email", "Correo", TipoCampo.Texto) { Requerido = false, MaxLongitud = 150, Texto = t?.Email ?? string.Empty });
            f.Agregar(new CampoFormulario("ciudad", "Ciudad de residencia", TipoCampo.ListaPredictiva)
            {
                Requerido = false,
                Opciones = ciudades,
                Seleccion = ciudades.Find(c => c.Codigo == (t?.CiudadCodigo ?? Sesion.Emprendimiento?.CiudadCodigo))
            });
            f.Agregar(new CampoFormulario("fecha", "Fecha de ingreso", TipoCampo.Fecha) { Fecha = t?.FechaIngreso ?? DateTime.Today });
            return f;
        }

        protected override async Task EliminarElemento(Trabajador t)
        {
            int emp = Sesion.IdEmprendimiento;
            await Task.Run(() => App.Servicios.Nomina.EliminarTrabajador(emp, t.Id));
        }

        protected override async Task DespuesDeCargar() { await _alCambiar(); }

        [RelayCommand]
        private async Task CambiarEstado()
        {
            if (Seleccionado == null) { Notificador.Error("Selecciona un trabajador."); return; }
            Trabajador t = Seleccionado;
            int emp = Sesion.IdEmprendimiento;
            await Ejecutar(() => Task.Run(() => App.Servicios.Nomina.CambiarEstado(emp, t.Id)));
            await Recargar();
        }
    }

    public partial class NominaViewModel : ViewModelBase
    {
        public CargosViewModel Cargos { get; }
        public TrabajadoresViewModel Trabajadores { get; }
        public ObservableCollection<FilaNomina> Detalle { get; } = new ObservableCollection<FilaNomina>();

        [ObservableProperty] private ResumenNomina? _resumen;

        public NominaViewModel()
        {
            Cargos = new CargosViewModel(CargarResumen);
            Trabajadores = new TrabajadoresViewModel(CargarResumen);
        }

        public override async Task Cargar()
        {
            await Cargos.Cargar();
            await Trabajadores.Cargar();
        }

        /// <summary>pkg_sg_nomina: los cinco totales y la lista de nomina.</summary>
        private async Task CargarResumen()
        {
            int emp = Sesion.IdEmprendimiento;
            Resumen = await Task.Run(() => App.Servicios.Nomina.Resumen(emp));
            List<FilaNomina> filas = await Task.Run(() => App.Servicios.Nomina.Detalle(emp));
            Detalle.Clear();
            foreach (FilaNomina f in filas) Detalle.Add(f);
        }
    }
}
