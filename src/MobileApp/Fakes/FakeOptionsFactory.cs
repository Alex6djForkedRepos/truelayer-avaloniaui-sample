using Microsoft.Extensions.Options;
using TrueLayer;

namespace MobileApp.Fakes;

public class FakeOptionsFactory : IOptionsFactory<TrueLayerOptions>
{
    public TrueLayerOptions Create(string name)
    {
        return new TrueLayerOptions { ClientId = "fake-client-id" };
    }
}
