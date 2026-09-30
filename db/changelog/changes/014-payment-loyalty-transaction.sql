--liquibase formatted sql

--changeset merari:pay-014-loyalty-transaction dbms:mssql
--comment: Loyalty points ledger, source of truth for balances. customer_id -> customer.customer, booking_id -> booking.booking (no FK, cross-service)
CREATE TABLE [payment].loyalty_transaction (
    loyalty_transaction_id   BIGINT        IDENTITY(1,1) NOT NULL,
    customer_id              BIGINT        NOT NULL,
    loyalty_movement_type_id SMALLINT      NOT NULL,
    booking_id               BIGINT        NULL,
    points                   INT           NOT NULL,
    balance_after            INT           NOT NULL,
    expires_on               DATE          NULL,
    [description]            NVARCHAR(200) NULL,
    created_at               DATETIME2(3)  NOT NULL CONSTRAINT df_ltx_created DEFAULT SYSUTCDATETIME(),
    created_by               BIGINT        NULL,
    updated_at               DATETIME2(3)  NULL,
    updated_by               BIGINT        NULL,
    deleted_at               DATETIME2(3)  NULL,
    deleted_by               BIGINT        NULL,
    row_version              INT           NOT NULL CONSTRAINT df_ltx_rv DEFAULT 1,
    CONSTRAINT pk_loyalty_transaction PRIMARY KEY (loyalty_transaction_id),
    CONSTRAINT fk_loyalty_transaction_type FOREIGN KEY (loyalty_movement_type_id) REFERENCES [payment].loyalty_movement_type(loyalty_movement_type_id),
    CONSTRAINT ck_loyalty_points CHECK (points <> 0),
    CONSTRAINT ck_loyalty_balance CHECK (balance_after >= 0)
);

CREATE INDEX ix_loyalty_transaction_customer ON [payment].loyalty_transaction (customer_id, created_at);
CREATE INDEX ix_loyalty_transaction_booking  ON [payment].loyalty_transaction (booking_id);
--rollback DROP TABLE [payment].loyalty_transaction;
