namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class CustomerActivityRepository(SqlStore db) : ICustomerActivityRepository
{
    // Each source is an actual persisted event. Revocation is deliberately not called logout.
    private const string Events = """
        WITH activity AS (
          SELECT s.id,'login' type,s.customer_id,
            DATEADD(MILLISECOND,CONVERT(int,s.created_at%86400000),DATEADD(DAY,CONVERT(int,s.created_at/86400000),CONVERT(datetime2(3),'19700101'))) occurred_at,
            CAST(NULL AS varchar(36)) order_id,CAST(NULL AS varchar(36)) ticket_id,
            CAST(NULL AS nvarchar(160)) subject,CAST(NULL AS varchar(20)) order_code,
            CAST(NULL AS varchar(24)) status,CAST(NULL AS nvarchar(254)) actor
          FROM dbo.Auth_Sessions s WHERE s.customer_id IS NOT NULL
          UNION ALL
          SELECT o.id,'order_created',o.customer_id,o.created_at,o.id,NULL,o.title,o.code,NULL,NULL FROM dbo.Orders o
          UNION ALL
          SELECT t.id,'ticket_created',t.customer_id,t.created_at,NULL,t.id,t.subject,NULL,NULL,NULL FROM dbo.Tickets t
          UNION ALL
          SELECT e.id,'order_status',o.customer_id,e.created_at,o.id,NULL,o.title,o.code,e.status,
            CASE WHEN e.actor='customer' OR (o.customer_id IS NOT NULL AND e.actor=CONCAT('customer:',o.customer_id))
              THEN 'customer' ELSE COALESCE(n.username,NULLIF(e.actor,'')) END
          FROM dbo.Events e JOIN dbo.Orders o ON o.id=e.order_id
          LEFT JOIN dbo.Admin_Usernames n ON n.email=e.actor WHERE e.status!='new'
        )
        """;
    private const string From = """
        FROM activity a LEFT JOIN dbo.Customer_Registrations r ON r.id=a.customer_id
        WHERE (@p0='' OR CHARINDEX(@p0,r.first_name+' '+r.last_name)>0 OR CHARINDEX(@p0,r.phone)>0)
          AND (@p1='' OR a.type=@p1) AND (@p2 IS NULL OR a.occurred_at>=@p2) AND (@p3 IS NULL OR a.occurred_at<@p3)
        """;
    public async Task<(List<Dictionary<string,object?>> Items,long Total)> List(string query,string type,DateTime? from,DateTime? until,int page,int pageSize, bool oldestFirst = false)
    {
        var items=await db.Query(Events+"""
            SELECT a.id,a.type,a.occurred_at,a.order_id,a.ticket_id,a.subject,a.order_code,a.status,
                COALESCE(NULLIF(LTRIM(RTRIM(r.first_name+' '+r.last_name)),''),r.phone,N'اطلاعات مشتری موجود نیست') customer_name,
                r.phone,CASE WHEN a.type='order_status' AND (a.actor IS NULL OR a.actor!='customer') THEN a.actor
                    ELSE COALESCE(NULLIF(LTRIM(RTRIM(r.first_name+' '+r.last_name)),''),r.phone) END actor
            """+"\n"+From+" ORDER BY "+(oldestFirst ? "a.occurred_at,a.type,a.id" : "a.occurred_at DESC,a.type,a.id DESC")+" OFFSET @p4 ROWS FETCH NEXT @p5 ROWS ONLY",
            query,type,from,until,(page-1)*pageSize,pageSize);
        var count=await db.One(Events+"SELECT COUNT_BIG(*) n "+From,query,type,from,until);
        return (items,count!.Num("n"));
    }
}
