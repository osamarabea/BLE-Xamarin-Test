using System;
using System.Collections.Generic;
using System.Linq;
using BLE.Client.Models;
using SQLite;
using Xamarin.Forms;

namespace BLE.Client.Helpers
{
    public class DatabaseHelper
    {
        static SQLiteConnection sqliteconnection;
        public const string DbFileName = "BLE5.db";

        public DatabaseHelper()
        {
            sqliteconnection = DependencyService.Get<ISQLite>().GetConnection();
            sqliteconnection.CreateTable<AdvertisementData>();
        }

        // Get All Device data      
        public List<AdvertisementData> GetAllDevicesData()
        {
            return (from data in sqliteconnection.Table<AdvertisementData>()
                    select data).ToList();
        }

        //Get Specific Device data  
        public AdvertisementData GetDeviceData(Guid id)
        {
            return sqliteconnection.Table<AdvertisementData>().FirstOrDefault(t => t.Id == id);
        }

        // Delete all Devices Data  
        public void DeleteAllDevices()
        {
            sqliteconnection.DeleteAll<AdvertisementData>();
        }

        // Delete Specific Device  
        public void DeleteDevice(Guid id)
        {
            sqliteconnection.Delete<AdvertisementData>(id);
        }

        // Insert new Device to DB   
        public void InsertDevice(AdvertisementData Device)
        {
            sqliteconnection.Insert(Device);
        }

        // Update Device Data  
        public void UpdateDevice(AdvertisementData Device)
        {
            sqliteconnection.Update(Device);
        }
    }
}
