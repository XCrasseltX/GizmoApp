using Microsoft.Extensions.Logging;
using GizmoApp.Service;

namespace GizmoApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            // Plattformimplementierungen von INetworkInfoProvider registrieren
#if ANDROID
            builder.Services.AddSingleton<INetworkInfoProvider, GizmoApp.Platforms.Android.NetworkInfoProvider>();
#endif
#if WINDOWS
            builder.Services.AddSingleton<INetworkInfoProvider, GizmoApp.Platforms.Windows.NetworkInfoProvider>();
#endif

            return builder.Build();
        }
    }
}
