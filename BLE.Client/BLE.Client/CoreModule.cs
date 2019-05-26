using System;
using System.Reflection;
using Autofac;
using BLE.Client.Repositories;
using BLE.Client.ViewModels;
using MvvmCross.Platform;
using Module = Autofac.Module;

namespace BLE.Client
{
    public class CoreModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);
            Xamarin.Forms.DependencyService.Register<IAdvertisementDataRepository, AdvertisementDataRepository>();
            builder.RegisterType<DeviceListViewModel>().SingleInstance();
        }
    }
}
