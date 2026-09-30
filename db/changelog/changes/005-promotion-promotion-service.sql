--liquibase formatted sql

--changeset merari:pay-005-promotion-service dbms:mssql
--comment: Restricts a promotion to specific services. service_id points to catalog.service (no FK, cross-service)
CREATE TABLE [promotion].promotion_service (
    promotion_service_id INT          IDENTITY(1,1) NOT NULL,
    promotion_id INT          NOT NULL,
    service_id   INT          NOT NULL,
    created_at   DATETIME2(3) NOT NULL CONSTRAINT df_psvc_created DEFAULT SYSUTCDATETIME(),
    created_by   BIGINT       NULL,
    updated_at   DATETIME2(3) NULL,
    updated_by   BIGINT       NULL,
    deleted_at   DATETIME2(3) NULL,
    deleted_by   BIGINT       NULL,
    row_version  INT          NOT NULL CONSTRAINT df_psvc_rv DEFAULT 1,
    CONSTRAINT pk_promotion_service PRIMARY KEY (promotion_service_id),
    CONSTRAINT uq_promotion_service UNIQUE (promotion_id, service_id),
    CONSTRAINT fk_promotion_service_promotion FOREIGN KEY (promotion_id) REFERENCES [promotion].promotion(promotion_id)
);

CREATE INDEX ix_promotion_service_service ON [promotion].promotion_service (service_id);
--rollback DROP TABLE [promotion].promotion_service;
