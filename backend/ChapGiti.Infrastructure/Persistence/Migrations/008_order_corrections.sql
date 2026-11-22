-- Additive correction/file history. Existing orders, files and accepted quotes remain intact.
ALTER TABLE dbo.Orders ADD approved_file_id varchar(36) NULL;
ALTER TABLE dbo.Files ADD correction_id varchar(36) NULL, file_version int NULL;
CREATE UNIQUE INDEX UX_Files_OrderReference ON dbo.Files(order_id,id);

CREATE TABLE dbo.Order_Corrections (
    id varchar(36) NOT NULL CONSTRAINT PK_OrderCorrections PRIMARY KEY,
    order_id varchar(36) NOT NULL REFERENCES dbo.Orders(id),
    revision int NOT NULL,
    state varchar(16) NOT NULL,
    is_active bit NOT NULL,
    request_text nvarchar(900) NOT NULL,
    requires_new_pdf bit NOT NULL,
    requested_by_admin nvarchar(254) NULL REFERENCES dbo.Admin_Users(email),
    requested_at datetime2(3) NULL,
    is_legacy bit NOT NULL CONSTRAINT DF_OrderCorrections_Legacy DEFAULT 0,
    base_file_id varchar(36) NULL,
    submission_text nvarchar(3000) NULL,
    submitted_by_customer varchar(36) NULL REFERENCES dbo.Customer_Registrations(id),
    submitted_at datetime2(3) NULL,
    file_id varchar(36) NULL,
    reviewed_by_admin nvarchar(254) NULL REFERENCES dbo.Admin_Users(email),
    review_reason nvarchar(900) NULL,
    reviewed_at datetime2(3) NULL,
    approved_file_id varchar(36) NULL,
    version int NOT NULL CONSTRAINT DF_OrderCorrections_Version DEFAULT 1,
    CONSTRAINT UQ_OrderCorrections_Revision UNIQUE(order_id,revision),
    CONSTRAINT UQ_OrderCorrections_OrderReference UNIQUE(order_id,id),
    CONSTRAINT FK_OrderCorrections_BaseFile FOREIGN KEY(order_id,base_file_id) REFERENCES dbo.Files(order_id,id),
    CONSTRAINT FK_OrderCorrections_SubmittedFile FOREIGN KEY(order_id,file_id) REFERENCES dbo.Files(order_id,id),
    CONSTRAINT FK_OrderCorrections_ApprovedFile FOREIGN KEY(order_id,approved_file_id) REFERENCES dbo.Files(order_id,id),
    CONSTRAINT CK_OrderCorrections_Number CHECK(revision>0 AND version>0),
    CONSTRAINT CK_OrderCorrections_Request CHECK(LEN(LTRIM(RTRIM(request_text)))>=2),
    CONSTRAINT CK_OrderCorrections_RequestActor CHECK(is_legacy=1 OR (requested_by_admin IS NOT NULL AND requested_at IS NOT NULL)),
    CONSTRAINT CK_OrderCorrections_State CHECK(
        (state IN('requested','submitted') AND is_active=1) OR
        (state IN('approved','rejected','superseded') AND is_active=0)),
    CONSTRAINT CK_OrderCorrections_Submission CHECK(state NOT IN('submitted','approved','rejected') OR
        (submitted_at IS NOT NULL AND submitted_by_customer IS NOT NULL AND submission_text IS NOT NULL AND LEN(LTRIM(RTRIM(submission_text)))>=2)),
    CONSTRAINT CK_OrderCorrections_RequiredPdf CHECK(requires_new_pdf=0 OR state NOT IN('submitted','approved','rejected') OR file_id IS NOT NULL),
    CONSTRAINT CK_OrderCorrections_Review CHECK(state NOT IN('approved','rejected') OR
        (reviewed_at IS NOT NULL AND reviewed_by_admin IS NOT NULL)),
    CONSTRAINT CK_OrderCorrections_Rejection CHECK(state<>'rejected' OR (review_reason IS NOT NULL AND LEN(LTRIM(RTRIM(review_reason)))>=2)),
    CONSTRAINT CK_OrderCorrections_Approval CHECK(state='approved' OR approved_file_id IS NULL)
);
CREATE UNIQUE INDEX UX_OrderCorrections_Active ON dbo.Order_Corrections(order_id) WHERE is_active=1;
CREATE INDEX IX_OrderCorrections_History ON dbo.Order_Corrections(order_id,revision DESC);

-- Dynamic batches bind the columns only after ALTER TABLE has added them.
EXEC(N'ALTER TABLE dbo.Files ADD CONSTRAINT FK_Files_Correction FOREIGN KEY(order_id,correction_id) REFERENCES dbo.Order_Corrections(order_id,id);');
EXEC(N'ALTER TABLE dbo.Files ADD CONSTRAINT CK_Files_Version CHECK(file_version IS NULL OR file_version>0);');
EXEC(N'CREATE UNIQUE INDEX UX_Files_OrderVersion ON dbo.Files(order_id,file_version) WHERE file_version IS NOT NULL;');
EXEC(N'ALTER TABLE dbo.Orders ADD CONSTRAINT FK_Orders_ApprovedFile FOREIGN KEY(id,approved_file_id) REFERENCES dbo.Files(order_id,id);');

-- Accepted versions are immutable historical evidence. Only the latest quote is
-- actionable; the order lock and command receipts serialize each new decision.
DROP INDEX UX_OrderQuotes_Accepted ON dbo.Order_Quotes;
CREATE INDEX IX_OrderQuotes_AcceptedHistory ON dbo.Order_Quotes(order_id,revision DESC) WHERE state='accepted';

-- Preserve outstanding pre-008 information requests without inventing an author
-- or request time. Existing attachments do not become new corrected PDFs.
INSERT dbo.Order_Corrections(id,order_id,revision,state,is_active,request_text,requires_new_pdf,is_legacy,base_file_id)
SELECT LOWER(CONVERT(varchar(36),NEWID())),o.id,1,'requested',1,o.information_request,o.information_requires_file,1,
    (SELECT TOP(1) f.id FROM dbo.Files f WHERE f.order_id=o.id ORDER BY f.created_at DESC,f.id DESC)
FROM dbo.Orders o WHERE LEN(LTRIM(RTRIM(o.information_request)))>=2;
