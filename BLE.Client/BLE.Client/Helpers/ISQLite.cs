using System;
using SQLite;

namespace BLE.Client.Helpers
{
    public interface ISQLite
    {
        SQLiteConnection GetConnection();
    }
}
