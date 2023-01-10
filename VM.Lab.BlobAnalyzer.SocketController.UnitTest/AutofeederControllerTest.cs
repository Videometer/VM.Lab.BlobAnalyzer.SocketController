using System;
using System.Collections.Concurrent;
using NUnit.Framework;
using VM.BlobAnalyzer.SocketController;
using VM.Lab.Interfaces.BlobAnalyzer;

namespace VM.Lab.BlobAnalyzer.SocketController.UnitTest
{
    public class AutofeederControllerTest : IAutofeederControlListener
    {
        [Test]
        public void TestSendingStartMessageInvokesStartAction()
        {
            TestingChannel channel = new TestingChannel();
            using (var controller = new BlobAnalyzerSocketController(this, channel))
            {
                // Pretend a recipe is loading by setting state to IDLE
                controller.StateChanged(BlobAnalyzerState.IDLE);
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
        
        /// <summary>
        /// Test that we can send stop command for all relevant states.
        /// </summary>
        /// <param name="sourceState"></param>
        [Test]
        public void TestSendingStopMessageInvokesStopAction(
        [Values(BlobAnalyzerState.MEASURING, BlobAnalyzerState.FLUSHING_IDLE,  BlobAnalyzerState.FLUSHING_NONE, BlobAnalyzerState.FLUSHING_STOPPED)] BlobAnalyzerState sourceState)
        {
            TestingChannel channel = new TestingChannel();
            using (var controller = new BlobAnalyzerSocketController(this, channel))
            {
                // Pretend we are measuring, which should be possible to sto
                controller.StateChanged(sourceState);
                channel.GenerateMessage("STOP");

                // Check that the correct method in the controller was invoked as expected
                CallbackLog.TryDequeue(out string lastCommand);
                Assert.AreEqual(StopCommandInvokeMessage, lastCommand);
                
                // Check that the correct reply was replied on the channel
                channel.BroadcastedMessages.TryDequeue(out var broadcastedReply);
                Assert.AreEqual("ACK|STOP", broadcastedReply);
            }
        }
        
        [Test]
        public void TestSendingStopIsIgnoredFromSomeInvalidStates(
            [Values(BlobAnalyzerState.None, BlobAnalyzerState.LOADING_RECIPE,  BlobAnalyzerState.IDLE)] BlobAnalyzerState sourceState)
        {
            TestingChannel channel = new TestingChannel();
            using (var controller = new BlobAnalyzerSocketController(this, channel))
            {
                // Pretend we are measuring, which should be possible to sto
                controller.StateChanged(sourceState);
                channel.GenerateMessage("STOP");

                // Check that the correct method in the controller was invoked as expected
                CallbackLog.TryDequeue(out string lastCommand);
                Assert.AreEqual(null, lastCommand);
                
                // Check that the correct reply was replied on the channel
                channel.BroadcastedMessages.TryDequeue(out var broadcastedReply);
                Assert.IsTrue(
                    broadcastedReply
                        .StartsWith(
                            "NACK|Blob Analyzer not in running or flushing state"));
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