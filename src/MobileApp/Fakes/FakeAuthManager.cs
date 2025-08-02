using System;

namespace MobileApp.Fakes;

public class FakeAuthManager : IAuthManager
{
    #pragma warning disable CS0067
    public event EventHandler<CallbackReceivedEventArgs>? CallbackReceived;
    public void Start() { }
    public void Stop() { }

    public void Dispose() { }
}
