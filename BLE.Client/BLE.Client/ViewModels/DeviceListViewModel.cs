using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Acr.UserDialogs;
using BLE.Client.Extensions;
using BLE.Client.Helpers;
using BLE.Client.Models;
using BLE.Client.Repositories;
using MvvmCross.Core.ViewModels;
using MvvmCross.Platform;
using Newtonsoft.Json;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;
using Plugin.Clipboard;
using Plugin.Messaging;
using Plugin.Permissions.Abstractions;
using Plugin.Settings.Abstractions;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace BLE.Client.ViewModels
{
    public class DeviceListViewModel : BaseViewModel
    {
        private readonly IBluetoothLE _bluetoothLe;
        private readonly IUserDialogs _userDialogs;
        private readonly ISettings _settings;
        private readonly IPermissions _permissions;
        private readonly IAdvertisementDataRepository _advertisementDataRepository;
        private readonly ISQLite _sqliteProvider;
        private readonly IFileWorker _fileWorker;
        private Guid _previousGuid;
        private CancellationTokenSource _cancellationTokenSource;

        public MvxCommand RefreshCommand => new MvxCommand(() => TryStartScanning(true));
        public MvxCommand SendEmailCommand => new MvxCommand(() => SendDbEmail());
        public MvxCommand<DeviceListItemViewModel> DisconnectCommand => new MvxCommand<DeviceListItemViewModel>(DisconnectDevice);

        public ObservableCollection<DeviceListItemViewModel> Devices { get; set; } = new ObservableCollection<DeviceListItemViewModel>();
        public bool IsRefreshing => Adapter.IsScanning;
        public bool IsStateOn => _bluetoothLe.IsOn;
        public string StateText => GetStateText();
        public string VersionNumber { get; private set; }
        public DeviceListItemViewModel SelectedDevice
        {
            get { return null; }
            set
            {
                if (value != null)
                {
                    HandleSelectedDevice(value);
                }

                RaisePropertyChanged();
            }
        }

        bool _useAutoConnect;
        public bool UseAutoConnect
        {
            get
            {
                return _useAutoConnect;
            }

            set
            {
                if (_useAutoConnect == value)
                    return;
                
                _useAutoConnect = value;
                RaisePropertyChanged();
            }
        }

        bool _onlyShowTi;
        public bool OnlyShowTi
        {
            get
            {
                return _onlyShowTi;
            }

            set
            {
                if (_onlyShowTi == value)
                    return;

                _onlyShowTi = value;
                RaisePropertyChanged();
                StopScanCommand.Execute();
                TryStartScanning(true);
            }
        }

        public MvxCommand StopScanCommand => new MvxCommand(() =>
        {
            _cancellationTokenSource.Cancel();
            CleanupCancellationToken();
            RaisePropertyChanged(() => IsRefreshing);
        }, () => _cancellationTokenSource != null);

        public DeviceListViewModel(IBluetoothLE bluetoothLe, 
            IAdapter adapter, 
            IUserDialogs userDialogs, 
            ISettings settings, 
            IPermissions permissions) : base(adapter)
        {
            _permissions = permissions;
            _bluetoothLe = bluetoothLe;
            _userDialogs = userDialogs;
            _settings = settings;
            _bluetoothLe.StateChanged += OnStateChanged;
            Adapter.DeviceDiscovered += OnDeviceDiscovered;
            Adapter.ScanTimeoutElapsed += Adapter_ScanTimeoutElapsed;
            Adapter.DeviceDisconnected += OnDeviceDisconnected;
            Adapter.DeviceConnectionLost += OnDeviceConnectionLost;
            //Adapter.DeviceConnected += (sender, e) => Adapter.DisconnectDeviceAsync(e.Device);
            _advertisementDataRepository = DependencyService.Get<IAdvertisementDataRepository>();
            _sqliteProvider = DependencyService.Get<ISQLite>();
            _fileWorker = DependencyService.Get<IFileWorker>();

            VersionNumber = "Version: " + VersionTracking.CurrentVersion;

            RequestPermissions();
        }

        private void OnDeviceConnectionLost(object sender, DeviceErrorEventArgs e)
        {
            Devices.FirstOrDefault(d => d.Id == e.Device.Id)?.Update();

            _userDialogs.HideLoading();
            _userDialogs.ErrorToast("Error", $"Connection LOST {e.Device.Name}", TimeSpan.FromMilliseconds(6000));
        }

        private void OnStateChanged(object sender, BluetoothStateChangedArgs e)
        {
            RaisePropertyChanged(nameof(IsStateOn));
            RaisePropertyChanged(nameof(StateText));
            //TryStartScanning();
        }

        private string GetStateText()
        {
            switch (_bluetoothLe.State)
            {
                case BluetoothState.Unknown:
                    return "Unknown BLE state.";
                case BluetoothState.Unavailable:
                    return "BLE is not available on this device.";
                case BluetoothState.Unauthorized:
                    return "You are not allowed to use BLE.";
                case BluetoothState.TurningOn:
                    return "BLE is warming up, please wait.";
                case BluetoothState.On:
                    return "BLE is on.";
                case BluetoothState.TurningOff:
                    return "BLE is turning off. That's sad!";
                case BluetoothState.Off:
                    return "BLE is off. Turn it on!";
                default:
                    return "Unknown BLE state.";
            }
        }

        private void Adapter_ScanTimeoutElapsed(object sender, EventArgs e)
        {
            RaisePropertyChanged(() => IsRefreshing);

            CleanupCancellationToken();
        }

        private void OnDeviceDiscovered(object sender, DeviceEventArgs args)
        {
            AddOrUpdateDevice(args.Device);
        }

        private void AddOrUpdateDevice(IDevice device)
        {
            InvokeOnMainThread(() =>
            {
                var vm = Devices.FirstOrDefault(d => d.Device.Id == device.Id);
                if (vm != null)
                {
                    if (OnlyShowTi)
                        Devices.Remove(vm);
                    else
                        vm.Update();
                }
                else
                {
                    if (OnlyShowTi)
                    {
                        var isTi = device.AdvertisementRecords != null && device.AdvertisementRecords.Count >= 3
                            && device.AdvertisementRecords.Any(x => x.Type == AdvertisementRecordType.ManufacturerSpecificData
                            && Convert.ToInt32(x.Data[0]) == 1 && Convert.ToInt32(x.Data[1]) == 2 && Convert.ToInt32(x.Data[2]) == 3);
                        if (isTi)
                            Devices.Add(new DeviceListItemViewModel(device));
                    }
                    else
                        Devices.Add(new DeviceListItemViewModel(device));
                }
            });
        }

        public override async void Resume()
        {
            base.Resume();

            TryStartScanning();
            GetSystemConnectedOrPairedDevices();
        }

        private void GetSystemConnectedOrPairedDevices()
        {
            try
            {
                //heart rate
                var guid = Guid.Parse("0000180d-0000-1000-8000-00805f9b34fb");

                // SystemDevices = Adapter.GetSystemConnectedOrPairedDevices(new[] { guid }).Select(d => new DeviceListItemViewModel(d)).ToList();
                // remove the GUID filter for test
                // Avoid to loose already IDevice with a connection, otherwise you can't close it
                // Keep the reference of already known devices and drop all not in returned list.
                var pairedOrConnectedDeviceWithNullGatt = Adapter.GetSystemConnectedOrPairedDevices();
                SystemDevices.RemoveAll(sd => !pairedOrConnectedDeviceWithNullGatt.Any(p => p.Id == sd.Id));
                SystemDevices.AddRange(pairedOrConnectedDeviceWithNullGatt.Where(d => !SystemDevices.Any(sd => sd.Id == d.Id)).Select(d => new DeviceListItemViewModel(d)));
                RaisePropertyChanged(() => SystemDevices);
            }
            catch (Exception ex)
            {
                Trace.Message("Failed to retreive system connected devices. {0}", ex.Message);
            }
        }

        public List<DeviceListItemViewModel> SystemDevices { get; private set; } = new List<DeviceListItemViewModel>();

        public override void Suspend()
        {
            base.Suspend();

            Adapter.StopScanningForDevicesAsync();
            RaisePropertyChanged(() => IsRefreshing);
        }

        private async void TryStartScanning(bool refresh = false)
        {
            var arePermissionsGranted = await CheckIfPermissionsGranted();
            if (arePermissionsGranted && IsStateOn && (refresh || !Devices.Any()) && !IsRefreshing)
            {
                ScanForDevices();
            }
        }

        private async Task RequestPermissions()
        {
            if (Xamarin.Forms.Device.OS == Xamarin.Forms.TargetPlatform.Android)
            {
                var status = await _permissions.CheckPermissionStatusAsync(Permission.Location);
                if (status != PermissionStatus.Granted)
                {
                    var permissionResult = await _permissions.RequestPermissionsAsync(Permission.Location);

                    if (permissionResult.First().Value != PermissionStatus.Granted)
                    {
                        _userDialogs.ShowError("Permission denied. Not scanning.");
                        return;
                    }
                }

                status = await _permissions.CheckPermissionStatusAsync(Permission.Storage);
                if (status != PermissionStatus.Granted)
                {
                    var permissionResult = await _permissions.RequestPermissionsAsync(Permission.Storage);

                    if (permissionResult.First().Value != PermissionStatus.Granted)
                    {
                        _userDialogs.ShowError("Permission denied. Cannot Save to Local Storage");
                        return;
                    }
                }
            }
        }

        private async Task<bool> CheckIfPermissionsGranted()
        {
            if (Xamarin.Forms.Device.OS == Xamarin.Forms.TargetPlatform.Android)
            {
                /*var status = await _permissions.CheckPermissionStatusAsync(Permission.Storage);
                if (status != PermissionStatus.Granted)
                    return false;*/

                var status = await _permissions.CheckPermissionStatusAsync(Permission.Location);
                if (status != PermissionStatus.Granted)
                    return false;

                return true;
            }

            return true;
        }

        private async void ScanForDevices()
        {
            Devices.Clear();

            foreach (var connectedDevice in Adapter.ConnectedDevices)
            {
                //update rssi for already connected evices (so tha 0 is not shown in the list)
                try
                {
                    await connectedDevice.UpdateRssiAsync();
                }
                catch (Exception ex)
                {
                    Mvx.Trace(ex.Message);
                    _userDialogs.ShowError($"Failed to update RSSI for {connectedDevice.Name}");
                }

                AddOrUpdateDevice(connectedDevice);
            }

            _cancellationTokenSource = new CancellationTokenSource();
            RaisePropertyChanged(() => StopScanCommand);

            RaisePropertyChanged(() => IsRefreshing);
            Adapter.ScanMode = ScanMode.LowLatency;
            await Adapter.StartScanningForDevicesAsync(_cancellationTokenSource.Token);
        }

        private void CleanupCancellationToken()
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;
            RaisePropertyChanged(() => StopScanCommand);
        }

        private async void DisconnectDevice(DeviceListItemViewModel device)
        {
            try
            {
                if (!device.IsConnected)
                    return;

                _userDialogs.ShowLoading($"Disconnecting {device.Name}...");

                await Adapter.DisconnectDeviceAsync(device.Device);
            }
            catch (Exception ex)
            {
                _userDialogs.Alert(ex.Message, "Disconnect error");
            }
            finally
            {
                device.Update();
                _userDialogs.HideLoading();
            }
        }

        private void HandleSelectedDevice(DeviceListItemViewModel device)
        {
            var config = new ActionSheetConfig();

            if (device.IsConnected)
            {
                config.Add("Update RSSI", async () =>
                {
                    try
                    {
                        _userDialogs.ShowLoading();

                        await device.Device.UpdateRssiAsync();
                        device.RaisePropertyChanged(nameof(device.Rssi));

                        _userDialogs.HideLoading();

                        _userDialogs.ShowSuccess($"RSSI updated {device.Rssi}", 1000);
                    }
                    catch (Exception ex)
                    {
                        _userDialogs.HideLoading();
                        _userDialogs.ShowError($"Failed to update rssi. Exception: {ex.Message}");
                    }
                });

                config.Destructive = new ActionSheetOption("Disconnect", () => DisconnectCommand.Execute(device));
            }
            else
            {
                config.Add("Download Data", async () =>
                {
                    await RequestPermissions();
                    if (await ConnectDeviceAsync(device, false))
                    {
                        bool isCancelPressed = false;
                        var progressDialogConfig = new ProgressDialogConfig()
                        {
                            Title = $"Reading Data",
                            CancelText = "Cancel",
                            IsDeterministic = false,
                            OnCancel = new Action(delegate ()
                            {
                                isCancelPressed = true;
                            })
                        };

                        using (_userDialogs.Progress(progressDialogConfig))
                        {
                            try
                            {
                                StopScanCommand.Execute(null);

                                if (device.Device == null)
                                {
                                    _userDialogs.Alert("Failed to connect");
                                    return;
                                }

                                var mtuValue = await device.Device.RequestMtuAsync(247);

                                var servicesFound = await device.Device.GetServicesAsync();
                                var desiredService = servicesFound.LastOrDefault(x => x.Id.ToString().Contains("113"));
                                if (servicesFound == null || servicesFound.Count == 0 || desiredService == null)
                                {
                                    _userDialogs.Alert("Failed to find services");
                                    return;
                                }

                                var characteristicsFound = await desiredService.GetCharacteristicsAsync();
                                var desiredCharacteristic = characteristicsFound.LastOrDefault(x => x.Id.ToString().Contains("1132"));
                                if (characteristicsFound == null || characteristicsFound.Count == 0 || desiredCharacteristic == null || !desiredCharacteristic.CanWrite)
                                {
                                    _userDialogs.Alert("Failed to find characteristics");
                                    return;
                                }

                                var data = GetBytesFromString("11");
                                var isWritten = await desiredCharacteristic.WriteAsync(data);
                                if (!isWritten)
                                {
                                    _userDialogs.Alert("Failed to read data");
                                    return;
                                }
                                var readingResult = await desiredCharacteristic.ReadAsync();

                                var totalDownloadedBytes = new List<byte>();
                                totalDownloadedBytes.AddRange(readingResult.Skip(1));//skip sequence byte
                                do
                                {
                                    if (isCancelPressed)
                                    {
                                        await Adapter.DisconnectDeviceAsync(device.Device);
                                        return;
                                    }

                                    data = GetBytesFromString("12");
                                    isWritten = await desiredCharacteristic.WriteAsync(data);
                                    if (!isWritten)
                                    {
                                        _userDialogs.Alert("Failed to read data");
                                        return;
                                    }
                                    readingResult = await desiredCharacteristic.ReadAsync();
                                    totalDownloadedBytes.AddRange(readingResult.Skip(1));//skip sequence byte
                                } while (readingResult.Count() > 1);

                                String filename = "X-lab" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + ".csv";
                                _fileWorker.SaveBytes(filename, totalDownloadedBytes.ToArray());

                                await Adapter.DisconnectDeviceAsync(device.Device);
                                _userDialogs.Alert("Data downloaded and saved to /Android/data/XLab");
                            }
                            catch (Exception e)
                            {
                                _userDialogs.Alert("Failed to download data");
                            }
                        }
                    }
                });

                config.Add("Send RTC Info", async () =>
                {
                    if (await ConnectDeviceAsync(device, false))
                    {
                        using (_userDialogs.Loading("Writing Data"))
                        {
                            try
                            {
                                StopScanCommand.Execute(null);

                                if (device.Device == null)
                                {
                                    _userDialogs.Alert("Failed to connect");
                                    return;
                                }

                                var servicesFound = await device.Device.GetServicesAsync();
                                var desiredService = servicesFound.LastOrDefault(x => x.Id.ToString().Contains("113"));
                                if (servicesFound == null || servicesFound.Count == 0 || desiredService == null)
                                {
                                    _userDialogs.Alert("Failed to find services");
                                    return;
                                }

                                var characteristicsFound = await desiredService.GetCharacteristicsAsync();
                                var desiredCharacteristic = characteristicsFound.LastOrDefault(x => x.Id.ToString().Contains("1132"));
                                if (characteristicsFound == null || characteristicsFound.Count == 0 || desiredCharacteristic == null || !desiredCharacteristic.CanWrite)
                                {
                                    _userDialogs.Alert("Failed to find characteristics");
                                    return;
                                }

                                var rtcCommandData = GetBytesFromString("13");
                                var epochData = GetBytesFromSeconds(GetSecondsSinceUnixEpoch(DateTime.Now.Year, DateTime.Now.Month,
                                    DateTime.Now.Day, DateTime.Now.Hour, DateTime.Now.Minute));
                                var combinedDataArray = new byte[rtcCommandData.Length + epochData.Length];
                                Buffer.BlockCopy(rtcCommandData, 0, combinedDataArray, 0, rtcCommandData.Length);
                                Buffer.BlockCopy(epochData, 0, combinedDataArray, rtcCommandData.Length, epochData.Length);

                                var isWritten = await desiredCharacteristic.WriteAsync(combinedDataArray);
                                if (!isWritten)
                                {
                                    _userDialogs.Alert("Failed to write data");
                                    return;
                                }

                                await Adapter.DisconnectDeviceAsync(device.Device);
                                _userDialogs.Alert("RTC Info Sent");
                            }
                            catch (Exception e)
                            {
                                _userDialogs.Alert("Failed to set RTC info");
                            }
                        }
                    }
                });

                /*config.Add("Connect", async () =>
                {
                    if (await ConnectDeviceAsync(device))
                    {
                        ShowViewModel<ServiceListViewModel>(new MvxBundle(new Dictionary<string, string> { { DeviceIdKey, device.Device.Id.ToString() } }));
                    }
                });

                //config.Add("Connect & Dispose", () => ConnectDisposeCommand.Execute(device));
                config.Add("Save Advertised Data", () =>
                {
                    if (device.Device.AdvertisementRecords == null || device.Device.AdvertisementRecords.Count == 0)
                    {
                        _userDialogs.Alert("No Data Found");
                        return;
                    }

                    var advModel = new AdvertisementData()
                    {
                        Id = Guid.NewGuid(),
                        DeviceId = device.Id.ToString(),
                        Name = device.Name,
                        Data = device.Device.AdvertisementRecords[0].Data
                    };
                    _advertisementDataRepository.InsertDevice(advModel);
                });*/
            }

            //config.Add("Copy ID", () => CopyGuidCommand.Execute(device));
            config.Cancel = new ActionSheetOption("Cancel");
            config.SetTitle("Device Options");
            _userDialogs.ActionSheet(config);
        }

        private async Task<bool> ConnectDeviceAsync(DeviceListItemViewModel device, bool showPrompt = true)
        {
            CancellationTokenSource tokenSource = new CancellationTokenSource();
            var config = new ProgressDialogConfig()
            {
                Title = $"Connecting to '{device.Id}'",
                CancelText = "Cancel",
                IsDeterministic = false,
                OnCancel = tokenSource.Cancel
            };

            if (showPrompt && !await _userDialogs.ConfirmAsync($"Connect to device '{device.Name}'?"))
            {
                return false;
            }
            try
            {
                using (var progress = _userDialogs.Progress(config))
                {
                    progress.Show();
                    
                    //switch forceBleTransport to false for non ble connections
                    await Adapter.ConnectToDeviceAsync(device.Device, new ConnectParameters(autoConnect: UseAutoConnect, forceBleTransport: true), tokenSource.Token); 
                }

                _userDialogs.ShowSuccess($"Connected to {device.Device.Name}.");
                return true;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("133")) //retry once
                {
                    using (var progress = _userDialogs.Progress(config))
                    {
                        progress.Show();
                        await Task.Delay(500);
                        var reconnectionSucceeded = false;
                        int reconnectionCounter = 0;

                        do
                        {
                            try
                            {
                                //switch forceBleTransport to false for non ble connections
                                await Adapter.ConnectToDeviceAsync(device.Device, new ConnectParameters(autoConnect: UseAutoConnect, forceBleTransport: true), tokenSource.Token);
                                reconnectionSucceeded = true;
                            }
                            catch(Exception e)
                            {
                                if (tokenSource.IsCancellationRequested)
                                    break;
                                reconnectionCounter ++;
                            }
                        } while (!reconnectionSucceeded && reconnectionCounter <= 4);

                        if(reconnectionSucceeded)
                        {
                            _userDialogs.ShowSuccess($"Connected to {device.Device.Name}.");
                            return true;
                        }
                    }
                }

                Mvx.Trace(ex.Message);
                _userDialogs.Alert(ex.Message, "Connection error");
                return false;
            }
            finally
            {
                _userDialogs.HideLoading();
                device.Update();
            }
        }

        private void OnDeviceDisconnected(object sender, DeviceEventArgs e)
        {
            Devices.FirstOrDefault(d => d.Id == e.Device.Id)?.Update();
            _userDialogs.HideLoading();
            //_userDialogs.Toast($"Disconnected {e.Device.Name}");
        }

        private void SendDbEmail ()
        {
            var savedData = _advertisementDataRepository.GetAllDevicesData();
            var emailMessenger = CrossMessaging.Current.EmailMessenger;
            if (emailMessenger.CanSendEmail)
            {
                var email = new EmailMessageBuilder()
                  .Subject("BLE Test Data")
                  .Body(JsonConvert.SerializeObject(savedData))
                  //.WithAttachment(_sqliteProvider.DbPath, "*/*")
                  .Build();

                emailMessenger.SendEmail(email);
            }
        }

        private static byte[] GetBytesFromString(string text)
        {
            return text.Split(' ').Where(token => !string.IsNullOrEmpty(token)).Select(token => Convert.ToByte(token, 16)).ToArray();
        }

        private static byte[] GetBytesFromSeconds(long seconds)
        {
            byte[] b = new byte[] { 10, 12, 12, 12 };
            var bytesResult = BitConverter.GetBytes(seconds);
            Array.Copy(bytesResult, 0, b, 0, 4);
            return b;
        }

        private readonly DateTime UnixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private long GetSecondsSinceUnixEpoch(int year, int month, int day,
                                                  int hour, int minute)
        {
            DateTime local = new DateTime(year, month, day, hour, minute, 0,
                                          DateTimeKind.Local);
            DateTime utc = local.ToUniversalTime();
            return (long)(utc - UnixEpoch).TotalSeconds;
        }

        public MvxCommand<DeviceListItemViewModel> CopyGuidCommand => new MvxCommand<DeviceListItemViewModel>(device =>
        {
            CrossClipboard.Current.SetText(device.Id.ToString());
        });
    }
}