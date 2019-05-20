using System;
using SQLite;

namespace BLE.Client.Helpers
{
    public interface ISQLite
    {
        string DbPath { get; set; }
        SQLiteConnection GetConnection();
    }
}
