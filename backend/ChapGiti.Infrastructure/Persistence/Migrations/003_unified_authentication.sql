-- Additive, reviewed source only. Apply explicitly during a separately authorized rollout.
-- Existing customer/order/ticket identifiers and legacy administrator records are preserved.
ALTER TABLE dbo.Customer_Otp_Challenges DROP CONSTRAINT CK_OtpPurpose;
ALTER TABLE dbo.Customer_Otp_Challenges ALTER COLUMN purpose varchar(16) NOT NULL;
ALTER TABLE dbo.Customer_Otp_Challenges ADD CONSTRAINT CK_OtpPurpose
    CHECK (purpose IN ('login','reset','register','verify-phone'));

CREATE TABLE dbo.Pending_Customer_Registrations (
    challenge_id char(64) NOT NULL PRIMARY KEY REFERENCES dbo.Customer_Otp_Challenges(id) ON DELETE CASCADE,
    first_name nvarchar(80) NOT NULL,
    last_name nvarchar(80) NOT NULL,
    password_hash varchar(512) NOT NULL
);

CREATE TABLE dbo.Customer_Roles (
    customer_id varchar(36) NOT NULL REFERENCES dbo.Customer_Accounts(registration_id),
    role varchar(20) NOT NULL,
    assigned_by nvarchar(254) NOT NULL,
    assigned_at datetime2(3) NOT NULL,
    PRIMARY KEY (customer_id,role),
    CONSTRAINT CK_CustomerRole CHECK (role='admin')
);

CREATE TABLE dbo.Auth_Sessions (
    id varchar(36) NOT NULL PRIMARY KEY,
    customer_id varchar(36) NULL REFERENCES dbo.Customer_Accounts(registration_id),
    legacy_admin_email nvarchar(254) NULL REFERENCES dbo.Admin_Users(email),
    access_hash char(64) NOT NULL UNIQUE,
    access_expires bigint NOT NULL,
    created_at bigint NOT NULL,
    expires bigint NOT NULL,
    revoked_at bigint NULL,
    CONSTRAINT CK_AuthPrincipal CHECK (
        (customer_id IS NOT NULL AND legacy_admin_email IS NULL) OR
        (customer_id IS NULL AND legacy_admin_email IS NOT NULL))
);
CREATE INDEX IX_AuthSession_Customer ON dbo.Auth_Sessions(customer_id,revoked_at);
CREATE INDEX IX_AuthSession_Admin ON dbo.Auth_Sessions(legacy_admin_email,revoked_at);
CREATE TABLE dbo.Auth_Refresh_Credentials (
    token_hash char(64) NOT NULL PRIMARY KEY,
    session_id varchar(36) NOT NULL REFERENCES dbo.Auth_Sessions(id),
    created_at bigint NOT NULL,
    expires bigint NOT NULL,
    consumed_at bigint NULL
);
CREATE INDEX IX_Refresh_Session ON dbo.Auth_Refresh_Credentials(session_id);
-- Keep consumed refresh digests until family expiry for reuse detection.
-- Do not infer administrator roles from phone/email/name and do not copy old session tokens.
