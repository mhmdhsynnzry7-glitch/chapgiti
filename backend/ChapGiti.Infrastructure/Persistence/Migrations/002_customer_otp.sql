CREATE TABLE dbo.Customer_Otp_Challenges (
    id char(64) NOT NULL PRIMARY KEY,
    phone varchar(11) NOT NULL,
    purpose varchar(10) NOT NULL,
    customer_id varchar(36) NULL REFERENCES dbo.Customer_Registrations(id),
    code_hash char(64) NULL,
    created_at bigint NOT NULL,
    expires bigint NOT NULL,
    attempts int NOT NULL DEFAULT 0,
    consumed_at bigint NULL,
    delivery_status varchar(12) NOT NULL DEFAULT 'pending',
    CONSTRAINT CK_OtpPurpose CHECK (purpose IN ('login','reset')),
    CONSTRAINT CK_OtpAttempts CHECK (attempts BETWEEN 0 AND 5),
    CONSTRAINT CK_OtpDelivery CHECK (delivery_status IN ('pending','sent','failed'))
);
CREATE INDEX IX_Otp_Phone_Created ON dbo.Customer_Otp_Challenges(phone,created_at DESC);
CREATE TABLE dbo.Customer_Password_Resets (
    token_hash char(64) NOT NULL PRIMARY KEY,
    customer_id varchar(36) NOT NULL REFERENCES dbo.Customer_Registrations(id),
    created_at bigint NOT NULL,
    expires bigint NOT NULL,
    consumed_at bigint NULL
);
CREATE INDEX IX_PasswordReset_Customer ON dbo.Customer_Password_Resets(customer_id,expires);
