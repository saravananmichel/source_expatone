BEGIN;
ALTER TABLE family_requests ADD COLUMN IF NOT EXISTS "Relationship" varchar(40) NOT NULL DEFAULT 'Other';
COMMIT;
