using ChapGiti.Application.Common;
using Microsoft.Data.SqlClient;

namespace ChapGiti.SqlAcceptanceTests;

internal static class OrderWorkflowSchemaChecks
{
    public static async Task Run(IsolatedDatabase database,AcceptanceChecks checks)
    {
        foreach(var (table,column,type,length,nullable) in new[]{
            ("Order_Corrections","id","varchar",36,0),("Order_Corrections","request_text","nvarchar",1800,0),
            ("Order_Corrections","submission_text","nvarchar",6000,1),("Order_Corrections","requires_new_pdf","bit",1,0),
            ("Order_Corrections","requested_by_admin","nvarchar",508,1),("Order_Corrections","version","int",4,0),
            ("Files","file_version","int",4,1),("Files","correction_id","varchar",36,1),
            ("Orders","approved_file_id","varchar",36,1),("Order_Views","seen_through_sequence","bigint",8,0),
            ("Order_Views","customer_id","varchar",36,1),("Order_Views","admin_email","nvarchar",508,1)})
            checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.columns WHERE object_id=OBJECT_ID(@p0) AND name=@p1 AND TYPE_NAME(user_type_id)=@p2 AND max_length=@p3 AND is_nullable=@p4","dbo."+table,column,type,length,nullable))!.Num("n")==1,"actual SQL type/nullability: "+table+"."+column);
        checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.foreign_keys WHERE name IN('FK_OrderCorrections_BaseFile','FK_OrderCorrections_SubmittedFile','FK_OrderCorrections_ApprovedFile','FK_Files_Correction','FK_Orders_ApprovedFile') AND delete_referential_action=0 AND is_not_trusted=0 AND is_disabled=0"))!.Num("n")==5,"all composite file/correction foreign keys are trusted and non-cascading");
        checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.Order_Quotes') AND ((name='IX_OrderQuotes_AcceptedHistory' AND is_unique=0 AND has_filter=1) OR name='UX_OrderQuotes_Accepted')"))!.Num("n")==1,"accepted quote history replaces one-acceptance uniqueness without rewriting records");
        checks.Check((await database.Read("SELECT COUNT(*) n FROM sys.check_constraints WHERE parent_object_id IN(OBJECT_ID('dbo.Order_Corrections'),OBJECT_ID('dbo.Order_Views')) AND is_disabled=0 AND is_not_trusted=0"))!.Num("n")==11,"all correction/view CHECK constraints are enabled and trusted");
        checks.Check((await database.Read("SELECT COLUMNPROPERTY(OBJECT_ID('dbo.Order_Views'),'id','IsIdentity') n"))!.Num("n")==1,"view storage uses its expected identity primary key");
        foreach(var malformed in new[]{"INSERT dbo.Order_Views(order_id,recipient_kind,customer_id,admin_email,seen_through_sequence,seen_at) SELECT TOP(1) id,'admin',customer_id,NULL,0,SYSUTCDATETIME() FROM dbo.Orders",
            "INSERT dbo.Order_Views(order_id,recipient_kind,customer_id,admin_email,seen_through_sequence,seen_at) SELECT TOP(1) id,'customer',customer_id,NULL,-1,SYSUTCDATETIME() FROM dbo.Orders"})
        {
            if((await database.Read("SELECT COUNT(*) n FROM dbo.Orders"))!.Num("n")==0)break;
            try{await database.Write(malformed);checks.Check(false,"invalid view recipient/sequence must fail in SQL");}
            catch(SqlException error)when(error.Number==547){checks.Check(true,"SQL CHECK rejects invalid typed view recipient/sequence");}
        }
    }
}
