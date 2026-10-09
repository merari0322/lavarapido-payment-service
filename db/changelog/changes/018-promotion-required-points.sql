--liquibase formatted sql
-- Umbral de puntos de fidelizacion que el cliente necesita acumulado (payment.loyalty_transaction,
-- balance_after de su ultimo movimiento) para que esta promocion se le desbloquee y la pueda
-- canjear. 0 significa que no depende de puntos (sigue disponible solo por fecha/publico/limites).

--changeset merari:pay-018-promotion-required-points dbms:mssql
--comment: Puntos minimos acumulados para desbloquear la promocion
--preconditions onFail:MARK_RAN
--precondition-sql-check expectedResult:0 SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('promotion.promotion') AND name = 'required_points'
ALTER TABLE [promotion].promotion ADD required_points INT NOT NULL CONSTRAINT df_promo_required_points DEFAULT 0;
--rollback ALTER TABLE [promotion].promotion DROP COLUMN required_points;

--changeset merari:pay-018-promotion-required-points-check dbms:mssql
--comment: No puede ser negativo
ALTER TABLE [promotion].promotion ADD CONSTRAINT ck_promo_required_points CHECK (required_points >= 0);
--rollback ALTER TABLE [promotion].promotion DROP CONSTRAINT ck_promo_required_points;
