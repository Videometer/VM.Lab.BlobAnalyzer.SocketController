using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VM.Lab.Interfaces.Autofeeder;

namespace VM.BlobAnalyzer.SocketController.UnitTest
{
    public class AutofeederControllerTest : IAutofeederControlListener
    {
        [Test]
        public void TestSendingStartMessageInvokesStartAction()
        {
            TestingChannel channel = new TestingChannel((x) => { });
            channel.MessageReceived += (sender, args) => { };

            AutofeederController controller = new AutofeederController(this, channel);
            channel.GenerateMessage("START|lot543887|Corn_2022_v2|CHG|A test measurement");
            
            Thread.Sleep(500);
            CallbackLog.TryDequeue(out string startCommandWasInvoked);
            Assert.AreEqual("lot543887, CHG, A test measurement", startCommandWasInvoked);

        }

        private class TestingChannel : IMessagingChannel
        {
            private readonly Action<string> _broadcasted;

            public TestingChannel(Action<string> Broadcasted)
            {
                _broadcasted = Broadcasted;
            }

            public void GenerateMessage(string x) =>
                MessageReceived.Invoke(this, new NewMessageEventArgs { Value = x });
            
            public void Dispose()  { }

            public event EventHandler<NewMessageEventArgs> MessageReceived;
            public void Broadcast(string message)
            {
                _broadcasted(message);
            }
        }

        public ConcurrentQueue<string> CallbackLog = new ConcurrentQueue<string>();

        public Task Start(string id, string initials, string comments) => new Task( () =>
            CallbackLog.Enqueue($"{id}, {initials}, {comments}"));
            
        

        public Task Stop(WaitCondition waitCondition, bool doFlush = false)
        {
            throw new NotImplementedException();
        }

        public Task Flush()
        {
            throw new NotImplementedException();
        }

        public Task Finish()
        {
            throw new NotImplementedException();
        }
    }
}