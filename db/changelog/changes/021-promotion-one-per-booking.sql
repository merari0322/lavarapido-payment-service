--liquibase formatted sql
-- E-09: una sola promoción canjeada por reserva. El servicio ya lo valida (PROMOTION_ONE_PER_BOOKING);
-- el índice único lo garantiza también con dos canjes simultáneos.
-- Si la base ya tiene reservas con dos cupones, el índice no se crea (CONTINUE: se vuelve a intentar en
-- la siguiente actualización). Para verlas:
--   SELECT booking_id, COUNT(*) FROM [promotion].booking_promotion GROUP BY booking_id HAVING COUNT(*) > 1;

--changeset merari:pay-021-promotion-one-per-booking dbms:mssql
--comment: E-09 una sola promocion por reserva
--preconditions onFail:CONTINUE
--precondition-sql-check expectedResult:0 SELECT COUNT(*) FROM (SELECT booking_id FROM [promotion].booking_promotion GROUP BY booking_id HAVING COUNT(*) > 1) d
CREATE UNIQUE INDEX ux_booking_promotion_one_per_booking ON [promotion].booking_promotion (booking_id);
--rollback DROP INDEX ux_booking_promotion_one_per_booking ON [promotion].booking_promotion;
