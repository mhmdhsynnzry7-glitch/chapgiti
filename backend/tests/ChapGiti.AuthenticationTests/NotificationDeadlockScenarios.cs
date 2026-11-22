using System.Reflection;
using ChapGiti.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;

namespace ChapGiti.AuthenticationTests;

internal static class NotificationDeadlockScenarios
{
    public static async Task Run(Action<bool,string> check)
    {
        var calls=0;
        var value=await SqlDeadlockRetry.Run(()=>++calls<3?Task.FromException<int>(Failure(1205)):Task.FromResult(42),false);
        check(value==42&&calls==3,"notification autocommit read recovers from SQL 1205 without returning partial results");
        calls=0;
        try { await SqlDeadlockRetry.Run<int>(()=>{calls++;return Task.FromException<int>(Failure(1205));},false);check(false,"deadlock retry is bounded"); }
        catch(SqlException error) {check(error.Number==1205&&calls==3,"persistent SQL deadlock is propagated after three total attempts");}
        calls=0;
        try { await SqlDeadlockRetry.Run<int>(()=>{calls++;return Task.FromException<int>(Failure(1205));},true);check(false,"business transaction cannot be partially replayed"); }
        catch(SqlException error) {check(error.Number==1205&&calls==1,"notification retry never replays a statement inside a business transaction");}
        calls=0;
        try { await SqlDeadlockRetry.Run<int>(()=>{calls++;return Task.FromException<int>(Failure(547));},false);check(false,"constraint failures cannot be retried"); }
        catch(SqlException error) {check(error.Number==547&&calls==1,"notification constraint failures are preserved without retry or false success");}
        calls=0;
        try { await SqlDeadlockRetry.Run<int>(()=>{calls++;return Task.FromException<int>(new IOException("Simulated read failure"));},false);check(false,"non-SQL failures cannot be retried"); }
        catch(IOException) {check(calls==1,"unrelated notification persistence failures remain visible");}
        calls=0;var committed=0;
        var updated=await SqlDeadlockRetry.Run(()=>{
            if(++calls==1)return Task.FromException<int>(Failure(1205));
            committed++;return Task.FromResult(1);
        },false);
        check(updated==1&&calls==2&&committed==1,"standalone idempotent read-state update recovers once without duplicating a committed update");
    }

    // Same simulated SqlClient exception mechanism used by the schema-aware OTP tests.
    private static SqlException Failure(int number)
    {
        var constructor=typeof(SqlError).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).OrderBy(c=>c.GetParameters().Length).First();
        var args=constructor.GetParameters().Select(p=>p.ParameterType==typeof(int)?(object)(p.Position==0?number:0)
            :p.ParameterType==typeof(byte)?(byte)(p.Name!.Contains("class",StringComparison.OrdinalIgnoreCase)?13:0)
            :p.ParameterType==typeof(uint)?0U:p.ParameterType==typeof(string)?"Isolated simulated SQL failure":null).ToArray();
        var error=(SqlError)constructor.Invoke(args);
        var collection=(SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection),true)!;
        typeof(SqlErrorCollection).GetMethod("Add",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(collection,[error]);
        var factory=typeof(SqlException).GetMethods(BindingFlags.Static|BindingFlags.NonPublic)
            .Where(m=>m.Name=="CreateException"&&m.GetParameters()[0].ParameterType==typeof(SqlErrorCollection))
            .OrderBy(m=>m.GetParameters().Length).First();
        var factoryArgs=factory.GetParameters().Select(p=>p.Position==0?(object)collection:p.ParameterType==typeof(string)?"16.0.0":p.ParameterType==typeof(Guid)?Guid.Empty:null).ToArray();
        return (SqlException)factory.Invoke(null,factoryArgs)!;
    }
}
