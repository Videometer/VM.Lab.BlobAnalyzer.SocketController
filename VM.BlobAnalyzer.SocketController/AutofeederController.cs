using System;
using System.Runtime.CompilerServices;
using System.Threading;
using VM.Lab.Interfaces.BlobAnalyzer;

[assembly:InternalsVisibleTo("VM.BlobAnalyzer.SocketController.UnitTest", AllInternalsVisible =true)]
namespace VM.BlobAnalyzer.SocketController
{
	public class AutofeederController : AutofeederControl
	{
		private IMessagingChannel _messageChannel;

		/// <summary>
		///  Creates the controller using a Socket connection on port 8888 as server listening for commands
		/// </summary>
		/// <param name="listener"></param>
		public AutofeederController(IAutofeederControlListener listener) : base(listener)
		{
			const short socketPort = 8888;
			SetChannel(
				new SocketServerChannelWrapper(
					socketPort));
		}
		
		internal AutofeederController(
			IAutofeederControlListener listener, 
			IMessagingChannel channel)
			 : base(listener)
		{
			SetChannel(channel);
		}

		private void SetChannel(IMessagingChannel channel)
		{
			_messageChannel = channel;
			_messageChannel.MessageReceived += (s, e) =>
				MessageReceived(e.Value);
		}

		public void MessageReceived(string message)
		{
			Console.WriteLine($"AutofeederController << received message: {message}");

			var parsedMessage = BlobAnalyzerMessagePacket.FromMessage(message);
			switch (parsedMessage.Command)
			{
				case PacketHeader.START:
					if (registreredState == BlobAnalyzerState.IDLE || registreredState == BlobAnalyzerState.None)
					{
						StateChangedEvent.Reset();
						_listener.LoadRecipe(parsedMessage.RecipeName);
						bool waitOK = StateChangedEvent.WaitOne(5000);
						
						bool correctState = (registreredState == BlobAnalyzerState.IDLE);
						if (correctState )
						{
							var measurementTime = DateTime.Now;
							_listener.Start(
								parsedMessage.SampleId, 
								parsedMessage.Operator, 
								parsedMessage.Comment,
								GetPredictionResultFilename(parsedMessage.SampleId, measurementTime),
								GetBlobCollectionSubfolder(parsedMessage.SampleId, measurementTime));
							BroadcastAndPrint(BlobAnalyzerMessagePacket.Ack(message));
						}
						else
						{
							string reason = $"Autofeeder didnt change to  invalid state {registreredState}, unable to accept start request.";
							BroadcastAndPrint(new BlobAnalyzerMessagePacket
							{
								Command = PacketHeader.NACK,
								ErrorMessage = reason
							}.ToString());
						}
					}
					else
					{
						string reason = $"Autofeeder in invalid state {registreredState}, unable to accept start request.";
						BroadcastAndPrint(new BlobAnalyzerMessagePacket
						{
							Command = PacketHeader.NACK,
							ErrorMessage = reason
						}.ToString());
					}

					break;
				case PacketHeader.ACK:
					break;
				case PacketHeader.SAMPLING_DONE:
					break;
				case PacketHeader.FINISH:
					if (registreredState == BlobAnalyzerState.STOPPED)
					{
						BroadcastAndPrint(BlobAnalyzerMessagePacket.Ack(message));
						_listener.Finish();
					}
					else
					{
						BroadcastAndPrint(new BlobAnalyzerMessagePacket
						{
							Command = PacketHeader.NACK,
							SampleId = parsedMessage.SampleId,
							ErrorMessage = $"Autofeeder not in stopped state, state= {registreredState}"
						}.ToString());
					}
					break;

				// Nacks also include parsing Errors
				case PacketHeader.NACK:
					BroadcastAndPrint(parsedMessage.ToString());
					break;

				case PacketHeader.STOP:
					if (registreredState == BlobAnalyzerState.MEASURING 
						|| registreredState == BlobAnalyzerState.FLUSHING_IDLE
						|| registreredState == BlobAnalyzerState.FLUSHING_NONE
						|| registreredState == BlobAnalyzerState.FLUSHING_STOPPED)
					{
						_listener.Stop();
						BroadcastAndPrint(BlobAnalyzerMessagePacket.Ack(message));
					}
					else
					{
						BroadcastAndPrint(new BlobAnalyzerMessagePacket
						{
							Command = PacketHeader.NACK,
							SampleId = parsedMessage.SampleId,
							ErrorMessage = $"Autofeeder not in running or flushing state, state= {registreredState}"
						}.ToString());

					}
					break;
				case PacketHeader.FLUSH:
					if (registreredState == BlobAnalyzerState.STOPPED 
						|| registreredState == BlobAnalyzerState.IDLE 
						|| registreredState == BlobAnalyzerState.None)
					{
						_listener.Flush();
						BroadcastAndPrint(BlobAnalyzerMessagePacket.Ack(message));
					}
					else
					{
						BroadcastAndPrint(new BlobAnalyzerMessagePacket
						{
							Command = PacketHeader.NACK,
							SampleId = parsedMessage.SampleId,
							ErrorMessage = $"Autofeeder not in stopped or idle state, state= {registreredState}"
						}.ToString());
					}
					break;

				default:
					break;
			}
		}
		
		private void BroadcastAndPrint(string message)
		{
			Console.WriteLine($"AutofeederController >> Sending: {message}");
			_messageChannel.Broadcast(message);
		}

		private AutoResetEvent StateChangedEvent = new AutoResetEvent(false);
		private BlobAnalyzerState registreredState = BlobAnalyzerState.None;

		public override void StateChanged(BlobAnalyzerState newState)
		{
			Console.WriteLine($"AutofeederController: StateChanged({newState})");
			registreredState = newState;
			StateChangedEvent.Set();

			// When we have stopped. alert operator
			if (newState == BlobAnalyzerState.STOPPED)
			{
				BroadcastAndPrint(
					new BlobAnalyzerMessagePacket
					{
						Command = PacketHeader.SAMPLING_DONE
					}.ToString());
			}
		}

		public string GetBlobCollectionSubfolder(string sampleId, DateTime measurementStartTime) =>
			$"{sampleId}_{measurementStartTime.ToString("yyyyMMdd_HHmmss")}";

		public string GetPredictionResultFilename(string sampleId, DateTime measurementStartTime) =>
			 $"PredictionResult_{sampleId}_{measurementStartTime.ToString("yyyyMMdd_HHmmss")}.xlsx";
		
		
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposing)
			{
				_messageChannel?.Dispose();
			}
		}
	}
}
