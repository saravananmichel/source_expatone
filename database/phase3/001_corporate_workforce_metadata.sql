-- Phase 3 workforce metadata. Apply with your controlled database migration process
-- BEFORE deploying the API that maps these new fields.
-- Safe to rerun; preserves all existing memberships and audit history.
BEGIN;
ALTER TABLE corporate_memberships
  ADD COLUMN IF NOT EXISTS "WorkforceClassification" varchar(20) NOT NULL DEFAULT 'Unknown',
  ADD COLUMN IF NOT EXISTS "Nationality" varchar(100),
  ADD COLUMN IF NOT EXISTS "EmploymentCountry" varchar(100),
  ADD COLUMN IF NOT EXISTS "EmploymentStartDate" date;
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'corporate_memberships_workforce_classification_check'
          AND conrelid = 'corporate_memberships'::regclass
    ) THEN
        ALTER TABLE corporate_memberships
        ADD CONSTRAINT corporate_memberships_workforce_classification_check
        CHECK ("WorkforceClassification" IN ('Local', 'Foreign', 'Unknown'));
    END IF;
END
$$;
COMMIT;
