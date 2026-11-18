/* =====================================================================
   SGAPE v2 - USUARIO DE BASE DE DATOS PARA EL BOT (minimo privilegio)
   Ejecutar como ADMIN en SQL Developer (F5), DESPUES de sgape_v2_completo.sql.
   El bot NO usa la cuenta ADMIN: solo puede leer las vistas y catalogos,
   ejecutar los paquetes y procedimientos que necesita, registrar movimientos
   (ingresos/gastos confirmados) y guardar postulaciones.
   Cambia la clave antes de ejecutar.
   ===================================================================== */
SET SERVEROUTPUT ON;

DECLARE
  v_existe NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_existe FROM all_users WHERE username = 'SGAPE_BOT';
  IF v_existe = 0 THEN
    EXECUTE IMMEDIATE 'CREATE USER sgape_bot IDENTIFIED BY "CambiaEstaClave_2026x"';
  END IF;
  EXECUTE IMMEDIATE 'GRANT CREATE SESSION TO sgape_bot';
END;
/

DECLARE
  PROCEDURE dar(p_privilegio VARCHAR2, p_objeto VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE 'GRANT ' || p_privilegio || ' ON ' || p_objeto || ' TO sgape_bot';
    -- Sinonimo: el bot escribe "vw_sg_vacantes" sin anteponer "ADMIN."
    EXECUTE IMMEDIATE 'CREATE OR REPLACE SYNONYM sgape_bot.' || p_objeto || ' FOR ' || USER || '.' || p_objeto;
    DBMS_OUTPUT.PUT_LINE('OK ' || p_privilegio || ' ' || p_objeto);
  END;
BEGIN
  -- Lectura: todas las vistas y los catalogos
  FOR v IN (SELECT view_name FROM user_views WHERE view_name LIKE 'VW\_SG\_%' ESCAPE '\') LOOP
    dar('SELECT', v.view_name);
  END LOOP;
  FOR t IN (SELECT column_value AS nombre FROM TABLE(sys.odcivarchar2list(
              'SG_SECTOR', 'SG_CIUDAD', 'SG_DEPARTAMENTO', 'SG_CARGO_PLANTILLA', 'SG_TIPO_DOCUMENTO',
              'SG_CATEGORIA_MOVIMIENTO', 'SG_CONOCIMIENTO'))) LOOP
    dar('SELECT', t.nombre);
  END LOOP;

  -- Escritura minima
  dar('INSERT', 'SG_MOVIMIENTO');      -- ingresos y gastos confirmados con el boton
  dar('INSERT', 'SG_POSTULACION');     -- hojas de vida de los aspirantes

  -- Paquetes y procedimientos que usa el bot
  FOR o IN (SELECT column_value AS nombre FROM TABLE(sys.odcivarchar2list(
              'PKG_SG_NOMINA', 'PKG_SG_REPORTES', 'PKG_SG_TELEGRAM',
              'SP_SG_RESUMEN_EMPRENDIMIENTO', 'SP_SG_PRODUCTOS_STOCK_BAJO', 'SP_SG_PRESUPUESTOS_ESTADO',
              'SP_SG_LIMPIAR_BITACORA', 'SP_SG_CERRAR_VACANTES_VENCIDAS'))) LOOP
    dar('EXECUTE', o.nombre);
  END LOOP;
END;
/

-- Verificacion
SELECT privilege, table_name FROM user_tab_privs_made WHERE grantee = 'SGAPE_BOT' ORDER BY table_name;
