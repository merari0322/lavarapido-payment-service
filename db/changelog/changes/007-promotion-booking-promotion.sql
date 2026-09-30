--liquibase formatted sql

--changeset merari:pay-007-booking-promotion dbms:mssql
--comment: Which promotion was applied to which booking and how much it discounted. booking_id -> booking.booking (no FK, cross-service)
CREATE TABLE [promotion].booking_promotion (
    booking_promotion_id BIGINT        IDENTITY(1,1) NOT NULL,
    booking_id     BIGINT        NOT NULL,
    promotion_id   INT           NOT NULL,
    applied_amount DECIMAL(12,2) NOT NULL,
    created_at     DATETIME2(3)  NOT NULL CONSTRAINT df_bpromo_created DEFAULT SYSUTCDATETIME(),
    created_by     BIGINT        NULL,
    updated_at     DATETIME2(3)  NULL,
    updated_by     BIGINT        NULL,
    deleted_at     DATETIME2(3)  NULL,
    deleted_by     BIGINT        NULL,
    row_version    INT           NOT NULL CONSTRAINT df_bpromo_rv DEFAULT 1,
    CONSTRAINT pk_booking_promotion PRIMARY KEY (booking_promotion_id),
    CONSTRAINT uq_booking_promotion UNIQUE (booking_id, promotion_id),
    CONSTRAINT fk_booking_promotion_promotion FOREIGN KEY (promotion_id) REFERENCES [promotion].promotion(promotion_id),
    CONSTRAINT ck_bpromo_amount CHECK (applied_amount > 0)
);

CREATE INDEX ix_booking_promotion_booking ON [promotion].booking_promotion (booking_id);
--rollback DROP TABLE [promotion].booking_promotion;
