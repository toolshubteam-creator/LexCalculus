using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LexCalculus.Tests.TestHelpers;

/// <summary>
/// Adım 6.10 (#31/#32) — yürütülen DB komutlarını sayan EF Core interceptor.
/// n+1 refactor'ların "tek query" iddiasını gerçek SQL Server LocalDB üzerinde
/// kanıtlamak için kullanılır (InMemory provider farklı davranır; SQL plan'i
/// yansıtmaz). Hem sync hem async reader yürütmeleri sayılır. Faz 6+ reuse.
/// </summary>
public sealed class QueryCounterInterceptor : DbCommandInterceptor
{
    public int Count { get; private set; }
    public List<string> ExecutedCommands { get; } = new();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Record(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    // Scalar (COUNT vs.) yürütmeleri de say — GetUnreadCountAsync tek SELECT COUNT.
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Record(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Record(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Record(DbCommand command)
    {
        Count++;
        ExecutedCommands.Add(command.CommandText);
    }

    public void Reset()
    {
        Count = 0;
        ExecutedCommands.Clear();
    }
}
