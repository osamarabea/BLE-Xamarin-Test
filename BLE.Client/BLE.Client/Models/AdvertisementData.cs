using System;
using SQLite;

namespace BLE.Client.Models
{
    [Table("AdvertisementData")]
    public class AdvertisementData
    {
        [PrimaryKey, AutoIncrement]
        public Guid Id { get; set; }
        public string DeviceId { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
    }
}
