-- Explicit HR-recorded issued permits; never infer expiry from visa application drafts.
BEGIN;
CREATE TABLE IF NOT EXISTS corporate_issued_permits (
    "Id" uuid PRIMARY KEY,
    "CompanyId" uuid NOT NULL REFERENCES corporate_companies("Id") ON DELETE RESTRICT,
    "EmployeeUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
    "RecordedByUserId" uuid NOT NULL REFERENCES users("Id") ON DELETE RESTRICT,
    "CountryCode" varchar(8) NOT NULL,
    "PermitType" varchar(100) NOT NULL,
    "Status" varchar(24) NOT NULL DEFAULT 'Active',
    "IssuedOn" date,
    "ExpiresOn" date NOT NULL,
    "Reference" varchar(120),
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt" timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT corporate_issued_permits_status_check CHECK ("Status" IN ('Active','Expired','Cancelled','Superseded'))
);
CREATE INDEX IF NOT EXISTS ix_corporate_issued_permits_company_employee_expires
 ON corporate_issued_permits ("CompanyId","EmployeeUserId","ExpiresOn");
COMMIT;
