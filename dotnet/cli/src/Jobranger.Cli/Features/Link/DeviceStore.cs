using System.Buffers.Text;
using Jobranger.Cli.Storage;
using Microsoft.Data.Sqlite;

namespace Jobranger.Cli.Features.Link;

internal sealed class DeviceStore(SqliteConnection connection) : IDeviceStore {
    private const string addSql =
        "INSERT INTO devices (id, name, public_key, paired_at) VALUES ($id, $name, $key, $at) "
        + "ON CONFLICT (id) DO UPDATE SET name = excluded.name, paired_at = excluded.paired_at";
    private const string listSql = "SELECT id, name, paired_at, last_seen FROM devices ORDER BY paired_at, id";
    private const string removeSql = "DELETE FROM devices WHERE id = $id";
    private const string removeAllSql = "DELETE FROM devices";

    public string Add(byte[] publicKey, string name, DateTimeOffset pairedAt) {
        string id = Base64Url.EncodeToString(publicKey);
        SqlCommands.Execute(connection, null, addSql,
            ("$id", id), ("$name", name), ("$key", publicKey), ("$at", StoredTime.Format(pairedAt)));
        return id;
    }

    public IReadOnlyList<Device> List() => SqlCommands.Query(connection, null, listSql, reader => new Device(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.OptionalText(3)));

    public bool Remove(string id) => SqlCommands.Execute(connection, null, removeSql, ("$id", id)) > 0;

    public int RemoveAll() => SqlCommands.Execute(connection, null, removeAllSql);
}
