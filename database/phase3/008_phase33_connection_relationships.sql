BEGIN;
ALTER TABLE family_requests ADD COLUMN IF NOT EXISTS "Relationship" varchar(40) NOT NULL DEFAULT 'Other';
ALTER TABLE family_requests ADD COLUMN IF NOT EXISTS "AllowVisaApplicationUse" boolean NOT NULL DEFAULT false;
COMMIT;
