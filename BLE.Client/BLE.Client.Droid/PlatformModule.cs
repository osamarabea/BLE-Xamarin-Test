using System;
using Autofac;
using BLE.Client.Droid.Helpers;

namespace BLE.Client.Droid
{
    public class PlatformModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder
             .RegisterType<AndroidSQLite>()
             .AsImplementedInterfaces()
             .SingleInstance();

            builder.RegisterModule(new CoreModule());
        }
    }
}
