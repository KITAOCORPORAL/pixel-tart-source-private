using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RAWSelectionAssistant.Core.Services.Database;
namespace Guangjing.Schedule.Server;

public sealed class DeviceAuthorization(PixelTartDatabase database,string ownerKey)
{
    public static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public void Owner(HttpRequest request)
    {
        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Hash(request.Headers["X-Owner-Key"].ToString())),Encoding.UTF8.GetBytes(Hash(ownerKey))))
            throw new ScheduleProblem(401,"OwnerRequired","需要电脑授权凭据。");
    }
    public async Task<ScheduleDevice> Require(HttpRequest request,CancellationToken token)
    {
        var header=request.Headers.Authorization.ToString();
        if(!header.StartsWith("Bearer ",StringComparison.Ordinal)||header.Length>256) throw new ScheduleProblem(401,"DeviceRequired","请使用电脑签发的完整手机链接。");
        await using var connection=await database.OpenConnectionAsync(cancellationToken:token);
        await using var command=connection.CreateCommand();
        command.CommandText="SELECT Id,Name,ExpiresAtUtc,RevokedAtUtc FROM ScheduleDevices WHERE TokenHash=$hash;";
        command.Parameters.AddWithValue("$hash",Hash(header[7..]));
        await using var reader=await command.ExecuteReaderAsync(token);
        if(!await reader.ReadAsync(token)||!reader.IsDBNull(3)) throw new ScheduleProblem(401,"DeviceRevoked","设备授权已失效，请联系电脑重新授权。");
        var expires=DateTimeOffset.Parse(reader.GetString(2));
        if(expires<=DateTimeOffset.UtcNow) throw new ScheduleProblem(401,"DeviceExpired","手机档期授权已过期，请重新授权。");
        return new(reader.GetString(0),reader.GetString(1),expires);
    }
    public async Task<object> Create(DeviceRequest body,string operationId,string publicBase,CancellationToken token)
    {
        if(string.IsNullOrWhiteSpace(body.Name)||body.Name.Length>80||body.ValidHours is <1 or >168||!Guid.TryParse(operationId,out var id))
            throw new ScheduleProblem(400,"InvalidDevice","需要设备名称、1至168小时有效期和稳定请求 Id。");
        // Derived token can be recovered for an identical create retry; only its hash is stored in SQLite.
        var credential=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(ownerKey),Encoding.UTF8.GetBytes("schedule/v1/"+id.ToString("N"))));
        await using var connection=await database.OpenConnectionAsync(write:true,cancellationToken:token);
        await using var transaction=connection.BeginTransaction(deferred:false);
        await using var existing=connection.CreateCommand();existing.Transaction=transaction;
        existing.CommandText="SELECT Name,ExpiresAtUtc,RevokedAtUtc,RequestHash FROM ScheduleDevices WHERE Id=$id;";existing.Parameters.AddWithValue("$id",id.ToString("N"));
        DateTimeOffset expires;
        await using(var reader=await existing.ExecuteReaderAsync(token))
        {
            if(await reader.ReadAsync(token))
            {
                if(reader.GetString(0)!=body.Name||!reader.IsDBNull(2)||reader.GetString(3)!=Hash(JsonSerializer.Serialize(body))) throw new ScheduleProblem(409,"DeviceOperationConflict","授权请求已用于其他设备或已经撤销。");
                expires=DateTimeOffset.Parse(reader.GetString(1));
            }
            else expires=DateTimeOffset.UtcNow.AddHours(body.ValidHours);
        }
        await using var command=connection.CreateCommand();command.Transaction=transaction;
        command.CommandText="INSERT OR IGNORE INTO ScheduleDevices(Id,Name,RequestHash,TokenHash,ExpiresAtUtc,CreatedAtUtc) VALUES($id,$name,$requestHash,$hash,$expires,$at);";
        command.Parameters.AddWithValue("$id",id.ToString("N"));command.Parameters.AddWithValue("$name",body.Name);
        command.Parameters.AddWithValue("$requestHash",Hash(JsonSerializer.Serialize(body)));
        command.Parameters.AddWithValue("$hash",Hash(credential));command.Parameters.AddWithValue("$expires",expires.ToString("O"));command.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));
        await command.ExecuteNonQueryAsync(token);await transaction.CommitAsync(token);
        return new {device=new ScheduleDevice(id.ToString("N"),body.Name,expires),url=publicBase+"/calendar#token="+credential};
    }
    public async Task Revoke(string id,CancellationToken token)
    {
        await using var connection=await database.OpenConnectionAsync(write:true,cancellationToken:token);await using var command=connection.CreateCommand();
        command.CommandText="UPDATE ScheduleDevices SET RevokedAtUtc=COALESCE(RevokedAtUtc,$at) WHERE Id=$id;";
        command.Parameters.AddWithValue("$id",id);command.Parameters.AddWithValue("$at",DateTimeOffset.UtcNow.ToString("O"));await command.ExecuteNonQueryAsync(token);
    }
}
