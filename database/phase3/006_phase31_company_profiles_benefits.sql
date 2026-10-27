-- Phase 3.1: company branding, reusable benefits, and currency reference data.
-- Run against a staging database first. No existing company or offer rows are modified.
BEGIN;

CREATE TABLE IF NOT EXISTS currencies (
  "Code" char(3) PRIMARY KEY,
  "Name" varchar(120) NOT NULL,
  "NumericCode" varchar(3),
  "MinorUnits" smallint,
  "IsActive" boolean NOT NULL DEFAULT true,
  "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT currencies_code_uppercase CHECK ("Code" ~ '^[A-Z]{3}$'),
  CONSTRAINT currencies_minor_units_check CHECK ("MinorUnits" IS NULL OR "MinorUnits" BETWEEN 0 AND 4)
);

CREATE TABLE IF NOT EXISTS corporate_company_profiles (
  "CompanyId" uuid PRIMARY KEY REFERENCES corporate_companies("Id") ON DELETE CASCADE,
  "LogoObjectKey" text,
  "AddressLine1" varchar(250),
  "AddressLine2" varchar(250),
  "City" varchar(120),
  "State" varchar(120),
  "PostalCode" varchar(30),
  "CountryCode" varchar(2),
  "ContactNumber" varchar(40),
  "ContactEmail" varchar(254),
  "Website" varchar(300),
  "OfferTemplateJson" jsonb NOT NULL DEFAULT '{}'::jsonb,
  "TemplateVersion" integer NOT NULL DEFAULT 1,
  "CreatedAt" timestamptz NOT NULL DEFAULT now(),
  "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT company_profiles_template_version_check CHECK ("TemplateVersion" > 0),
  CONSTRAINT company_profiles_template_object_check CHECK (jsonb_typeof("OfferTemplateJson") = 'object')
);

CREATE TABLE IF NOT EXISTS corporate_benefits (
  "Id" uuid PRIMARY KEY,
  "CompanyId" uuid NOT NULL REFERENCES corporate_companies("Id") ON DELETE RESTRICT,
  "Category" varchar(80) NOT NULL,
  "Name" varchar(160) NOT NULL,
  "Description" text,
  "IsActive" boolean NOT NULL DEFAULT true,
  "CreatedAt" timestamptz NOT NULL DEFAULT now(),
  "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT corporate_benefits_name_not_empty CHECK (length(btrim("Name")) > 0)
);
CREATE INDEX IF NOT EXISTS ix_corporate_benefits_company_active
  ON corporate_benefits ("CompanyId", "IsActive", "Category");

-- Snapshot the terms rather than depending on mutable benefit descriptions.
CREATE TABLE IF NOT EXISTS corporate_offer_benefits (
  "OfferId" uuid NOT NULL REFERENCES corporate_offer_letters("Id") ON DELETE RESTRICT,
  "BenefitId" uuid REFERENCES corporate_benefits("Id") ON DELETE SET NULL,
  "Position" integer NOT NULL,
  "CategorySnapshot" varchar(80) NOT NULL,
  "NameSnapshot" varchar(160) NOT NULL,
  "DescriptionSnapshot" text,
  PRIMARY KEY ("OfferId", "Position"),
  CONSTRAINT corporate_offer_benefits_position_check CHECK ("Position" >= 0)
);
CREATE INDEX IF NOT EXISTS ix_corporate_offer_benefits_benefit
  ON corporate_offer_benefits ("BenefitId");

-- Future electronic-signature workflow: issued and signed PDFs remain separate.
CREATE TABLE IF NOT EXISTS corporate_offer_signatures (
  "Id" uuid PRIMARY KEY,
  "OfferId" uuid NOT NULL UNIQUE REFERENCES corporate_offer_letters("Id") ON DELETE RESTRICT,
  "EmployeeUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
  "SignatureObjectKey" text NOT NULL,
  "SignedDocumentId" uuid REFERENCES documents("Id") ON DELETE RESTRICT,
  "SignedAt" timestamptz NOT NULL,
  "SignatureMethod" varchar(30) NOT NULL DEFAULT 'Drawn',
  "OfferVersion" integer NOT NULL,
  "CreatedAt" timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT corporate_offer_signatures_version_check CHECK ("OfferVersion" > 0)
);
COMMIT;
