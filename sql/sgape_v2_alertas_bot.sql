/* =====================================================================
   SGAPE v2 - ALERTAS AUTOMATICAS DEL BOT (actualizacion)
   Ejecuta este script SOLO si ya corriste sgape_v2_completo.sql antes
   del 17/11/2026 (una instalacion nueva ya trae todo esto).
   SQL Developer, conectado como ADMIN: abrir y pulsar F5.

   Agrega:
     - sg_telegram_vinculo.alertas  ('S' recibe avisos, 'N' no)
     - vw_sg_telegram_alertas       chats que reciben avisos y su emprendimiento
     - sp_sg_telegram_alertas       activa / desactiva / alterna los avisos (/alertas)
     - permisos para SGAPE_BOT       (si ya existe ese usuario)
   ===================================================================== */
SET SERVEROUTPUT ON;

ALTER TABLE sg_telegram_vinculo ADD (alertas CHAR(1) DEFAULT 'S' NOT NULL);
ALTER TABLE sg_telegram_vinculo ADD CONSTRAINT ck_sg_tvin_alertas CHECK (alertas IN ('S', 'N'));

-- Chats que reciben avisos automaticos (stock bajo, presupuestos, hojas de vida, resumen diario)
CREATE OR REPLACE VIEW vw_sg_telegram_alertas AS
SELECT t.chat_id,
       t.usuario_id,
       t.emprendimiento_id,
       e.nombre AS emprendimiento
  FROM sg_telegram_vinculo t
  JOIN sg_emprendimiento e ON e.id = t.emprendimiento_id
 WHERE t.alertas = 'S'
   AND e.estado  = 'activo';

-- p_activar: 'S' activa, 'N' desactiva, NULL alterna. Devuelve el estado final.
CREATE OR REPLACE PROCEDURE sp_sg_telegram_alertas (
    p_chat_id  IN  NUMBER,
    p_activar  IN  CHAR,
    p_estado   OUT CHAR
) AS
BEGIN
    IF p_activar IS NOT NULL AND p_activar NOT IN ('S', 'N') THEN
        RAISE_APPLICATION_ERROR(-20060, 'Usa S para activar o N para desactivar las alertas.');
    END IF;

    UPDATE sg_telegram_vinculo
       SET alertas = NVL(p_activar, CASE alertas WHEN 'S' THEN 'N' ELSE 'S' END)
     WHERE chat_id = p_chat_id
    RETURNING alertas INTO p_estado;

    IF SQL%ROWCOUNT = 0 THEN
        RAISE_APPLICATION_ERROR(-20061, 'Este chat no está vinculado a una cuenta de SGAPE.');
    END IF;
END sp_sg_telegram_alertas;
/

-- Permisos del bot (se omiten si SGAPE_BOT todavia no existe)
DECLARE
  v_existe NUMBER;
  PROCEDURE dar(p_privilegio VARCHAR2, p_objeto VARCHAR2) IS
  BEGIN
    EXECUTE IMMEDIATE 'GRANT ' || p_privilegio || ' ON ' || p_objeto || ' TO sgape_bot';
    EXECUTE IMMEDIATE 'CREATE OR REPLACE SYNONYM sgape_bot.' || p_objeto || ' FOR ' || USER || '.' || p_objeto;
    DBMS_OUTPUT.PUT_LINE('OK ' || p_privilegio || ' ' || p_objeto);
  END;
BEGIN
  SELECT COUNT(*) INTO v_existe FROM all_users WHERE username = 'SGAPE_BOT';
  IF v_existe = 0 THEN
    DBMS_OUTPUT.PUT_LINE('SGAPE_BOT no existe todavía: los permisos los da sgape_v2_usuario_bot.sql.');
    RETURN;
  END IF;
  dar('SELECT', 'VW_SG_TELEGRAM_ALERTAS');
  dar('EXECUTE', 'SP_SG_TELEGRAM_ALERTAS');
  dar('SELECT', 'SG_POSTULACION');           -- /postulaciones: descargar la hoja de vida (BLOB)
  dar('UPDATE', 'SG_POSTULACION');           -- marcarla "en revisión" al abrirla
END;
/

-- Verificacion
SELECT object_name, object_type, status FROM user_objects
 WHERE object_name IN ('VW_SG_TELEGRAM_ALERTAS', 'SP_SG_TELEGRAM_ALERTAS');
