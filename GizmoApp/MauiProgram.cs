using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using CommunityToolkit.Maui;

// Füge das Windows-Namespace hinzu, um auf die Fenstermethoden zuzugreifen
#if WINDOWS
using Microsoft.UI.Windowing;
using Microsoft.UI;
#endif

namespace GizmoApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureLifecycleEvents(events =>
                {
#if WINDOWS
                    events.AddWindows(windows => windows.OnWindowCreated(window =>
                    {
                        // Definiere die gewünschte Größe
                        const int width = 1000;
                        const int height = 900;

                        // Zugriff auf das native Windows-Fenster-Handle (IntPtr)
                        IntPtr nativeWindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
                
                        // Hole die Windows-ID vom Handle
                        WindowId winuiWindowId = Win32Interop.GetWindowIdFromWindow(nativeWindowHandle);
                
                        // Finde den AppWindow Manager
                        AppWindow appWindow = AppWindow.GetFromWindowId(winuiWindowId);
                
                        // Ändere die Größe
                        appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
                
                        // Optional: Positioniere das Fenster in der Mitte des Bildschirms
                        // appWindow.Move(new Windows.Graphics.SizeInt32(x, y)); 

                    }));

#endif
                })

                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("ComicNeueSansID.ttf", "ComicNeueSansID");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
