--liquibase formatted sql
-- Verifica los catalogos que el codigo C# necesita. No cambia ninguna tabla.
--
-- payment_status se verifica por ID porque el codigo (enum PaymentStatus) y el indice filtrado
-- ux_payment_one_approved_per_booking usan los IDs 1..5 tal cual; la 015 solo revisaba APPROVED.
--
-- discount_type y loyalty_movement_type se verifican solo por CODIGO: SQL Server puede saltar
-- valores IDENTITY tras un reinicio (PACKAGE puede quedar con 102 en vez de 3), asi que el
-- servicio resuelve sus IDs por codigo al arrancar (CatalogIds) en vez de suponerlos fijos.

--changeset merari:pay-019-verify-payment-status-ids dbms:mssql
--comment: PENDING=1 .. REFUNDED=5 (enum PaymentStatus)
--preconditions onFail:HALT
--precondition-sql-check expectedResult:5 SELECT COUNT(*) FROM [payment].payment_status WHERE (payment_status_id = 1 AND code = N'PENDING') OR (payment_status_id = 2 AND code = N'IN_REVIEW') OR (payment_status_id = 3 AND code = N'APPROVED') OR (payment_status_id = 4 AND code = N'REJECTED') OR (payment_status_id = 5 AND code = N'REFUNDED')
SELECT 1;
--rollback SELECT 1;

--changeset merari:pay-019-verify-discount-type-codes dbms:mssql
--comment: PERCENT y FIXED deben existir (son los tipos que el servicio crea)
--preconditions onFail:HALT
--precondition-sql-check expectedResult:2 SELECT COUNT(*) FROM [promotion].discount_type WHERE code IN (N'PERCENT', N'FIXED')
SELECT 1;
--rollback SELECT 1;

--changeset merari:pay-019-verify-loyalty-movement-type-codes dbms:mssql
--comment: EARNED, REDEEMED, EXPIRED y ADJUSTED deben existir
--preconditions onFail:HALT
--precondition-sql-check expectedResult:4 SELECT COUNT(*) FROM [payment].loyalty_movement_type WHERE code IN (N'EARNED', N'REDEEMED', N'EXPIRED', N'ADJUSTED')
SELECT 1;
--rollback SELECT 1;
