using System;
using System.Threading.Tasks;
using System.Windows;

namespace ActormixerSanitizer.UI.Services
{
    public class DispatcherService : IDispatcherService
    {
        public async Task InvokeAsync(Action action)
        {
            if (Application.Current != null)
            {
                await Application.Current.Dispatcher.InvokeAsync(action);
            }
        }
    }
}
