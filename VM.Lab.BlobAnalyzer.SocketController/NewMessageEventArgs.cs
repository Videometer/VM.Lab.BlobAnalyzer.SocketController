using System;
using System.Runtime.CompilerServices;

[assembly:InternalsVisibleTo("VM.Lab.BlobAnalyzer.SocketController.UnitTest", AllInternalsVisible =true)]
namespace VM.BlobAnalyzer.SocketController
{
    internal class NewMessageEventArgs : EventArgs
    {
        public string Value;
    }
}