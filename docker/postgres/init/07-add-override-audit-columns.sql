-- Add missing audit record columns (from migration 20260506134124_AddOverrideAuditColumns)
-- Runs on container initialization if columns don't already exist.

-- Add override_action column if it doesn't exist
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT FROM information_schema.columns 
    WHERE table_schema = 'app' 
    AND table_name = 'audit_records' 
    AND column_name = 'override_action'
  ) THEN
    ALTER TABLE app.audit_records
    ADD COLUMN override_action character varying(20);
  END IF;
END $$;

-- Add override_constraint_type column if it doesn't exist
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT FROM information_schema.columns 
    WHERE table_schema = 'app' 
    AND table_name = 'audit_records' 
    AND column_name = 'override_constraint_type'
  ) THEN
    ALTER TABLE app.audit_records
    ADD COLUMN override_constraint_type character varying(50);
  END IF;
END $$;

-- Add override_reason column if it doesn't exist
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT FROM information_schema.columns 
    WHERE table_schema = 'app' 
    AND table_name = 'audit_records' 
    AND column_name = 'override_reason'
  ) THEN
    ALTER TABLE app.audit_records
    ADD COLUMN override_reason character varying(500);
  END IF;
END $$;

-- Create indices if they don't exist
CREATE INDEX IF NOT EXISTS ix_audit_records_event_type 
ON app.audit_records(event_type);

CREATE INDEX IF NOT EXISTS ix_audit_records_event_type_occurred_at 
ON app.audit_records(event_type, occurred_at DESC);
