using System;
using System.Collections.Generic;
using BLE.Client.Models;

namespace BLE.Client.Repositories
{
    public interface IAdvertisementDataRepository
    {
        List<AdvertisementData> GetAllDevicesData();

        //Get Specific Device data  
        AdvertisementData GetDeviceData(Guid deviceId);

        // Delete all Devices Data  
        void DeleteAllDevices();

        // Delete Specific Device  
        void DeleteDevice(Guid deviceId);

        // Insert new Device to DB   
        void InsertDevice(AdvertisementData data);

        // Update Device Data  
        void UpdateDevice(AdvertisementData data);
    }
}
