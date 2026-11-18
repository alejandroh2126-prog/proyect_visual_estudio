/* =====================================================================
   SGAPE v2 - BORRAR TODO (tablas, vistas, funciones, procedimientos,
   paquetes y disparadores de SGAPE).

   CUIDADO: ESTO ELIMINA TODOS LOS DATOS. Ejecutalo solo si quieres
   empezar desde cero antes de correr sgape_v2_completo.sql.
   SQL Developer: abrir y pulsar "Ejecutar script" (F5).
   ===================================================================== */
SET SERVEROUTPUT ON;

DECLARE
  PROCEDURE borrar(p_sql VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE p_sql;
    DBMS_OUTPUT.PUT_LINE('OK  ' || p_sql);
  EXCEPTION
    WHEN OTHERS THEN
      -- -942 tabla/vista no existe, -4043 objeto no existe, -4080 disparador no existe
      IF SQLCODE NOT IN (-942, -4043, -4080, -2289) THEN RAISE; END IF;
  END;
BEGIN
  -- Vistas
  FOR v IN (SELECT view_name FROM user_views WHERE view_name LIKE 'VW\_SG\_%' ESCAPE '\') LOOP
    borrar('DROP VIEW ' || v.view_name);
  END LOOP;

  -- Paquetes, procedimientos y funciones
  FOR o IN (SELECT object_name, object_type FROM user_objects
             WHERE object_type IN ('PACKAGE', 'PROCEDURE', 'FUNCTION')
               AND (object_name LIKE 'PKG\_SG\_%' ESCAPE '\'
                 OR object_name LIKE 'SP\_SG\_%'  ESCAPE '\'
                 OR object_name LIKE 'FN\_SG\_%'  ESCAPE '\')) LOOP
    borrar('DROP ' || o.object_type || ' ' || o.object_name);
  END LOOP;

  -- Tablas (de hijas a padres; CASCADE CONSTRAINTS por si quedan llaves)
  FOR t IN (SELECT column_value AS nombre FROM TABLE(sys.odcivarchar2list(
              'SG_CONFIGURACION', 'SG_TELEGRAM_CODIGO', 'SG_TELEGRAM_VINCULO', 'SG_POSTULACION', 'SG_VACANTE',
              'SG_DETALLE_VENTA', 'SG_VENTA', 'SG_MOVIMIENTO', 'SG_PRESUPUESTO',
              'SG_PRODUCTO', 'SG_TRABAJADOR', 'SG_CARGO', 'SG_EMPRENDIMIENTO', 'SG_USUARIO',
              'SG_CARGO_PLANTILLA', 'SG_CATEGORIA_MOVIMIENTO', 'SG_TIPO_DOCUMENTO',
              'SG_SECTOR', 'SG_CIUDAD', 'SG_DEPARTAMENTO',
              'SG_AUDITORIA_PRECIO', 'SG_BITACORA', 'SG_CONOCIMIENTO', 'CONOCIMIENTO_SGAPE'))) LOOP
    borrar('DROP TABLE ' || t.nombre || ' CASCADE CONSTRAINTS PURGE');
  END LOOP;
END;
/
