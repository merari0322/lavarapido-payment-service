--liquibase formatted sql

--changeset merari:pay-004-promotion dbms:mssql
--comment: Promotion: code, discount, eligibility and usage limits
CREATE TABLE [promotion].promotion (
    promotion_id                 INT            IDENTITY(1,1) NOT NULL,
    code                         NVARCHAR(30)   NOT NULL,
    name                         NVARCHAR(100)  NOT NULL,
    [description]                NVARCHAR(300)  NULL,
    discount_type_id             SMALLINT       NOT NULL,
    discount_value               DECIMAL(12,2)  NOT NULL,
    max_discount_amount          DECIMAL(12,2)  NULL,
    min_purchase_amount          DECIMAL(12,2)  NOT NULL CONSTRAINT df_promo_minpurchase DEFAULT 0,
    is_public                    BIT            NOT NULL CONSTRAINT df_promo_public DEFAULT 1,
    min_completed_booking        INT            NOT NULL CONSTRAINT df_promo_minbooking DEFAULT 0,
    valid_from                   DATE           NOT NULL,
    valid_to                     DATE           NOT NULL,
    max_redemptions              INT            NULL,
    max_redemptions_per_customer INT            NULL,
    is_active                    BIT            NOT NULL CONSTRAINT df_promo_active DEFAULT 1,
    created_at                   DATETIME2(3)   NOT NULL CONSTRAINT df_promo_created DEFAULT SYSUTCDATETIME(),
    created_by                   BIGINT         NULL,
    updated_at                   DATETIME2(3)   NULL,
    updated_by                   BIGINT         NULL,
    deleted_at                   DATETIME2(3)   NULL,
    deleted_by                   BIGINT         NULL,
    row_version                  INT            NOT NULL CONSTRAINT df_promo_rv DEFAULT 1,
    CONSTRAINT pk_promotion PRIMARY KEY (promotion_id),
    CONSTRAINT uq_promotion_code UNIQUE (code),
    CONSTRAINT fk_promotion_discount_type FOREIGN KEY (discount_type_id) REFERENCES [promotion].discount_type(discount_type_id),
    CONSTRAINT ck_promo_value CHECK (discount_value > 0),
    CONSTRAINT ck_promo_range CHECK (valid_to >= valid_from),
    CONSTRAINT ck_promo_minbooking CHECK (min_completed_booking >= 0),
    CONSTRAINT ck_promo_max_discount CHECK (max_discount_amount IS NULL OR max_discount_amount > 0),
    CONSTRAINT ck_promo_max_redemptions CHECK (max_redemptions IS NULL OR max_redemptions > 0),
    CONSTRAINT ck_promo_max_per_customer CHECK (max_redemptions_per_customer IS NULL OR max_redemptions_per_customer > 0),
    CONSTRAINT ck_promo_per_customer_le_total CHECK (max_redemptions IS NULL OR max_redemptions_per_customer IS NULL OR max_redemptions_per_customer <= max_redemptions)
);
--rollback DROP TABLE [promotion].promotion;
