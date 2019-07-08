using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Acr.UserDialogs;
using BLE.Client.Helpers;
using MvvmCross.Core.ViewModels;
using PCLStorage;
using Plugin.BLE.Abstractions;
using Plugin.BLE.Abstractions.Contracts;
using Plugin.BLE.Abstractions.EventArgs;
using Plugin.BLE.Abstractions.Extensions;
using Xamarin.Forms;

namespace BLE.Client.ViewModels
{
    public class CharacteristicDetailViewModel : BaseViewModel
    {
        private readonly IUserDialogs _userDialogs;
        private readonly IFileWorker _fileWorker;
        private bool _updatesStarted;
        public ICharacteristic Characteristic { get; private set; }

        public string CharacteristicValue => Characteristic?.Value.ToHexString().Replace("-", " ");

        public ObservableCollection<string> Messages { get; } = new ObservableCollection<string>();

        public string UpdateButtonText => _updatesStarted ? "Stop updates" : "Start updates";

        public string Permissions
        {
            get
            {
                if (Characteristic == null)
                    return string.Empty;

                return (Characteristic.CanRead ? "Read " : "") +
                       (Characteristic.CanWrite ? "Write " : "") +
                       (Characteristic.CanUpdate ? "Update" : "");
            }
        }

        public CharacteristicDetailViewModel(IAdapter adapter, IUserDialogs userDialogs) : base(adapter)
        {
            _userDialogs = userDialogs;
            _fileWorker = DependencyService.Get<IFileWorker>();
        }

        protected override async void InitFromBundle(IMvxBundle parameters)
        {
            base.InitFromBundle(parameters);

            Characteristic = await GetCharacteristicFromBundleAsync(parameters);
        }

        public override void Resume()
        {
            base.Resume();

            if (Characteristic != null)
            {
                return;
            }

            Close(this);
        }
        public override void Suspend()
        {
            base.Suspend();

            if (Characteristic != null)
            {
                StopUpdates();
            }
            
        }

        public MvxCommand ReadCommand => new MvxCommand(() => ReadValueAsync());

        private async Task ReadValueAsync()
        {
            if (Characteristic == null)
                return;

            try
            {
                _userDialogs.ShowLoading("Reading characteristic value...");

                await Characteristic.ReadAsync();

                RaisePropertyChanged(() => CharacteristicValue);

                Messages.Insert(0, $"Read value {CharacteristicValue}");
            }
            catch (Exception ex)
            {
                _userDialogs.HideLoading();
                _userDialogs.ShowError(ex.Message);

                Messages.Insert(0, $"Error {ex.Message}");

            }
            finally
            {
                _userDialogs.HideLoading();
            }

        }

        public MvxCommand SaveDataCommand => new MvxCommand(SaveDataToFileAsync);

        private async void SaveDataToFileAsync()
        {
            /*await ReadValueAsync();
            String filename = "TiBle";// + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + ".txt";
            var folder = await FileSystem.Current.LocalStorage.CreateFolderAsync("myFolder", CreationCollisionOption.OpenIfExists);
            IFile file = await folder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
            await file.WriteAllTextAsync("CharacteristicValue");*/

            await ReadValueAsync();
            String filename = "TiBle" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + ".csv";
            await _fileWorker.SaveTextAsync(filename, CharacteristicValue);
        }

        public MvxCommand WriteCommand => new MvxCommand(WriteValueAsync);

        private async void WriteValueAsync()
        {
            try
            {
                var result =
                    await
                        _userDialogs.PromptAsync("Input a value (as hex whitespace separated)", "Write value",
                            placeholder: CharacteristicValue);

                if (!result.Ok)
                    return;

                var data = GetBytes(result.Text);

                _userDialogs.ShowLoading("Write characteristic value");
                await Characteristic.WriteAsync(data);
                _userDialogs.HideLoading();

                RaisePropertyChanged(() => CharacteristicValue);
                Messages.Insert(0, $"Wrote value {CharacteristicValue}");
            }
            catch (Exception ex)
            {
                _userDialogs.HideLoading();
                _userDialogs.ShowError(ex.Message);
            }

        }

        private static byte[] GetBytes(string text)
        {
            return text.Split(' ').Where(token => !string.IsNullOrEmpty(token)).Select(token => Convert.ToByte(token, 16)).ToArray();
        }

        public MvxCommand ToggleUpdatesCommand => new MvxCommand((() =>
        {
            if (_updatesStarted)
            {
                StopUpdates();
            }
            else
            {
                StartUpdates();
            }
        }));

        private async void StartUpdates()
        {
            try
            {
                _updatesStarted = true;

                Characteristic.ValueUpdated -= CharacteristicOnValueUpdated;
                Characteristic.ValueUpdated += CharacteristicOnValueUpdated;
                await Characteristic.StartUpdatesAsync();
         

                Messages.Insert(0, $"Start updates");

                RaisePropertyChanged(() => UpdateButtonText);

            }
            catch (Exception ex)
            {
                _userDialogs.ShowError(ex.Message);
            }
        }

        private async void StopUpdates()
        {
            try
            {
                _updatesStarted = false;

                await Characteristic.StopUpdatesAsync();
                Characteristic.ValueUpdated -= CharacteristicOnValueUpdated;

                Messages.Insert(0, $"Stop updates");

                RaisePropertyChanged(() => UpdateButtonText);

            }
            catch (Exception ex)
            {
                _userDialogs.ShowError(ex.Message);
            }
        }

        private void CharacteristicOnValueUpdated(object sender, CharacteristicUpdatedEventArgs characteristicUpdatedEventArgs)
        {
            Messages.Insert(0, $"Updated value: {CharacteristicValue}");
            RaisePropertyChanged(() => CharacteristicValue);
        }
    }
}