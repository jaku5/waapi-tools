using System;
using System.Threading.Tasks;

namespace ActormixerSanitizer.UI.Services
{
    public interface IDispatcherService
    {
        Task InvokeAsync(Action action);
    }
}
