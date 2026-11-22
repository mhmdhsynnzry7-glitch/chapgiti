using System.Globalization;
namespace ChapGiti.Application.Features;

public sealed class CustomerActivityService(ICustomerActivityRepository repository,SessionService sessions)
{
    public async Task<UseCaseResult> List(string query,string type,string from,string until,int page,int pageSize, bool oldestFirst = false)
    {
        await sessions.Admin();
        if(query.Length>100||type is not ("" or "login" or "order_created" or "ticket_created" or "order_status")||page is <1 or >100000||pageSize is <1 or >50)
            throw new UseCaseException(ErrorKind.Invalid,"فیلتر فعالیت معتبر نیست.");
        static DateTime? Boundary(string value,bool end) {
            if(value=="")return null;
            if(!DateOnly.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||date.Year is <2000 or >2100)
                throw new UseCaseException(ErrorKind.Invalid,"تاریخ معتبر وارد کنید.");
            var local=(end?date.AddDays(1):date).ToDateTime(TimeOnly.MinValue,DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(local,TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"));
        }
        var start=Boundary(from,false);var end=Boundary(until,true);
        if(start.HasValue&&end.HasValue&&start>=end)throw new UseCaseException(ErrorKind.Invalid,"ترتیب تاریخ‌ها معتبر نیست.");
        var result=await repository.List(Input.NormalizeDigits(query.Trim()),type,start,end,page,pageSize, oldestFirst);
        return UseCaseResult.Json(new { items=result.Items,total=result.Total,page,pageSize });
    }
}
