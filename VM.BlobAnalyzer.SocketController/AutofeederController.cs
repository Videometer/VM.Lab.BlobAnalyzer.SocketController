using System;
using System.Data;
using System.Runtime.CompilerServices;
using VM.Lab.Interfaces.Autofeeder;

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

		private BlobAnalyzerMessagePacket _lastStartMessage;
		public void MessageReceived(string message)
		{
			Console.WriteLine($"AutofeederController << received message: {message}");

			var parsedMessage = BlobAnalyzerMessagePacket.FromMessage(message);
			switch (parsedMessage.Command)
			{
				case PacketHeader.START:
					if (registreredState == AutofeederState.Idle || registreredState == AutofeederState.Stopped)
					{
						_lastStartMessage = parsedMessage;
						_listener.Start(parsedMessage.SampleId, parsedMessage.Operator, parsedMessage.Comment)
							.Start();
						BroadcastAndPrint(BlobAnalyzerMessagePacket.Ack(message));
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
					if (registreredState == AutofeederState.Stopped)
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
					if (registreredState == AutofeederState.Running || registreredState == AutofeederState.Flushing)
					{
						_listener.Stop(WaitCondition.Wait_All_Queues_Empty);
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
					if (registreredState == AutofeederState.Stopped || registreredState == AutofeederState.Idle)
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

		private AutofeederState registreredState = AutofeederState.Idle;

		public override void StateChanged(AutofeederState oldState, AutofeederState newState, string sampleId, DataTable result)
		{
			Console.WriteLine($"AutofeederController: StateChanged({oldState}, {newState}, {sampleId}, ...)");
			registreredState = newState;

			// When we have stopped. alert operator
			if ((oldState == AutofeederState.Stopping || oldState == AutofeederState.Flushing)
				 && newState == AutofeederState.Stopped)
			{
				BroadcastAndPrint(new BlobAnalyzerMessagePacket { Command = PacketHeader.SAMPLING_DONE, SampleId = sampleId }.ToString());
			}
		}

		public override string GetBlobCollectionSubfolder(DateTime measurementStartTime)
		{
			if (_lastStartMessage.Command != PacketHeader.START)
				return null;

			return $"{_lastStartMessage.SampleId}_{measurementStartTime.ToString("yyyyMMdd_HHmmss")}";
		}

		public override string GetPredictionResultFilename(DateTime measurementStartTime)
		{
			if (_lastStartMessage.Command != PacketHeader.START)
				return null;
			
			return $"PredictionResult_{_lastStartMessage.SampleId}_{measurementStartTime.ToString("yyyyMMdd_HHmmss")}.xlsx";
		}
		
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
