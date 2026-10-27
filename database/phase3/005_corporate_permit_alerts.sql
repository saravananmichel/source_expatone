-- Durable in-app alerts for issued permits. Apply after 003_corporate_issued_permits.sql.
BEGIN;
CREATE TABLE IF NOT EXISTS corporate_permit_alerts (
 "Id" uuid PRIMARY KEY,
 "CompanyId" uuid NOT NULL REFERENCES corporate_companies("Id") ON DELETE RESTRICT,
 "PermitId" uuid NOT NULL REFERENCES corporate_issued_permits("Id") ON DELETE RESTRICT,
 "ThresholdDays" integer NOT NULL,
 "ExpiryDate" date NOT NULL,
 "AcknowledgedAt" timestamptz,
 "AcknowledgedByUserId" uuid REFERENCES users("Id") ON DELETE RESTRICT,
 "CreatedAt" timestamptz NOT NULL DEFAULT now(),
 "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
 CONSTRAINT corporate_permit_alerts_threshold CHECK ("ThresholdDays" IN (0, 7, 30, 60, 90))
);
CREATE UNIQUE INDEX IF NOT EXISTS ix_corporate_permit_alerts_permit_threshold_expiry
 ON corporate_permit_alerts ("PermitId","ThresholdDays","ExpiryDate");
CREATE INDEX IF NOT EXISTS ix_corporate_permit_alerts_company_ack
 ON corporate_permit_alerts ("CompanyId","AcknowledgedAt");
COMMIT;
