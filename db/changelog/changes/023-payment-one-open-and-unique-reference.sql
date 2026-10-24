--liquibase formatted sql
-- A-16 / BIZ-09 / RF-016: la base garantiza lo que el servicio ya valida, tambien con dos requests
-- simultaneos (doble clic al reportar, dos pestanas):
--   1. un solo pago abierto (PENDING = 1 o IN_REVIEW = 2) por reserva;
--   2. una referencia de transaccion no respalda dos pagos vivos (PENDING, IN_REVIEW o APPROVED,
--      IDs 1 a 3). La referencia esta en payment_receipt y el estado en payment, asi que la
--      unicidad va en una vista indexada que junta ambas. Un pago rechazado o reembolsado ya no
--      cuenta: el cliente puede volver a subir el mismo comprobante despues de un rechazo.
-- El servicio traduce la violacion de cada indice a un 409 con su codigo (EfUnitOfWork):
-- PAYMENT_ALREADY_REPORTED y TRANSACTION_REFERENCE_REUSED.
--
-- Datos existentes:
--   - Si una reserva ya tiene dos pagos abiertos, queda abierto el mas reciente y los anteriores se
--     rechazan como duplicados (primer changeset); asi el indice siempre se puede crear.
--   - Si una referencia ya esta repetida entre pagos vivos no se adivina cual es la buena: la vista
--     no se crea (CONTINUE: se reintenta en el siguiente despliegue y Liquibase lo deja en el log) y
--     el control del servicio sigue funcionando. Para encontrarlas:
--       SELECT r.transaction_reference, COUNT(*) FROM [payment].payment_receipt r
--       JOIN [payment].payment p ON p.payment_id = r.payment_id
--       WHERE r.transaction_reference IS NOT NULL AND r.deleted_at IS NULL AND p.deleted_at IS NULL
--         AND p.payment_status_id <= 3
--       GROUP BY r.transaction_reference HAVING COUNT(*) > 1;

--changeset brayan:pay-023-reject-duplicate-open-payments dbms:mssql
--comment: A-16 precondicion de datos: de dos pagos abiertos de una reserva queda el mas reciente
UPDATE p
SET p.payment_status_id = 4,
    p.rejection_reason = N'Pago duplicado: la reserva ya tenia otro pago en revision.',
    p.processed_at = SYSUTCDATETIME(),
    p.updated_at = SYSUTCDATETIME(),
    p.row_version = p.row_version + 1
FROM [payment].payment p
WHERE p.payment_status_id IN (1, 2)
  AND p.deleted_at IS NULL
  AND EXISTS (
      SELECT 1 FROM [payment].payment newer
      WHERE newer.booking_id = p.booking_id
        AND newer.payment_status_id IN (1, 2)
        AND newer.deleted_at IS NULL
        AND newer.payment_id > p.payment_id);
--rollback SELECT 1;

--changeset brayan:pay-023-payment-one-open-per-booking dbms:mssql
--comment: A-16 un solo pago abierto por reserva (IDs fijos del catalogo, ver 019)
--preconditions onFail:HALT
--precondition-sql-check expectedResult:0 SELECT COUNT(*) FROM (SELECT booking_id FROM [payment].payment WHERE payment_status_id IN (1, 2) AND deleted_at IS NULL GROUP BY booking_id HAVING COUNT(*) > 1) d
CREATE UNIQUE INDEX ux_payment_one_open_per_booking
    ON [payment].payment (booking_id)
    WHERE payment_status_id IN (1, 2) AND deleted_at IS NULL;
--rollback DROP INDEX ux_payment_one_open_per_booking ON [payment].payment;

--changeset brayan:pay-023-live-transaction-reference-unique dbms:mssql
--comment: A-16 referencia de transaccion unica entre pagos vivos (vista indexada)
--preconditions onFail:CONTINUE
--precondition-sql-check expectedResult:0 SELECT COUNT(*) FROM (SELECT r.transaction_reference FROM [payment].payment_receipt r JOIN [payment].payment p ON p.payment_id = r.payment_id WHERE r.transaction_reference IS NOT NULL AND r.deleted_at IS NULL AND p.deleted_at IS NULL AND p.payment_status_id <= 3 GROUP BY r.transaction_reference HAVING COUNT(*) > 1) d
CREATE VIEW [payment].v_live_transaction_reference
WITH SCHEMABINDING
AS
SELECT r.transaction_reference
FROM [payment].payment_receipt r
JOIN [payment].payment p ON p.payment_id = r.payment_id
WHERE r.transaction_reference IS NOT NULL
  AND r.deleted_at IS NULL
  AND p.deleted_at IS NULL
  AND p.payment_status_id <= 3;
CREATE UNIQUE CLUSTERED INDEX ux_live_transaction_reference
    ON [payment].v_live_transaction_reference (transaction_reference);
--rollback DROP VIEW [payment].v_live_transaction_reference;
