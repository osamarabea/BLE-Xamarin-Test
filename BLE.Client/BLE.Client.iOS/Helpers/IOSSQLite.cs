using System;
using System.IO;
using BLE.Client.Helpers;
using BLE.Client.iOS.Helpers;

//[assembly: Dependency(typeof(IOSSQLite))]
namespace BLE.Client.iOS.Helpers
{
    public class IOSSQLite : ISQLite
    {
        public string DbPath { get; set; }

        public SQLiteConnection GetConnection()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal); // Documents folder  
            string libraryPath = Path.Combine(documentsPath, "..", "Library"); // Library folder  
            DbPath = Path.Combine(libraryPath, DatabaseHelper.DbFileName);
            // Create the connection  
            var plat = new SQLite.Net.Platform.XamarinIOS.SQLitePlatformIOS();
            var conn = new SQLiteConnection(plat, DbPath);
            // Return the database connection  
            return conn;
        }
    }
}
