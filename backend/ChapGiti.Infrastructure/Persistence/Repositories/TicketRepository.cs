namespace ChapGiti.Infrastructure.Persistence.Repositories;

internal sealed class TicketRepository(SqlStore db) : ITicketRepository
{
    public Task<Dictionary<string, object?>?> LockExistingTicket(string id)
    {
        return db.One("""
            SELECT t.*,m.message
            FROM dbo.Tickets t WITH(UPDLOCK,HOLDLOCK)
            JOIN dbo.Ticket_Messages m ON m.id=t.id
            WHERE t.id=@p0
            """, id);
    }

    public async Task<int> InsertTicket(string id, string customerId, string subject, string createdAt, string? orderId = null)
    {
        if(orderId!=null)return await db.Execute("INSERT dbo.Tickets(id,customer_id,subject,status,created_at,updated_at,order_id) VALUES(@p0,@p1,@p2,'open',@p3,@p3,@p4)",id,customerId,subject,createdAt,orderId);
        return await db.Execute("INSERT dbo.Tickets(id,customer_id,subject,status,created_at,updated_at) VALUES(@p0,@p1,@p2,'open',@p3,@p3)", id, customerId, subject, createdAt);
    }
    public Task<bool> SupportsOrderLinks() => db.SupportsOrderLinks();
    private bool? messageReads;
    public async Task<bool> SupportsMessageReads() =>
        messageReads ??= (await db.One("SELECT CASE WHEN COL_LENGTH('dbo.Ticket_Messages','read_at') IS NULL THEN 0 ELSE 1 END ready"))!.Num("ready") == 1;
    private const string Filter = """
        WHERE (@p0 IS NULL OR t.customer_id=@p0)
        AND (@p1='' OR CHARINDEX(@p1,t.subject)>0 OR CHARINDEX(@p1,r.first_name+' '+r.last_name)>0 OR CHARINDEX(@p1,r.phone)>0)
        AND (@p2='' OR t.status=@p2)
        """;
    private async Task<string> Projection() {
        var linked=await db.SupportsOrderLinks();
        return """
            SELECT t.id,t.customer_id,t.subject,t.status,t.created_at,t.updated_at,
                COALESCE(NULLIF(LTRIM(RTRIM(r.first_name+' '+r.last_name)),''),r.phone) name,r.phone,
                LEFT(last_message.message,160) last_message,last_message.actor last_actor,
            """ + (await SupportsMessageReads() ? """
                (SELECT COUNT(*) FROM dbo.Ticket_Messages m WHERE m.ticket_id=t.id AND m.actor='admin' AND m.read_at IS NULL) unread_customer,
                (SELECT COUNT(*) FROM dbo.Ticket_Messages m WHERE m.ticket_id=t.id AND m.actor='customer' AND m.read_at IS NULL) unread_admin
                """ : " NULL unread_customer, NULL unread_admin") + ", " + (linked ? " o.id order_id,o.code order_code,o.title order_title,o.status order_status " : " CAST(NULL AS varchar(36)) order_id,CAST(NULL AS varchar(20)) order_code,CAST(NULL AS nvarchar(160)) order_title,CAST(NULL AS varchar(24)) order_status ") + """
            FROM dbo.Tickets t JOIN dbo.Customer_Registrations r ON r.id=t.customer_id
            """ + (linked?" LEFT JOIN dbo.Orders o ON o.id=t.order_id AND o.customer_id=t.customer_id ":" ") + """
            OUTER APPLY(SELECT TOP(1) message,actor FROM dbo.Ticket_Messages WHERE ticket_id=t.id ORDER BY created_at DESC,id DESC) last_message
            """;
    }
    public async Task<List<Dictionary<string,object?>>> SearchTickets(string? customerId,string query,string status,int skip,int take, bool oldestFirst = false) =>
        await db.Query(await Projection()+"\n"+Filter+" ORDER BY "+(oldestFirst ? "t.updated_at,t.id" : "t.updated_at DESC,t.id DESC")+" OFFSET @p3 ROWS FETCH NEXT @p4 ROWS ONLY",customerId,query,status,skip,take);
    public async Task<long> CountTickets(string? customerId,string query,string status) =>
        (await db.One("SELECT COUNT_BIG(*) n FROM dbo.Tickets t JOIN dbo.Customer_Registrations r ON r.id=t.customer_id "+Filter,customerId,query,status))!.Num("n");

    public Task<List<Dictionary<string, object?>>> OrderConversations(string orderId) =>
        db.Query("SELECT id, subject, status FROM dbo.Tickets WHERE order_id=@p0 ORDER BY updated_at DESC, id", orderId);

    public async Task<string?> ActiveOrderTicket(string customer, string order) =>
        (await db.One("SELECT TOP (1) id FROM dbo.Tickets WITH(UPDLOCK,HOLDLOCK) WHERE customer_id=@p0 AND order_id=@p1 AND status <> 'closed' ORDER BY updated_at DESC,id", customer, order))?.Str("id");

    public async Task<long> UnreadSupport(string? customerId, string incomingActor)
    {
        if (!await SupportsMessageReads()) return 0;
        return (await db.One("""
            SELECT COUNT_BIG(*) n FROM dbo.Ticket_Messages m JOIN dbo.Tickets t ON t.id=m.ticket_id
            WHERE m.actor=@p0 AND m.read_at IS NULL AND (@p1 IS NULL OR t.customer_id=@p1)
            """,incomingActor,customerId))!.Num("n");
    }

    public async Task<int> MarkMessagesRead(string ticket, string actor, string throughId) =>
        await db.Execute("""
            UPDATE m SET read_at=SYSUTCDATETIME()
            FROM dbo.Ticket_Messages m JOIN dbo.Ticket_Messages last ON last.ticket_id=m.ticket_id AND last.id=@p2 AND last.actor=@p1
            WHERE m.ticket_id=@p0 AND m.actor=@p1 AND m.read_at IS NULL
              AND (m.created_at<last.created_at OR (m.created_at=last.created_at AND m.id<=last.id))
            """,ticket,actor,throughId);

    public Task<int> InsertInitialMessage(string id, string message, string createdAt)
    {
        return db.Execute("INSERT dbo.Ticket_Messages(id,ticket_id,actor,message,created_at) VALUES(@p0,@p0,'customer',@p1,@p2)", id, message, createdAt);
    }

    public Task<List<Dictionary<string, object?>>> ListAll()
    {
        return db.Query("""
            SELECT TOP(200) t.*,r.first_name+' '+r.last_name name,r.phone
            FROM dbo.Tickets t
            JOIN dbo.Customer_Registrations r ON r.id=t.customer_id
            ORDER BY t.updated_at DESC
            """);
    }

    public Task<List<Dictionary<string, object?>>> ListOwned(string customerId)
    {
        return db.Query("""
            SELECT TOP(100) id,subject,status,created_at,updated_at
            FROM dbo.Tickets
            WHERE customer_id=@p0
            ORDER BY updated_at DESC
            """, customerId);
    }

    public async Task<List<Dictionary<string, object?>>> Messages(string ticketId)
    {
        var read = await SupportsMessageReads() ? "read_at" : "NULL read_at";
        return await db.Query("SELECT id,actor,message,created_at,"+read+" FROM dbo.Ticket_Messages WHERE ticket_id=@p0 ORDER BY created_at,id", ticketId);
    }

    public Task<int> UpdateStatus(string status, string updatedAt, string ticketId)
    {
        return db.Execute("UPDATE dbo.Tickets SET status=@p0,updated_at=@p1 WHERE id=@p2", status, updatedAt, ticketId);
    }

    public Task<Dictionary<string, object?>?> LockExistingMessage(string id)
    {
        return db.One("SELECT * FROM dbo.Ticket_Messages WITH(UPDLOCK,HOLDLOCK) WHERE id=@p0", id);
    }

    public Task<int> InsertMessage(string id, string ticketId, string actor, string message, string createdAt)
    {
        return db.Execute("INSERT dbo.Ticket_Messages(id,ticket_id,actor,message,created_at) VALUES(@p0,@p1,@p2,@p3,@p4)", id, ticketId, actor, message, createdAt);
    }

    public Task<int> UpdateAfterReply(string status, string updatedAt, string ticketId)
    {
        return db.Execute("UPDATE dbo.Tickets SET status=@p0,updated_at=@p1 WHERE id=@p2", status, updatedAt, ticketId);
    }
    public async Task<Dictionary<string, object?>?> FindTicket(string id, bool administrator, string? customerId, bool locked)
    {
        var sql = await Projection();
        if(locked)sql=sql.Replace("FROM dbo.Tickets t JOIN", "FROM dbo.Tickets t WITH(UPDLOCK,HOLDLOCK) JOIN");
        sql += " WHERE t.id=@p0" +
            (administrator ? "" : " AND t.customer_id=@p1");
        return await db.One(sql, administrator ? [id] : [id, customerId]);
    }

}
