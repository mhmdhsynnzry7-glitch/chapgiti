using ChapGiti.Application.Common;
using Microsoft.Data.SqlClient;

namespace ChapGiti.SqlAcceptanceTests;

internal static class OrderIntegritySqlChecks
{
    public static async Task Run(OrderWorkflowApi api)
    {
        var db=api.Database;var checks=api.Checks;
        foreach(var (table,column,value) in new[]{("Order_Corrections","version",1),("Order_Corrections","is_legacy",0),("Orders","workflow_version",2)})
        {
            var row=await db.Read("SELECT d.definition FROM sys.default_constraints d JOIN sys.columns c ON d.object_id=c.default_object_id WHERE c.object_id=OBJECT_ID(@p0) AND c.name=@p1","dbo."+table,column);
            checks.Check(row!=null&&row.Str("definition").Replace("(","").Replace(")","")==value.ToString(),"actual SQL default: "+table+"."+column+"="+value);
        }
        checks.Check((await db.Read("SELECT COUNT(*) n FROM sys.columns WHERE (object_id=OBJECT_ID('dbo.Orders') AND name='final_price' OR object_id=OBJECT_ID('dbo.Order_Quotes') AND name='amount_toman') AND TYPE_NAME(user_type_id)='bigint'"))!.Num("n")==2,"both order and immutable quote amounts retain SQL bigint storage");
        var foreign=(await db.Read("SELECT TOP(1) f.id,f.order_id FROM dbo.Files f WHERE EXISTS(SELECT 1 FROM dbo.Order_Corrections c WHERE c.order_id<>f.order_id)"))!;
        var correction=(await db.Read("SELECT TOP(1) id,order_id FROM dbo.Order_Corrections WHERE order_id<>@p0",foreign.Str("order_id")))!;
        await Reject(db,checks,"UPDATE dbo.Orders SET approved_file_id=@p0 WHERE id=@p1",[foreign.Str("id"),correction.Str("order_id")],547,"SQL composite FK rejects a foreign-order production PDF");
        await Reject(db,checks,"UPDATE dbo.Order_Corrections SET file_id=@p0 WHERE id=@p1",[foreign.Str("id"),correction.Str("id")],547,"SQL composite FK rejects a foreign-order correction attachment");
        await Reject(db,checks,"UPDATE dbo.Files SET correction_id=@p0 WHERE id=@p1",[correction.Str("id"),foreign.Str("id")],547,"SQL composite FK rejects linking an attachment to another order's cycle");
        await Reject(db,checks,"UPDATE dbo.Files SET file_version=0 WHERE id=@p0",[foreign.Str("id")],547,"SQL CHECK rejects a non-positive PDF version");
        await Reject(db,checks,"INSERT dbo.Order_Views(order_id,recipient_kind,customer_id,admin_email,seen_through_sequence,seen_at) SELECT TOP(1) order_id,recipient_kind,customer_id,admin_email,seen_through_sequence,seen_at FROM dbo.Order_Views",[],2601,"SQL filtered recipient index rejects duplicate order views",2627);
        await Reject(db,checks,"INSERT dbo.Order_Corrections(id,order_id,revision,state,is_active,request_text,requires_new_pdf,requested_by_admin,requested_at) SELECT TOP(1) CONVERT(varchar(36),NEWID()),order_id,revision+1000,'requested',1,N'Duplicate active cycle',0,@p0,SYSUTCDATETIME() FROM dbo.Order_Corrections WHERE is_active=1",[OrderWorkflowFixtures.Administrator],2601,"SQL filtered unique index prevents two active correction cycles",2627);
        await Reject(db,checks,"INSERT dbo.Notifications(id,recipient_kind,customer_id,admin_email,event_type,entity_kind,entity_id,title,body,target_path,created_at,dedupe_key) SELECT TOP(1) CONVERT(varchar(36),NEWID()),recipient_kind,customer_id,admin_email,event_type,entity_kind,entity_id,title,body,target_path,created_at,dedupe_key FROM dbo.Notifications",[],2601,"SQL unique deduplication index rejects duplicate per-recipient business events",2627);
        await using(var store=db.Store())
        {
            await db.Guard(store);checks.Check((await store.Query("DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS")).Count==0,"DBCC verifies no orphaned foreign keys or invalid CHECK values in the isolated fixture");
            var metadata=await store.Query("SELECT object_key FROM dbo.Files");
            checks.Check(Directory.GetFiles(api.Fixtures.StorageRoot,"*.pdf",SearchOption.AllDirectories).Length==metadata.Count,"every synthetic private PDF has exactly one SQL metadata row after rollbacks");
            foreach(var row in metadata)await api.Fixtures.Storage.ReadPdf(row.Str("object_key"));
            checks.Check(true,"every SQL file metadata row resolves to its synthetic private PDF");
        }
        checks.Check((await db.Read("SELECT COUNT(*) n FROM dbo.Admin_Usernames n JOIN dbo.Admin_Users u ON u.email=n.email WHERE username='admin'"))!.Num("n")==1&&
            (await db.Read("SELECT COUNT(*) n FROM dbo.Customer_Accounts"))!.Num("n")==2,"administrator and synthetic customer identities remain present after all SQL workflows");
        await api.Request(api.Administrator,"GET","auth/me");await api.Request(api.Customer,"GET","customer/me");
        await api.Fixtures.VerifyPrivateHistory(checks);
    }
    private static async Task Reject(IsolatedDatabase database,AcceptanceChecks checks,string sql,object?[] values,int number,string label,int? alternative=null)
    {
        try{await database.Write(sql,values);checks.Check(false,label);}
        catch(SqlException error)when(error.Number==number||error.Number==alternative){checks.Check(true,label);}
    }
}
