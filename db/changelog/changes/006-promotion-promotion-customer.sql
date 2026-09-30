--liquibase formatted sql

--changeset merari:pay-006-promotion-customer dbms:mssql
--comment: Assigns a promotion to one customer. customer_id -> customer.customer, assigned_by -> security.app_user (no FK, cross-service)
CREATE TABLE [promotion].promotion_customer (
    promotion_customer_id BIGINT       IDENTITY(1,1) NOT NULL,
    promotion_id INT          NOT NULL,
    customer_id  BIGINT       NOT NULL,
    assigned_at  DATETIME2(3) NOT NULL CONSTRAINT df_pcust_assigned DEFAULT SYSUTCDATETIME(),
    assigned_by  BIGINT       NULL,
    created_at   DATETIME2(3) NOT NULL CONSTRAINT df_pcust_created DEFAULT SYSUTCDATETIME(),
    created_by   BIGINT       NULL,
    updated_at   DATETIME2(3) NULL,
    updated_by   BIGINT       NULL,
    deleted_at   DATETIME2(3) NULL,
    deleted_by   BIGINT       NULL,
    row_version  INT          NOT NULL CONSTRAINT df_pcust_rv DEFAULT 1,
    CONSTRAINT pk_promotion_customer PRIMARY KEY (promotion_customer_id),
    CONSTRAINT uq_promotion_customer UNIQUE (promotion_id, customer_id),
    CONSTRAINT fk_promotion_customer_promotion FOREIGN KEY (promotion_id) REFERENCES [promotion].promotion(promotion_id)
);

CREATE INDEX ix_promotion_customer_customer    ON [promotion].promotion_customer (customer_id);
CREATE INDEX ix_promotion_customer_assigned_by ON [promotion].promotion_customer (assigned_by);
--rollback DROP TABLE [promotion].promotion_customer;
