using System;
using System.Runtime.CompilerServices;
using VM.Communication;

[assembly:InternalsVisibleTo("VM.Lab.BlobAnalyzer.SocketController.UnitTest", AllInternalsVisible =true)]
namespace VM.BlobAnalyzer.SocketController
{
    /// <summary>
    /// Messaging channel to abstract away socket server to allow testing the Controller
    /// </summary>
    internal interface IMessagingChannel : IDisposable
    {
        event EventHandler<NewMessageEventArgs> MessageReceived;
        void Broadcast(string message);
    }

    internal sealed class SocketServerChannelWrapper : IMessagingChannel
    {
        private readonly SocketServer _socketServer;

        public SocketServerChannelWrapper(short socketPort)
        {
            _socketServer = new SocketServer((x) => 
                MessageReceived?.Invoke(
                    this, 
                    new NewMessageEventArgs
                    {
                        Value = x
                    }));
            _socketServer.Start(socketPort);
        }

        public void Dispose()
        {
            if (_socketServer.IsRunning)
            {
                _socketServer.Stop();
            }
        }

        public event EventHandler<NewMessageEventArgs> MessageReceived;

        public void Broadcast(string message) =>
            _socketServer.Broadcast(message);
        
    }
}