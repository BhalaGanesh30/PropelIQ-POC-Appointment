-- PropelIQ PostgreSQL Extension Initialization
-- Executed automatically on first container start by Docker entrypoint.
-- Script name prefix 01 ensures this runs before schema creation (02-*).
--
-- AC-2: pgvector extension must be active after container initialization.
-- DR-001: uuid-ossp provides UUID generation for primary keys.
-- pg_trgm supports trigram-based fuzzy text search for clinician name lookup.

-- pgvector: enables vector similarity search for AI embedding storage.
-- Bundled with pgvector/pgvector:pg15 image — no compilation required.
CREATE EXTENSION IF NOT EXISTS vector;

-- uuid-ossp: provides uuid_generate_v4() for UUID primary keys.
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- pg_trgm: enables trigram index support for fast ILIKE / similarity queries.
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- Verify extensions are active (logged to container stdout for diagnostics).
DO $$
DECLARE
  ext_count INT;
BEGIN
  SELECT COUNT(*) INTO ext_count
  FROM pg_extension
  WHERE extname IN ('vector', 'uuid-ossp', 'pg_trgm');

  IF ext_count < 3 THEN
    RAISE EXCEPTION 'Extension initialization failed: expected 3, found %', ext_count;
  END IF;

  RAISE NOTICE 'All % extensions initialized successfully.', ext_count;
END $$;
