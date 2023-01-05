using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using VM.Lab.Interfaces.BlobAnalyzer;

namespace VM.BlobAnalyzer.SocketController.UnitTest
{
    public class AutofeederControllerTest : IAutofeederControlListener
    {
        [Test]
        public void TestSendingStartMessageInvokesStartAction()
        {
            TestingChannel channel = new TestingChannel();
            using (new AutofeederController(this, channel))
            {
                // This message should pass into the controller that invoked the Start(x,y,z) callback
                string message = "START|lot543887|Corn_2022_v2|CHG|A test measurement";
                channel.GenerateMessage(message);

                // Check that the start method in the controller was invoked as expected
                CallbackLog.TryDequeue(out var loadRecipeCommandInvokeMessage);
                Assert.AreEqual("LoadRecipe(Corn_2022_v2)", loadRecipeCommandInvokeMessage);
                
                // Check that load recipe method in the controller was invoked as expected
                CallbackLog.TryDequeue(out var startCommandInvokeMessage);
                Assert.AreEqual("Start(lot543887, CHG, A test measurement)", startCommandInvokeMessage);
                
                // Check that the correct reply was replied on the channel
                channel.BroadcastedMessages.TryDequeue(out var broadcastedReply);
                Assert.AreEqual("ACK|"+message, broadcastedReply);
            }
        }
        
        [Test]
        public void TestSendingStopMessageAfterStartInvokesStopAction()
        {
            TestingChannel channel = new TestingChannel();
            using (var controller = new AutofeederController(this, channel))
            {
                // This message should pass into the controller that invoked the Start(x,y,z) callback
                channel.GenerateMessage("START|lot543887|Corn_2022_v2|CHG|A test measurement");
                CallbackLog.TryDequeue(out _); // dont care about start response in this test...
                channel.BroadcastedMessages.TryDequeue(out _); // We also dont care about the broadcast reply for start...
                controller.StateChanged(BlobAnalyzerState.IDLE);
                channel.GenerateMessage("STOP");

                // Check that the correct method in the controller was invoked as expected
                CallbackLog.TryDequeue(out string lastCommand);
                Assert.AreEqual(StopCommandInvokeMessage, lastCommand);
                
                // Check that the correct reply was replied on the channel
                channel.BroadcastedMessages.TryDequeue(out var broadcastedReply);
                Assert.AreEqual("ACK|STOP", broadcastedReply);
            }
        }

        private class TestingChannel : IMessagingChannel
        {
            /// <summary>
            /// Register of the messages that was broad casted, these are the replies from the real controller.
            /// </summary>
            public ConcurrentQueue<string> BroadcastedMessages = new ConcurrentQueue<string>();

            public void GenerateMessage(string x) =>
                MessageReceived.Invoke(this, new NewMessageEventArgs { Value = x });
            
            public void Dispose()  { }

            public event EventHandler<NewMessageEventArgs> MessageReceived;
            

            public void Broadcast(string message)
            {
                BroadcastedMessages.Enqueue(message);
            }
        }

        public ConcurrentQueue<string> CallbackLog = new ConcurrentQueue<string>();
        
        public void LoadRecipe(string recipeName) =>
            CallbackLog.Enqueue($"{nameof(LoadRecipe)}({recipeName})");

        public void Start(string id, string initials, string comments, string predictionResiltFilename, string blobCollectionName) =>
            CallbackLog.Enqueue($"{nameof(Start)}({id}, {initials}, {comments})");

        const string StopCommandInvokeMessage = "Stop invoked";
        public void Stop() =>
            CallbackLog.Enqueue(StopCommandInvokeMessage);

        public void Flush()
        {
            throw new NotImplementedException();
        }

        public void Finish()
        {
            throw new NotImplementedException();
        }
        
    }
}