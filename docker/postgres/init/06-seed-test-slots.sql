-- Seed appointment slots for testing (2026-05-13)
-- Runs on container initialization if it doesn't already exist.

-- Ensure we only seed if providers exist
WITH date_range AS (
  SELECT 
    '2026-05-13'::date as slot_date,
    generate_series(
      '2026-05-13 08:00:00'::timestamp,
      '2026-05-13 17:00:00'::timestamp,
      '30 minutes'::interval
    ) as start_time
)
INSERT INTO app.appointment_slots (
  id, provider_id, slot_date, start_time, end_time, duration_minutes, 
  appointment_type, max_capacity, current_bookings, status, created_at, updated_at
)
SELECT 
  gen_random_uuid(),
  p.id,
  date_range.slot_date,
  date_range.start_time,
  date_range.start_time + interval '30 minutes',
  30,
  'GENERAL_CHECKUP',
  1,
  0,
  'AVAILABLE',
  now(),
  now()
FROM date_range
CROSS JOIN (SELECT id FROM app.providers LIMIT 2) p
ON CONFLICT DO NOTHING;
