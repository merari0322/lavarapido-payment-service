--liquibase formatted sql
-- BIZ-05: el canje de un cupon porcentual guarda su porcentaje (y su tope en pesos, si la promocion
-- tenia uno) para recalcular el descuento con el total actual de la reserva. Antes se guardaba solo
-- el monto en pesos del dia del canje: 50 % sobre 40.000 y reprogramar a 20.000 dejaba 0 por pagar.
-- applied_amount se conserva tal cual (es lo que desconto el dia del canje); para los cupones de
-- monto fijo discount_percent queda NULL y se sigue descontando applied_amount.

--changeset brayan:pay-022-redemption-discount-percent-columns dbms:mssql
--comment: BIZ-05 porcentaje y tope del cupon en el canje
--preconditions onFail:MARK_RAN
--precondition-sql-check expectedResult:0 SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('promotion.booking_promotion') AND name = 'discount_percent'
ALTER TABLE [promotion].booking_promotion ADD
    discount_percent    DECIMAL(5,2)  NULL
        CONSTRAINT ck_bpromo_discount_percent CHECK (discount_percent IS NULL OR (discount_percent > 0 AND discount_percent <= 100)),
    max_discount_amount DECIMAL(12,2) NULL
        CONSTRAINT ck_bpromo_max_discount CHECK (max_discount_amount IS NULL OR max_discount_amount > 0);
--rollback ALTER TABLE [promotion].booking_promotion DROP CONSTRAINT ck_bpromo_discount_percent, ck_bpromo_max_discount;
--rollback ALTER TABLE [promotion].booking_promotion DROP COLUMN discount_percent, max_discount_amount;

--changeset brayan:pay-022-redemption-discount-percent-backfill dbms:mssql
--comment: Los canjes existentes de cupones porcentuales toman el porcentaje y el tope de su promocion
UPDATE bp
SET bp.discount_percent = p.discount_value,
    bp.max_discount_amount = p.max_discount_amount
FROM [promotion].booking_promotion bp
JOIN [promotion].promotion p ON p.promotion_id = bp.promotion_id
JOIN [promotion].discount_type t ON t.discount_type_id = p.discount_type_id
WHERE t.code = N'PERCENT' AND bp.discount_percent IS NULL AND p.discount_value <= 100;
--rollback UPDATE [promotion].booking_promotion SET discount_percent = NULL, max_discount_amount = NULL;
