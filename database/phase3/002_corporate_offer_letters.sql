-- Phase 3 offer-letter records. Apply through the controlled DB migration process.
-- Do not deploy offer-letter APIs before this table exists.
BEGIN;
CREATE TABLE IF NOT EXISTS corporate_offer_letters (
    "Id" uuid PRIMARY KEY,
    "CompanyId" uuid NOT NULL REFERENCES corporate_companies("Id") ON DELETE RESTRICT,
    "EmployeeUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
    "CreatedByUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
    "Version" integer NOT NULL DEFAULT 1,
    "Status" varchar(24) NOT NULL DEFAULT 'Draft',
    "TemplateDataJson" text NOT NULL DEFAULT '{}',
    "DocumentId" uuid NULL REFERENCES documents("Id") ON DELETE RESTRICT,
    "IssuedAt" timestamptz NULL,
    "RespondedAt" timestamptz NULL,
    "Response" varchar(24) NULL,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT corporate_offer_letters_version_positive CHECK ("Version" > 0),
    CONSTRAINT corporate_offer_letters_status_check CHECK ("Status" IN ('Draft','Issued','Accepted','Declined','Cancelled')),
    CONSTRAINT corporate_offer_letters_response_check CHECK ("Response" IS NULL OR "Response" IN ('Accepted','Declined')),
    CONSTRAINT corporate_offer_letters_issue_document_check CHECK ("Status" = 'Draft' OR "DocumentId" IS NOT NULL)
);
CREATE INDEX IF NOT EXISTS ix_corporate_offer_letters_company_employee_created
    ON corporate_offer_letters ("CompanyId", "EmployeeUserId", "CreatedAt");
CREATE INDEX IF NOT EXISTS ix_corporate_offer_letters_document
    ON corporate_offer_letters ("DocumentId");
COMMIT;
