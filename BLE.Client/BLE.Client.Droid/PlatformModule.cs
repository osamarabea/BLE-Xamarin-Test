using System;
using Autofac;
using BLE.Client.Droid.Helpers;
using BLE.Client.Helpers;

namespace BLE.Client.Droid
{
    public class PlatformModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            Xamarin.Forms.DependencyService.Register<ISQLite, AndroidSQLite>();

            builder
             .RegisterType<AndroidSQLite>()
             .AsImplementedInterfaces()
             .SingleInstance();

            Xamarin.Forms.DependencyService.Register<IFileWorker, FileWorker_Android>();

            builder
             .RegisterType<FileWorker_Android>()
             .AsImplementedInterfaces()
             .SingleInstance();

            builder.RegisterModule(new CoreModule());
        }
    }
}
