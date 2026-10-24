--liquibase formatted sql
-- La pantalla de Promociones (web y movil) vende un paquete a precio fijo con su propia duracion,
-- icono y lista de beneficios, no un cupon de descuento porcentual/fijo sobre otra compra. Se
-- agregan esos campos; discount_type_id/discount_value se quedan (son NOT NULL) y para estos
-- paquetes se guardan como PACKAGE + el mismo precio, solo para cumplir esa regla existente.

--changeset merari:pay-017-discount-type-package dbms:mssql
--comment: Tercer tipo: el "descuento" es en realidad el precio fijo del paquete
INSERT INTO [promotion].discount_type (code, name, display_order) VALUES (N'PACKAGE', N'Paquete a precio fijo', 3);
--rollback DELETE FROM [promotion].discount_type WHERE code = N'PACKAGE';

--changeset merari:pay-017-promotion-package-columns dbms:mssql
--comment: Precio, duracion, icono, destacado y beneficios del paquete (admin, Gestion > Promociones)
ALTER TABLE [promotion].promotion ADD
    price             DECIMAL(12,2) NULL,
    duration_minutes  INT           NULL,
    icon              NVARCHAR(40)  NULL,
    featured          BIT           NOT NULL CONSTRAINT df_promo_featured DEFAULT 0,
    -- una linea por beneficio (salto de linea); es solo texto para mostrar, sin reglas propias
    benefits          NVARCHAR(600) NULL;
--rollback ALTER TABLE [promotion].promotion DROP COLUMN price, duration_minutes, icon, featured, benefits;
