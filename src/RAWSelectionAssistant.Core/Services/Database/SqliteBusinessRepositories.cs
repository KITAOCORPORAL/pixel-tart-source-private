using Microsoft.Data.Sqlite;
using System.Text.Json;
using RAWSelectionAssistant.Core.Models;
using RAWSelectionAssistant.Core.Services.Business;

namespace RAWSelectionAssistant.Core.Services.Database;

public sealed class SqliteBookingPeopleRepository(IPixelTartDatabase database) : IBookingPeopleRepository
{
    public async Task<IReadOnlyList<BookingContact>> ListContactsAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,BookingId,DisplayName,Phone,WeChat,Email,OtherContact,IsPrimary,Note,CreatedAtUtc,UpdatedAtUtc FROM BookingContacts WHERE BookingId=$booking ORDER BY IsPrimary DESC,UpdatedAtUtc,Id;";
        command.Parameters.AddWithValue("$booking", bookingId.ToString("D"));
        var result = new List<BookingContact>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadContact(reader));
        return result;
    }

    public async Task<IReadOnlyList<BookingStaffMember>> ListStaffAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,BookingId,DisplayName,Role,ArrivalTime,Phone,WeChat,Email,Note,SortOrder,CreatedAtUtc,UpdatedAtUtc FROM BookingStaffMembers WHERE BookingId=$booking ORDER BY SortOrder,Id;";
        command.Parameters.AddWithValue("$booking", bookingId.ToString("D"));
        var result = new List<BookingStaffMember>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) result.Add(ReadStaff(reader));
        return result;
    }

    public async Task ReplaceAsync(Guid bookingId, IReadOnlyList<BookingContact> contacts, IReadOnlyList<BookingStaffMember> staff, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(write: true, cancellationToken).ConfigureAwait(false);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var table in new[] { "BookingContacts", "BookingStaffMembers" })
            {
                await using var clear = connection.CreateCommand();
                clear.Transaction = transaction;
                clear.CommandText = $"DELETE FROM {table} WHERE BookingId=$booking;";
                clear.Parameters.AddWithValue("$booking", bookingId.ToString("D"));
                await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            foreach (var contact in contacts) await InsertContactAsync(connection, transaction, contact with { BookingId = bookingId }, cancellationToken).ConfigureAwait(false);
            foreach (var member in staff) await InsertStaffAsync(connection, transaction, member with { BookingId = bookingId }, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task InsertContactAsync(SqliteConnection connection, SqliteTransaction transaction, BookingContact value, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO BookingContacts(Id,BookingId,DisplayName,Phone,WeChat,Email,OtherContact,IsPrimary,Note,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$booking,$name,$phone,$wechat,$email,$other,$primary,$note,$created,$updated);";
        command.Parameters.AddWithValue("$id", value.Id.ToString("D")); command.Parameters.AddWithValue("$booking", value.BookingId.ToString("D")); command.Parameters.AddWithValue("$name", value.DisplayName);
        command.Parameters.AddWithValue("$phone", Db(value.Phone)); command.Parameters.AddWithValue("$wechat", Db(value.WeChat)); command.Parameters.AddWithValue("$email", Db(value.Email)); command.Parameters.AddWithValue("$other", Db(value.OtherContact));
        command.Parameters.AddWithValue("$primary", value.IsPrimary ? 1 : 0); command.Parameters.AddWithValue("$note", Db(value.Note)); command.Parameters.AddWithValue("$created", Utc(value.CreatedAtUtc)); command.Parameters.AddWithValue("$updated", Utc(value.UpdatedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task InsertStaffAsync(SqliteConnection connection, SqliteTransaction transaction, BookingStaffMember value, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "INSERT INTO BookingStaffMembers(Id,BookingId,DisplayName,Role,ArrivalTime,Phone,WeChat,Email,Note,SortOrder,CreatedAtUtc,UpdatedAtUtc) VALUES($id,$booking,$name,$role,$arrival,$phone,$wechat,$email,$note,$sort,$created,$updated);";
        command.Parameters.AddWithValue("$id", value.Id.ToString("D")); command.Parameters.AddWithValue("$booking", value.BookingId.ToString("D")); command.Parameters.AddWithValue("$name", value.DisplayName); command.Parameters.AddWithValue("$role", value.Role.ToString());
        command.Parameters.AddWithValue("$arrival", value.ArrivalTime is null ? DBNull.Value : Utc(value.ArrivalTime.Value)); command.Parameters.AddWithValue("$phone", Db(value.Phone)); command.Parameters.AddWithValue("$wechat", Db(value.WeChat)); command.Parameters.AddWithValue("$email", Db(value.Email)); command.Parameters.AddWithValue("$note", Db(value.Note));
        command.Parameters.AddWithValue("$sort", value.SortOrder); command.Parameters.AddWithValue("$created", Utc(value.CreatedAtUtc)); command.Parameters.AddWithValue("$updated", Utc(value.UpdatedAtUtc));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static BookingContact ReadContact(SqliteDataReader r) => new() { Id=Guid.Parse(r.GetString(0)),BookingId=Guid.Parse(r.GetString(1)),DisplayName=r.GetString(2),Phone=Text(r,3),WeChat=Text(r,4),Email=Text(r,5),OtherContact=Text(r,6),IsPrimary=r.GetInt32(7)==1,Note=Text(r,8),CreatedAtUtc=DateTimeOffset.Parse(r.GetString(9)),UpdatedAtUtc=DateTimeOffset.Parse(r.GetString(10)) };
    private static BookingStaffMember ReadStaff(SqliteDataReader r) => new() { Id=Guid.Parse(r.GetString(0)),BookingId=Guid.Parse(r.GetString(1)),DisplayName=r.GetString(2),Role=Enum.TryParse<BookingStaffRole>(r.GetString(3),out var role)?role:BookingStaffRole.Other,ArrivalTime=r.IsDBNull(4)?null:DateTimeOffset.Parse(r.GetString(4)),Phone=Text(r,5),WeChat=Text(r,6),Email=Text(r,7),Note=Text(r,8),SortOrder=r.GetInt32(9),CreatedAtUtc=DateTimeOffset.Parse(r.GetString(10)),UpdatedAtUtc=DateTimeOffset.Parse(r.GetString(11)) };
    private static string? Text(SqliteDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i);
    private static object Db(string? value)=>string.IsNullOrWhiteSpace(value)?DBNull.Value:value.Trim();
    private static string Utc(DateTimeOffset value)=>value.ToUniversalTime().ToString("O");
}

public sealed class SqliteFinanceRepository(IPixelTartDatabase database) : IFinanceRepository
{
    public async Task<IReadOnlyList<FinanceCategory>> ListCategoriesAsync(bool includeDisabled = false, CancellationToken cancellationToken = default)
    {
        await using var connection=await database.OpenConnectionAsync(cancellationToken:cancellationToken).ConfigureAwait(false);await using var command=connection.CreateCommand();
        command.CommandText="SELECT Id,Kind,Name,SortOrder,IsSystemDefault,IsDisabled FROM FinanceCategories"+(includeDisabled?string.Empty:" WHERE IsDisabled=0")+" ORDER BY Kind,SortOrder,Name;";
        var result=new List<FinanceCategory>();await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))result.Add(new(){Id=Guid.Parse(reader.GetString(0)),Kind=Enum.Parse<FinanceTransactionKind>(reader.GetString(1)),Name=reader.GetString(2),SortOrder=reader.GetInt32(3),IsSystemDefault=reader.GetInt32(4)==1,IsDisabled=reader.GetInt32(5)==1});return result;
    }

    public async Task<FinanceTransaction?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection=await database.OpenConnectionAsync(cancellationToken:cancellationToken).ConfigureAwait(false);await using var command=connection.CreateCommand();command.CommandText=Select+" WHERE Id=$id;";command.Parameters.AddWithValue("$id",id.ToString("D"));await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)?Read(reader):null;
    }

    public async Task SaveAsync(FinanceTransaction value, CancellationToken cancellationToken = default)
    {
        await using var connection=await database.OpenConnectionAsync(write:true,cancellationToken).ConfigureAwait(false);await using var command=connection.CreateCommand();
        command.CommandText="""
        INSERT INTO FinanceTransactions(Id,Kind,CategoryId,AmountMinor,CurrencyCode,CurrencyScale,OccurredOn,PaymentStatus,BookingId,ProjectId,Counterparty,PaymentMethod,Note,AttachmentCount,AttachmentReferencesJson,CreatedAtUtc,UpdatedAtUtc)
        VALUES($id,$kind,$category,$amount,$currency,$scale,$date,$status,$booking,$project,$counterparty,$method,$note,$attachments,$attachmentReferences,$created,$updated)
        ON CONFLICT(Id) DO UPDATE SET Kind=excluded.Kind,CategoryId=excluded.CategoryId,AmountMinor=excluded.AmountMinor,CurrencyCode=excluded.CurrencyCode,CurrencyScale=excluded.CurrencyScale,OccurredOn=excluded.OccurredOn,PaymentStatus=excluded.PaymentStatus,BookingId=excluded.BookingId,ProjectId=excluded.ProjectId,Counterparty=excluded.Counterparty,PaymentMethod=excluded.PaymentMethod,Note=excluded.Note,AttachmentCount=excluded.AttachmentCount,AttachmentReferencesJson=excluded.AttachmentReferencesJson,UpdatedAtUtc=excluded.UpdatedAtUtc;
        """;
        command.Parameters.AddWithValue("$id",value.Id.ToString("D"));command.Parameters.AddWithValue("$kind",value.Kind.ToString());command.Parameters.AddWithValue("$category",value.CategoryId.ToString("D"));command.Parameters.AddWithValue("$amount",value.AmountMinor);command.Parameters.AddWithValue("$currency",value.CurrencyCode);command.Parameters.AddWithValue("$scale",value.CurrencyScale);command.Parameters.AddWithValue("$date",value.OccurredOn.ToString("yyyy-MM-dd"));command.Parameters.AddWithValue("$status",value.PaymentStatus.ToString());command.Parameters.AddWithValue("$booking",value.BookingId is null?DBNull.Value:value.BookingId.Value.ToString("D"));command.Parameters.AddWithValue("$project",value.ProjectId is null?DBNull.Value:value.ProjectId.Value.ToString("D"));command.Parameters.AddWithValue("$counterparty",Db(value.Counterparty));command.Parameters.AddWithValue("$method",Db(value.PaymentMethod));command.Parameters.AddWithValue("$note",Db(value.Note));command.Parameters.AddWithValue("$attachments",value.AttachmentPaths.Count);command.Parameters.AddWithValue("$attachmentReferences", value.AttachmentPaths.Count == 0 ? DBNull.Value : JsonSerializer.Serialize(value.AttachmentPaths));command.Parameters.AddWithValue("$created",Utc(value.CreatedAtUtc));command.Parameters.AddWithValue("$updated",Utc(value.UpdatedAtUtc));await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default){await using var connection=await database.OpenConnectionAsync(write:true,cancellationToken).ConfigureAwait(false);await using var command=connection.CreateCommand();command.CommandText="DELETE FROM FinanceTransactions WHERE Id=$id;";command.Parameters.AddWithValue("$id",id.ToString("D"));return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false)>0;}

    public async Task<IReadOnlyList<FinanceTransaction>> QueryAsync(FinanceQuery query, CancellationToken cancellationToken = default)
    {
        await using var connection=await database.OpenConnectionAsync(cancellationToken:cancellationToken).ConfigureAwait(false);await using var command=connection.CreateCommand();var clauses=new List<string>();
        if(query.From is not null){clauses.Add("OccurredOn >= $from");command.Parameters.AddWithValue("$from",query.From.Value.ToString("yyyy-MM-dd"));}if(query.To is not null){clauses.Add("OccurredOn <= $to");command.Parameters.AddWithValue("$to",query.To.Value.ToString("yyyy-MM-dd"));}if(query.Kind is not null){clauses.Add("Kind=$kind");command.Parameters.AddWithValue("$kind",query.Kind.Value.ToString());}if(query.PaymentStatus is not null){clauses.Add("PaymentStatus=$status");command.Parameters.AddWithValue("$status",query.PaymentStatus.Value.ToString());}if(query.BookingId is not null){clauses.Add("BookingId=$booking");command.Parameters.AddWithValue("$booking",query.BookingId.Value.ToString("D"));}if(query.ProjectId is not null){clauses.Add("ProjectId=$project");command.Parameters.AddWithValue("$project",query.ProjectId.Value.ToString("D"));}if(query.CategoryId is not null){clauses.Add("CategoryId=$category");command.Parameters.AddWithValue("$category",query.CategoryId.Value.ToString("D"));}if(!string.IsNullOrWhiteSpace(query.CurrencyCode)){clauses.Add("CurrencyCode=$currency COLLATE NOCASE");command.Parameters.AddWithValue("$currency",query.CurrencyCode.Trim());}if(!string.IsNullOrWhiteSpace(query.Keyword)){clauses.Add("(Counterparty LIKE $keyword OR PaymentMethod LIKE $keyword OR Note LIKE $keyword OR EXISTS(SELECT 1 FROM ShootBookings b WHERE b.Id=FinanceTransactions.BookingId AND (b.Title LIKE $keyword OR b.ClientDisplayName LIKE $keyword)) OR EXISTS(SELECT 1 FROM Projects p WHERE p.Id=FinanceTransactions.ProjectId AND p.Name LIKE $keyword))");command.Parameters.AddWithValue("$keyword","%"+query.Keyword.Trim()+"%");}
        command.CommandText=Select+(clauses.Count==0?string.Empty:" WHERE "+string.Join(" AND ",clauses))+" ORDER BY OccurredOn DESC,UpdatedAtUtc DESC,Id;";var result=new List<FinanceTransaction>();await using var reader=await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);while(await reader.ReadAsync(cancellationToken).ConfigureAwait(false))result.Add(Read(reader));return result;
    }

    private const string Select="SELECT Id,Kind,CategoryId,AmountMinor,CurrencyCode,CurrencyScale,OccurredOn,PaymentStatus,BookingId,ProjectId,Counterparty,PaymentMethod,Note,AttachmentCount,AttachmentReferencesJson,CreatedAtUtc,UpdatedAtUtc FROM FinanceTransactions";
    private static FinanceTransaction Read(SqliteDataReader r)=>new(){Id=Guid.Parse(r.GetString(0)),Kind=Enum.Parse<FinanceTransactionKind>(r.GetString(1)),CategoryId=Guid.Parse(r.GetString(2)),AmountMinor=r.GetInt64(3),CurrencyCode=r.GetString(4),CurrencyScale=r.GetInt32(5),OccurredOn=DateOnly.Parse(r.GetString(6)),PaymentStatus=Enum.Parse<FinancePaymentStatus>(r.GetString(7)),BookingId=r.IsDBNull(8)?null:Guid.Parse(r.GetString(8)),ProjectId=r.IsDBNull(9)?null:Guid.Parse(r.GetString(9)),Counterparty=Text(r,10),PaymentMethod=Text(r,11),Note=Text(r,12),AttachmentCount=r.GetInt32(13),AttachmentPaths=ReadPaths(r,14),CreatedAtUtc=DateTimeOffset.Parse(r.GetString(15)),UpdatedAtUtc=DateTimeOffset.Parse(r.GetString(16))};
    private static IReadOnlyList<string> ReadPaths(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal)) return [];
        try { return JsonSerializer.Deserialize<string[]>(reader.GetString(ordinal)) ?? []; }
        catch (JsonException) { return []; }
    }
    private static string? Text(SqliteDataReader r,int i)=>r.IsDBNull(i)?null:r.GetString(i);private static object Db(string? value)=>string.IsNullOrWhiteSpace(value)?DBNull.Value:value.Trim();private static string Utc(DateTimeOffset value)=>value.ToUniversalTime().ToString("O");
}
