/* =====================================================================
   SGAPE v2 - USUARIO DE LA APLICACION (SGAPE_APP)
   SQL Developer, conectado como ADMIN: abrir y pulsar F5.
   Se puede ejecutar varias veces (por ejemplo, despues de crear tablas nuevas).

   POR QUE: el instalador lleva la conexion adentro para que SGAPE funcione en
   cualquier computador. NO debe llevar la clave de ADMIN (con ella se puede
   borrar la base completa). SGAPE_APP solo puede leer/escribir los datos de
   SGAPE y ejecutar sus paquetes: no crea ni borra tablas.

   Se crea BLOQUEADO y con una clave al azar que nadie conoce. Despues TU eliges
   la clave y lo desbloqueas (12 a 30 caracteres, mayuscula, minuscula y numero,
   sin comillas dobles y sin la palabra SGAPE_APP):

     ALTER USER sgape_app IDENTIFIED BY "TuClaveSegura2026" ACCOUNT UNLOCK;

   Esa misma clave la pide preparar_instalador.bat.
   ===================================================================== */
SET SERVEROUTPUT ON;

DECLARE
  v_existe NUMBER;
BEGIN
  SELECT COUNT(*) INTO v_existe FROM all_users WHERE username = 'SGAPE_APP';
  IF v_existe = 0 THEN
    -- Clave temporal al azar (nadie la conoce) y cuenta bloqueada hasta que elijas la tuya
    EXECUTE IMMEDIATE 'CREATE USER sgape_app IDENTIFIED BY "' ||
                      DBMS_RANDOM.STRING('U', 6) || DBMS_RANDOM.STRING('L', 6) ||
                      TRUNC(DBMS_RANDOM.VALUE(100000, 999999)) || '" ACCOUNT LOCK';
    DBMS_OUTPUT.PUT_LINE('OK usuario SGAPE_APP creado (bloqueado)');
  ELSE
    DBMS_OUTPUT.PUT_LINE('SGAPE_APP ya existia: solo se actualizan permisos');
  END IF;
  EXECUTE IMMEDIATE 'GRANT CREATE SESSION TO sgape_app';
END;
/

DECLARE
  v_n NUMBER := 0;   -- (en PL/SQL las variables van ANTES de los procedimientos locales)
  PROCEDURE dar(p_privilegio VARCHAR2, p_objeto VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE 'GRANT ' || p_privilegio || ' ON ' || p_objeto || ' TO sgape_app';
    -- Sinonimo: la aplicacion escribe "sg_producto" sin anteponer "ADMIN."
    EXECUTE IMMEDIATE 'CREATE OR REPLACE SYNONYM sgape_app.' || p_objeto || ' FOR ' || USER || '.' || p_objeto;
  END;
BEGIN
  -- Tablas de SGAPE: leer y escribir datos (no alterar ni borrar la tabla)
  FOR t IN (SELECT table_name FROM user_tables WHERE table_name LIKE 'SG\_%' ESCAPE '\') LOOP
    dar('SELECT, INSERT, UPDATE, DELETE', t.table_name);
    v_n := v_n + 1;
  END LOOP;
  -- Vistas
  FOR v IN (SELECT view_name FROM user_views WHERE view_name LIKE 'VW\_SG\_%' ESCAPE '\') LOOP
    dar('SELECT', v.view_name);
    v_n := v_n + 1;
  END LOOP;
  -- Paquetes, procedimientos y funciones
  FOR o IN (SELECT object_name FROM user_objects
             WHERE object_type IN ('PACKAGE', 'PROCEDURE', 'FUNCTION')
               AND (object_name LIKE 'PKG\_SG\_%' ESCAPE '\' OR object_name LIKE 'SP\_SG\_%' ESCAPE '\'
                    OR object_name LIKE 'FN\_SG\_%' ESCAPE '\')) LOOP
    dar('EXECUTE', o.object_name);
    v_n := v_n + 1;
  END LOOP;
  DBMS_OUTPUT.PUT_LINE('OK ' || v_n || ' objetos con permiso para SGAPE_APP');
END;
/

-- Verificacion
SELECT username, account_status FROM dba_users WHERE username = 'SGAPE_APP';
SELECT COUNT(*) AS permisos FROM user_tab_privs_made WHERE grantee = 'SGAPE_APP';
