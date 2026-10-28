BEGIN;
CREATE TABLE IF NOT EXISTS family_occasions (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES users("Id"),
    "ConnectionId" uuid NULL REFERENCES family_requests("Id") ON DELETE SET NULL,
    "Title" varchar(200) NOT NULL,
    "Kind" varchar(32) NOT NULL DEFAULT 'Custom',
    "OccasionDate" timestamp with time zone NOT NULL,
    "RepeatsAnnually" boolean NOT NULL DEFAULT true,
    "RemindersEnabled" boolean NOT NULL DEFAULT true,
    "DaysBefore" integer NOT NULL DEFAULT 7,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    "UpdatedAt" timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT family_occasions_days_check CHECK ("DaysBefore" IN (1, 3, 7, 30)),
    CONSTRAINT family_occasions_kind_check CHECK ("Kind" IN ('Birthday', 'Anniversary', 'Relocation', 'Custom'))
);
CREATE INDEX IF NOT EXISTS ix_family_occasions_user_date ON family_occasions ("UserId", "OccasionDate");
COMMIT;
