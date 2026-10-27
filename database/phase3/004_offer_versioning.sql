BEGIN;
ALTER TABLE corporate_offer_letters ADD COLUMN IF NOT EXISTS "ReplacesOfferId" uuid;
ALTER TABLE corporate_offer_letters DROP CONSTRAINT IF EXISTS corporate_offer_letters_status_check;
ALTER TABLE corporate_offer_letters ADD CONSTRAINT corporate_offer_letters_status_check CHECK ("Status" IN ('Draft','Issued','Accepted','Declined','Cancelled','Superseded'));
CREATE UNIQUE INDEX IF NOT EXISTS ix_corporate_offer_letters_replaces_offer ON corporate_offer_letters ("ReplacesOfferId");
ALTER TABLE corporate_offer_letters ADD CONSTRAINT fk_offer_predecessor FOREIGN KEY ("ReplacesOfferId") REFERENCES corporate_offer_letters("Id");
COMMIT;
