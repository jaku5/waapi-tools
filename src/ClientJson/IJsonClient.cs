using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using JPAudio.WaapiTools.ClientCore;

namespace JPAudio.WaapiTools.ClientJson
{
    public interface IJsonClient
    {
        event Wamp.DisconnectedHandler Disconnected;
        Task Connect(string uri = "ws://localhost:8080/waapi", int timeout = int.MaxValue);
        void Disconnect();
        Task<JObject> Call(string uri, object args = null, object options = null, int timeout = int.MaxValue);
        Task<JObject> Call(string uri, JObject args, JObject options, int timeout = int.MaxValue);
        Task<int> Subscribe(string topic, object options, JsonClient.PublishHandler publishHandler, int timeout = int.MaxValue);
        Task<int> Subscribe(string topic, JObject options, JsonClient.PublishHandler publishHandler, int timeout = int.MaxValue);
        Task Unsubscribe(int subscriptionId, int timeout = int.MaxValue);
    }
}