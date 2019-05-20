using System;
using System.IO;
using BLE.Client.Droid.Helpers;
using BLE.Client.Helpers;
using SQLite;

//[assembly: Xamarin.Forms.Dependency(typeof(AndroidSQLite))]
namespace BLE.Client.Droid.Helpers
{
    public class AndroidSQLite : ISQLite
    {
        public string DbPath { get; set; }

        public SQLiteConnection GetConnection()
        {
            string documentsPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);

            // Documents folder  
            DbPath = Path.Combine(documentsPath, DatabaseHelper.DbFileName);
            var conn = new SQLiteConnection(DbPath);

            // Return the database connection  
            return conn;
        }
    }
}
