using System;
using System.Collections.Generic;
using BLE.Client.Helpers;
using BLE.Client.Models;

namespace BLE.Client.Repositories
{
    public class AdvertisementDataRepository : IAdvertisementDataRepository
    {
        DatabaseHelper _databaseHelper;
        public AdvertisementDataRepository()
        {
            _databaseHelper = new DatabaseHelper();
        }

        public void DeleteDevice(Guid deviceId)
        {
            _databaseHelper.DeleteDevice(deviceId);
        }

        public void DeleteAllDevices()
        {
            _databaseHelper.DeleteAllDevices();
        }

        public List<AdvertisementData> GetAllDevicesData()
        {
            return _databaseHelper.GetAllDevicesData();
        }

        public AdvertisementData GetDeviceData(Guid deviceId)
        {
            return _databaseHelper.GetDeviceData(deviceId);
        }

        public void InsertDevice(AdvertisementData data)
        {
            _databaseHelper.InsertDevice(data);
        }

        public void UpdateDevice(AdvertisementData data)
        {
            _databaseHelper.UpdateDevice(data);
        }
    }
}
