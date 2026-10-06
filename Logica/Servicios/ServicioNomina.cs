using System;
using System.Collections.Generic;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: cargos, trabajadores y calculo de nomina.
    /// (En Java esto estaba mezclado dentro de Main.java, con 800 lineas.)
    /// </summary>
    public class ServicioNomina
    {
        private readonly RepositorioCargos _cargos;
        private readonly RepositorioTrabajadores _trabajadores;

        public ServicioNomina(RepositorioCargos cargos, RepositorioTrabajadores trabajadores)
        {
            _cargos = cargos;
            _trabajadores = trabajadores;
        }

        // ---------------- CARGOS ----------------

        public List<Cargo> ListarCargos(int emprendimientoId)
        {
            return _cargos.Listar().FindAll(c => c.EmprendimientoId == emprendimientoId);
        }

        public Cargo GuardarCargo(Cargo cargo)
        {
            Validar.Requerido(cargo.Nombre, "Nombre del cargo");
            Validar.MayorQueCero(cargo.SalarioBase, "Salario base");

            if (cargo.Id == 0) return _cargos.Insertar(cargo);
            _cargos.Actualizar(cargo);
            return cargo;
        }

        public void EliminarCargo(int cargoId)
        {
            foreach (Trabajador t in _trabajadores.Listar())
            {
                if (t.Cargo.Id == cargoId)
                    throw new ReglaDeNegocioException("No se puede eliminar: hay trabajadores con este cargo.");
            }
            _cargos.Eliminar(cargoId);
        }

        // ---------------- TRABAJADORES ----------------

        public List<Trabajador> ListarTrabajadores(int emprendimientoId)
        {
            return _trabajadores.Listar().FindAll(t => t.EmprendimientoId == emprendimientoId);
        }

        public Trabajador GuardarTrabajador(Trabajador trabajador)
        {
            Validar.Requerido(trabajador.Nombre, "Nombre");
            Validar.Requerido(trabajador.Apellido, "Apellido");
            Validar.Requerido(trabajador.Cedula, "Cédula");

            foreach (Trabajador otro in _trabajadores.Listar())
            {
                if (otro.EmprendimientoId == trabajador.EmprendimientoId &&
                    otro.Cedula == trabajador.Cedula && otro.Id != trabajador.Id)
                    throw new ReglaDeNegocioException("Ya existe un trabajador con esa cédula.");
            }

            if (trabajador.Id == 0) return _trabajadores.Insertar(trabajador);
            _trabajadores.Actualizar(trabajador);
            return trabajador;
        }

        public void EliminarTrabajador(int trabajadorId)
        {
            _trabajadores.Eliminar(trabajadorId);
        }

        // ---------------- NOMINA ----------------

        public ResumenNomina CalcularResumen(int emprendimientoId)
        {
            ResumenNomina r = new ResumenNomina();
            foreach (Trabajador t in ListarTrabajadores(emprendimientoId))
            {
                if (!t.EstaActivo) continue;
                r.TrabajadoresActivos++;
                r.NominaMensualBruta += t.SalarioMensual;
                r.TotalDeducciones += t.TotalDeducciones;
            }
            r.NominaQuincenal = r.NominaMensualBruta / 2m;
            r.NominaNeta = r.NominaMensualBruta - r.TotalDeducciones;
            return r;
        }
    }
}