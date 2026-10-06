using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Logica.Utilidades;

namespace Logica.Repositorios
{
    /// <summary>
    /// PILAR: ABSTRACCION + HERENCIA. Toda la logica de leer y escribir archivos CSV
    /// vive aqui UNA sola vez. Cada repositorio hijo solo explica como convertir
    /// su entidad a texto y de texto a entidad (PILAR: POLIMORFISMO).
    /// En Java esa logica estaba repetida 10 veces dentro de ArchivoDAO y GestorInventario.
    /// </summary>
    public abstract class RepositorioCsv<T> where T : class
    {
        private readonly string _ruta;

        protected RepositorioCsv(string nombreArchivo)
        {
            _ruta = Path.Combine(RutasApp.CarpetaDatos, nombreArchivo);
        }

        protected abstract string Serializar(T entidad);
        protected abstract T? Deserializar(string[] campos);
        protected abstract int ObtenerId(T entidad);
        protected abstract void AsignarId(T entidad, int id);

        /// <summary>Gancho opcional: se ejecuta antes de leer el archivo.</summary>
        protected virtual void AntesDeCargar()
        {
        }

        public List<T> Listar()
        {
            AntesDeCargar();
            List<T> lista = new List<T>();
            if (!File.Exists(_ruta)) return lista;

            foreach (string linea in File.ReadAllLines(_ruta, Encoding.UTF8))
            {
                if (string.IsNullOrWhiteSpace(linea)) continue;
                try
                {
                    T? entidad = Deserializar(CsvUtil.Dividir(linea));
                    if (entidad != null) lista.Add(entidad);
                }
                catch (FormatException)
                {
                    // Linea danada: se ignora para que el programa no se caiga.
                }
                catch (IndexOutOfRangeException)
                {
                }
            }
            return lista;
        }

        public void Reemplazar(List<T> lista)
        {
            List<string> lineas = new List<string>();
            foreach (T entidad in lista) lineas.Add(Serializar(entidad));

            // Se escribe en un archivo temporal y luego se reemplaza: si el programa
            // se cierra a mitad de la escritura, no se pierde la informacion anterior.
            string temporal = _ruta + ".tmp";
            File.WriteAllLines(temporal, lineas, new UTF8Encoding(false));
            File.Move(temporal, _ruta, true);
        }

        public T Insertar(T entidad)
        {
            List<T> lista = Listar();
            int maximo = 0;
            foreach (T e in lista)
            {
                int id = ObtenerId(e);
                if (id > maximo) maximo = id;
            }
            AsignarId(entidad, maximo + 1);
            lista.Add(entidad);
            Reemplazar(lista);
            return entidad;
        }

        public T? ObtenerPorId(int id)
        {
            foreach (T e in Listar())
            {
                if (ObtenerId(e) == id) return e;
            }
            return null;
        }

        public void Actualizar(T entidad)
        {
            List<T> lista = Listar();
            int id = ObtenerId(entidad);
            for (int i = 0; i < lista.Count; i++)
            {
                if (ObtenerId(lista[i]) == id)
                {
                    lista[i] = entidad;
                    Reemplazar(lista);
                    return;
                }
            }
            throw new ReglaDeNegocioException("No se encontró el registro que se quiere actualizar.");
        }

        public bool Eliminar(int id)
        {
            List<T> lista = Listar();
            for (int i = 0; i < lista.Count; i++)
            {
                if (ObtenerId(lista[i]) == id)
                {
                    lista.RemoveAt(i);
                    Reemplazar(lista);
                    return true;
                }
            }
            return false;
        }

        public int EliminarDonde(Predicate<T> condicion)
        {
            List<T> lista = Listar();
            int eliminados = lista.RemoveAll(condicion);
            if (eliminados > 0) Reemplazar(lista);
            return eliminados;
        }
    }
}