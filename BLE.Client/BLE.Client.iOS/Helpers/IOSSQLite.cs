using System;
using System.IO;
using BLE.Client.Helpers;
using BLE.Client.iOS.Helpers;

//[assembly: Dependency(typeof(IOSSQLite))]
namespace BLE.Client.iOS.Helpers
{
    public class IOSSQLite : ISQLite
    {
        public SQLiteConnection GetConnection()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.Personal); // Documents folder  
            string libraryPath = Path.Combine(documentsPath, "..", "Library"); // Library folder  
            var path = Path.Combine(libraryPath, DatabaseHelper.DbFileName);
            // Create the connection  
            var plat = new SQLite.Net.Platform.XamarinIOS.SQLitePlatformIOS();
            var conn = new SQLiteConnection(plat, path);
            // Return the database connection  
            return conn;
        }
    }
}
