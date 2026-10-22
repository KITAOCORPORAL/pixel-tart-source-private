using Microsoft.Data.Sqlite;
using RAWSelectionAssistant.Core.Models;
namespace RAWSelectionAssistant.Core.Services.Database;
/// <summary>Strict insert and transaction receipt; deliberately not Upsert.</summary>
public interface IPlanningImportRepository
{
    Task<bool> ContainsProjectIdentityAsync(Guid id,CancellationToken token=default);
    Task<Guid?> GetPlanningImportReceiptAsync(Guid id,CancellationToken token=default);
    Task InsertPlanningImportAsync(PhotoProjectRecord project,Guid transactionId,CancellationToken token=default);
}
public sealed partial class SqliteProjectRepository : IPlanningImportRepository
{
    public string PlanningImportDatabaseLocation=>database is PixelTartDatabase db?db.DatabasePath:"当前项目数据库";
    public async Task<bool> ContainsProjectIdentityAsync(Guid id,CancellationToken token=default)
    {
        await using var connection=await database.OpenConnectionAsync(cancellationToken:token);await using var cmd=connection.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM Projects WHERE Id=$id";cmd.Parameters.AddWithValue("$id",id.ToString("D"));return Convert.ToInt64(await cmd.ExecuteScalarAsync(token))!=0;
    }
    public async Task<Guid?> GetPlanningImportReceiptAsync(Guid id,CancellationToken token=default)
    {
        await using var connection=await database.OpenConnectionAsync(cancellationToken:token);await using var exists=connection.CreateCommand();exists.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='PlanningPackageImportReceipts'";if(Convert.ToInt64(await exists.ExecuteScalarAsync(token))==0)return null;
        await using var cmd=connection.CreateCommand();cmd.CommandText="SELECT TransactionId FROM PlanningPackageImportReceipts WHERE ProjectId=$id";cmd.Parameters.AddWithValue("$id",id.ToString("D"));return Guid.TryParse(Convert.ToString(await cmd.ExecuteScalarAsync(token)),out var receipt)?receipt:null;
    }
    public async Task InsertPlanningImportAsync(PhotoProjectRecord project,Guid transactionId,CancellationToken token=default)
    {
        await using var connection=await database.OpenConnectionAsync(write:true,cancellationToken:token);await using var transaction=(SqliteTransaction)await connection.BeginTransactionAsync(token);
        await using(var schema=connection.CreateCommand()){schema.Transaction=transaction;schema.CommandText="CREATE TABLE IF NOT EXISTS PlanningPackageImportReceipts(ProjectId TEXT PRIMARY KEY,TransactionId TEXT NOT NULL,ImportedAt TEXT NOT NULL)";await schema.ExecuteNonQueryAsync(token);}
        await using(var insert=connection.CreateCommand())
        {
            insert.Transaction=transaction;insert.CommandText="INSERT INTO Projects(Id,Name,ProjectType,WorkflowState,RootPath,CreatedAt,UpdatedAt,LastOpenedAt,IsArchived) VALUES($id,$name,$type,$state,'',$now,$now,$now,0)";
            insert.Parameters.AddWithValue("$id",project.Id.ToString("D"));insert.Parameters.AddWithValue("$name",project.Name);insert.Parameters.AddWithValue("$type",project.Category.ToString());insert.Parameters.AddWithValue("$state",project.Status.ToString());insert.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await insert.ExecuteNonQueryAsync(token);
        }
        await using(var receipt=connection.CreateCommand()){receipt.Transaction=transaction;receipt.CommandText="INSERT INTO PlanningPackageImportReceipts(ProjectId,TransactionId,ImportedAt) VALUES($id,$tx,$now)";receipt.Parameters.AddWithValue("$id",project.Id.ToString("D"));receipt.Parameters.AddWithValue("$tx",transactionId.ToString("D"));receipt.Parameters.AddWithValue("$now",DateTimeOffset.UtcNow.ToString("O"));await receipt.ExecuteNonQueryAsync(token);}
        await transaction.CommitAsync(token);
    }
}
