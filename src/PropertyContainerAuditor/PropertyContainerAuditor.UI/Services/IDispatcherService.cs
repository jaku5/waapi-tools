using System;
using System.Threading.Tasks;

namespace PropertyContainerAuditor.UI.Services
{
    public interface IDispatcherService
    {
        Task InvokeAsync(Action action);
    }
}
