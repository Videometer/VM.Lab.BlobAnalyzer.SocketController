using System;
using System.Runtime.CompilerServices;
using System.Threading;
using VM.Lab.Interfaces.BlobAnalyzer;

[assembly:InternalsVisibleTo("VM.Lab.BlobAnalyzer.SocketController.UnitTest", AllInternalsVisible =true)]
namespace VM.Lab.BlobAnalyzer.SocketController
{
	public class BlobAnalyzerSocketController : AutofeederControl
	{
		private IMessagingChannel _messageChannel;

		/// <summary>
		///  Creates the controller using a Socket connection on port 8888 as server listening for commands
		/// </summary>
		/// <param name="listener"></param>
		public BlobAnalyzerSocketController(IAutofeederControlListener listener) : base(listener)
		{
			Console.WriteLine($"Started {nameof(BlobAnalyzerSocketController)}");
			const short socketPort = 8888;
			SetChannel(
				new SocketServerChannelWrapper(
					socketPort));
		}
		
		internal BlobAnalyzerSocketController(
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
			Console.WriteLine($"BlobAnalyzerSocketController << received message: {message}");

			var parsedMessage = BlobAnalyzerMessagePacket.FromMessage(message);
			switch (parsedMessage.Command)
			{
				case PacketHeader.START:
					if (_registreredState == BlobAnalyzerState.IDLE || _registreredState == BlobAnalyzerState.None)
					{
						StateChangedEvent.Reset();
						_listener.LoadRecipe(parsedMessage.RecipeName);
						
						// Wait till some state has changed
						bool waitOK = StateChangedEvent.WaitOne(5000);

						if (_registreredState == BlobAnalyzerState.LOADING_RECIPE)
						{
							// If it's still loading recipe, wait til next state change
							var waitOK_LOADING = StateChangedEvent.WaitOne(20000);
						}

						bool correctState = (_registreredState == BlobAnalyzerState.IDLE);
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
							string reason = $"Blob Analyzer didnt change to  invalid state {_registreredState}, unable to accept start request.";
							BroadcastAndPrint(new BlobAnalyzerMessagePacket
							{
								Command = PacketHeader.NACK,
								ErrorMessage = reason
							}.ToString());
						}
					}
					else
					{
						string reason = $"Blob Analyzer in invalid state {_registreredState}, unable to accept start request.";
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
					if (_registreredState == BlobAnalyzerState.STOPPED)
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
							ErrorMessage = $"Autofeeder not in stopped state, state= {_registreredState}"
						}.ToString());
					}
					break;

				// Nacks also include parsing Errors
				case PacketHeader.NACK:
					BroadcastAndPrint(parsedMessage.ToString());
					break;

				case PacketHeader.STOP:
					if (_registreredState == BlobAnalyzerState.MEASURING 
						|| _registreredState == BlobAnalyzerState.FLUSHING_IDLE
						|| _registreredState == BlobAnalyzerState.FLUSHING_NONE
						|| _registreredState == BlobAnalyzerState.FLUSHING_STOPPED)
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
							ErrorMessage = $"Blob Analyzer not in running or flushing state, state= {_registreredState}"
						}.ToString());

					}
					break;
				case PacketHeader.FLUSH:
					if (_registreredState == BlobAnalyzerState.STOPPED 
						|| _registreredState == BlobAnalyzerState.IDLE 
						|| _registreredState == BlobAnalyzerState.None)
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
							ErrorMessage = $"Autofeeder not in stopped or idle state, state= {_registreredState}"
						}.ToString());
					}
					break;
			}
		}
		
		private void BroadcastAndPrint(string message)
		{
			Console.WriteLine($"BlobAnalyzerSocketController >> Sending: {message}");
			_messageChannel.Broadcast(message);
		}

		private AutoResetEvent StateChangedEvent = new AutoResetEvent(false);
		private BlobAnalyzerState _registreredState = BlobAnalyzerState.None;

		public override void StateChanged(BlobAnalyzerState newState)
		{
			Console.WriteLine($"BlobAnalyzerSocketController: StateChanged({newState})");
			_registreredState = newState;
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

		public override void BroadcastError()
		{
			BroadcastAndPrint(
				new BlobAnalyzerMessagePacket
				{
					Command = PacketHeader.ERROR
				}.ToString());
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
