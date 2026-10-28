-- Phase 3.3: notification preferences and idempotent in-app delivery foundation.
-- Apply only after reviewing migrations 008 and 009; not executed by this commit.
BEGIN;

CREATE TABLE IF NOT EXISTS notification_preferences (
    "UserId" uuid PRIMARY KEY REFERENCES users("Id") ON DELETE CASCADE,
    "InAppEnabled" boolean NOT NULL DEFAULT true,
    "PushEnabled" boolean NOT NULL DEFAULT false,
    "DocumentEnabled" boolean NOT NULL DEFAULT true,
    "ConnectionEnabled" boolean NOT NULL DEFAULT true,
    "CorporateEnabled" boolean NOT NULL DEFAULT true,
    "SharingEnabled" boolean NOT NULL DEFAULT true,
    "VisaEnabled" boolean NOT NULL DEFAULT true,
    "OccasionEnabled" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "UpdatedAt" timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS notification_deliveries (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL REFERENCES users("Id") ON DELETE CASCADE,
    "ReminderKey" varchar(160) NOT NULL,
    "Channel" varchar(16) NOT NULL,
    "OccurrenceDate" date NOT NULL,
    "Title" varchar(250) NOT NULL,
    "Body" text NULL,
    "Status" varchar(16) NOT NULL DEFAULT 'Pending',
    "AttemptCount" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamptz NOT NULL DEFAULT now(),
    "DeliveredAt" timestamptz NULL,
    CONSTRAINT notification_delivery_channel_check CHECK ("Channel" IN ('InApp', 'Push')),
    CONSTRAINT notification_delivery_status_check CHECK ("Status" IN ('Pending', 'Delivered', 'Failed')),
    CONSTRAINT notification_delivery_attempt_check CHECK ("AttemptCount" >= 0),
    CONSTRAINT notification_delivery_unique_occurrence UNIQUE
        ("UserId", "ReminderKey", "Channel", "OccurrenceDate")
);

CREATE INDEX IF NOT EXISTS ix_notification_deliveries_user_created
    ON notification_deliveries ("UserId", "CreatedAt" DESC);
CREATE INDEX IF NOT EXISTS ix_notification_deliveries_pending
    ON notification_deliveries ("Status", "CreatedAt")
    WHERE "Status" = 'Pending';

COMMIT;
