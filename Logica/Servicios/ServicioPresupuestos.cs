using System;
using System.Collections.Generic;
using System.Globalization;
using Entidades;
using Logica.Repositorios;
using Logica.Utilidades;

namespace Logica.Servicios
{
    /// <summary>
    /// UNA SOLA MISION: presupuestos mensuales por categoria y su estado (cuanto llevo gastado).
    /// Para saber lo gastado le pregunta a ServicioFinanzas (no lee archivos de movimientos).
    /// </summary>
    public class ServicioPresupuestos
    {
        private readonly RepositorioPresupuestos _presupuestos;
        private readonly ServicioFinanzas _finanzas;

        public ServicioPresupuestos(RepositorioPresupuestos presupuestos, ServicioFinanzas finanzas)
        {
            _presupuestos = presupuestos;
            _finanzas = finanzas;
        }

        public static string MesActual()
        {
            return DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        public List<EstadoPresupuesto> Listar(int emprendimientoId, string mes)
        {
            List<Movimiento> movimientos = _finanzas.Listar(emprendimientoId);
            List<EstadoPresupuesto> resultado = new List<EstadoPresupuesto>();

            foreach (Presupuesto p in _presupuestos.Listar())
            {
                if (p.EmprendimientoId != emprendimientoId || p.Mes != mes) continue;

                decimal gastado = 0m;
                foreach (Movimiento m in movimientos)
                {
                    if (m.EsGasto && m.Categoria == p.Categoria &&
                        m.Fecha.ToString("yyyy-MM", CultureInfo.InvariantCulture) == mes)
                        gastado += m.Monto;
                }
                resultado.Add(new EstadoPresupuesto { Presupuesto = p, Gastado = gastado });
            }
            return resultado;
        }

        public List<EstadoPresupuesto> ListarExcedidos(int emprendimientoId, string mes)
        {
            return Listar(emprendimientoId, mes).FindAll(e => e.Excedido);
        }

        public Presupuesto Crear(int emprendimientoId, string categoria, decimal limite, string mes)
        {
            Validar.Requerido(categoria, "Categoría");
            Validar.MayorQueCero(limite, "Límite");

            DateTime fecha;
            if (!DateTime.TryParseExact(mes, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha))
                throw new ReglaDeNegocioException("El mes debe tener el formato aaaa-mm (ejemplo: 2026-10).");

            foreach (Presupuesto otro in _presupuestos.Listar())
            {
                if (otro.EmprendimientoId == emprendimientoId && otro.Mes == mes && otro.Categoria == categoria)
                    throw new ReglaDeNegocioException("Ya existe un presupuesto de '" + categoria + "' para ese mes. Elimínalo para crear otro.");
            }

            Presupuesto p = new Presupuesto
            {
                EmprendimientoId = emprendimientoId,
                Categoria = categoria,
                MontoLimite = limite,
                Mes = mes
            };
            return _presupuestos.Insertar(p);
        }

        public void Eliminar(int presupuestoId)
        {
            _presupuestos.Eliminar(presupuestoId);
        }
    }
}