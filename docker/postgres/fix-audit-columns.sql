-- Direct fix for missing audit_records columns
-- Run this if you can't restart the Docker container
-- Can be executed via: psql -U propeliq_user -d propeliq -f fix-audit-columns.sql

ALTER TABLE app.audit_records
ADD COLUMN IF NOT EXISTS override_action character varying(20),
ADD COLUMN IF NOT EXISTS override_constraint_type character varying(50),
ADD COLUMN IF NOT EXISTS override_reason character varying(500);

CREATE INDEX IF NOT EXISTS ix_audit_records_event_type 
ON app.audit_records(event_type);

CREATE INDEX IF NOT EXISTS ix_audit_records_event_type_occurred_at 
ON app.audit_records(event_type, occurred_at DESC);

SELECT 'Audit columns added successfully' as status;
