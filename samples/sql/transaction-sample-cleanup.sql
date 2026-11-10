-- 使用 psql -v biz_id='本次明确的 BizId' -f transaction-sample-cleanup.sql
-- 仅清理已终结事务的示例业务数据，不删除 DTM 全局事务或屏障记录
\if :{?biz_id}
BEGIN;
DELETE FROM sample_transaction_inventories WHERE biz_id = :'biz_id';
DELETE FROM sample_transaction_orders WHERE biz_id = :'biz_id';
COMMIT;
\else
\echo '必须通过 -v biz_id 指定本次清理的 BizId'
\quit 1
\endif
