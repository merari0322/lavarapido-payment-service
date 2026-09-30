--liquibase formatted sql

--changeset merari:pay-011-payment dbms:mssql
--comment: One payment attempt for a booking. booking_id -> booking.booking, approved_by -> security.app_user (no FK, cross-service)
CREATE TABLE [payment].payment (
    payment_id         BIGINT        IDENTITY(1,1) NOT NULL,
    booking_id         BIGINT        NOT NULL,
    payment_account_id SMALLINT      NOT NULL,
    payment_status_id  SMALLINT      NOT NULL,
    amount             DECIMAL(12,2) NOT NULL,
    processed_at       DATETIME2(3)  NULL,
    approved_by        BIGINT        NULL,
    rejection_reason   NVARCHAR(200) NULL,
    created_at         DATETIME2(3)  NOT NULL CONSTRAINT df_payment_created DEFAULT SYSUTCDATETIME(),
    created_by         BIGINT        NULL,
    updated_at         DATETIME2(3)  NULL,
    updated_by         BIGINT        NULL,
    deleted_at         DATETIME2(3)  NULL,
    deleted_by         BIGINT        NULL,
    row_version        INT           NOT NULL CONSTRAINT df_payment_rv DEFAULT 1,
    CONSTRAINT pk_payment PRIMARY KEY (payment_id),
    CONSTRAINT fk_payment_account FOREIGN KEY (payment_account_id) REFERENCES [payment].payment_account(payment_account_id),
    CONSTRAINT fk_payment_status FOREIGN KEY (payment_status_id) REFERENCES [payment].payment_status(payment_status_id),
    CONSTRAINT ck_payment_amount CHECK (amount > 0)
);

CREATE INDEX ix_payment_booking     ON [payment].payment (booking_id);
CREATE INDEX ix_payment_status      ON [payment].payment (payment_status_id);
CREATE INDEX ix_payment_approved_by ON [payment].payment (approved_by);
CREATE UNIQUE INDEX ux_payment_one_approved_per_booking
    ON [payment].payment (booking_id)
    WHERE payment_status_id = 3 AND deleted_at IS NULL;
--rollback DROP TABLE [payment].payment;
