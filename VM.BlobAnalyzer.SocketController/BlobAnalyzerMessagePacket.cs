using System;
using System.Collections.Generic;	
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VM.BlobAnalyzer.SocketController
{
    /// <summary>
    /// Converts messages between string and a dataobject for easy use in the code 
    /// </summary>
    public struct BlobAnalyzerMessagePacket
    {
        public static BlobAnalyzerMessagePacket FromMessage(string message)
        {
            var result = new BlobAnalyzerMessagePacket();
			if (message != null)
				message = message.TrimEnd((char)0);

            if (message == null || message.Length == 0)
            {
                result.Command = PacketHeader.UNKNOWN;
                return result;
            }

            const char divider = '|';
            string[] tokens = message.Split(divider);

            if (tokens.Length == 0)
            {
                result.Command = PacketHeader.UNKNOWN;
                return result;
            }
            
            if (Enum.TryParse(tokens[0], out PacketHeader parsedProcessingHeader))
            {
                result.Command = parsedProcessingHeader;
            }
            else
            {
                result.Command = PacketHeader.UNKNOWN;
            }
            
            try
            {
                if (result.Command == PacketHeader.ACK)
                {
                    if (Enum.TryParse(tokens[1], out PacketHeader ackPacketHeader))
                    {
                        result.AckPackageCommand =  ackPacketHeader;
                        parsedProcessingHeader = ackPacketHeader;
                        
                        // remove first token as the internals than should match 🎠
                        tokens = tokens
                            .Skip(1)
                            .ToArray();
                    }
                    else
                    {
                        result.Command = PacketHeader.NACK;
                        result.ErrorMessage = "Unable to interpret acknowledge, unknown command acknowledged";
                        return result;
                    }
                }

                result.SampleId = tokens[1];
                switch (parsedProcessingHeader)
                {
                    //     0          1               2          3                   4 
                    //"START|     1299 | example_recipe |   300310 |          COMMENTS
                    //"START| SampleID |    RECIPE_NAME | OPERATOR | Here are comments
                    case PacketHeader.START:
                        result.RecipeName = tokens[2];
                        result.Operator = tokens[3];
                        result.Comment = tokens[4];
                        break;
                    case PacketHeader.NACK:
                        result.SampleId = null;
                        result.ErrorMessage = tokens[1];
                        break;
                }
            }
            catch (FormatException fe)
            {
                result.Command = PacketHeader.NACK;
                result.ErrorMessage = fe.Message;
            }
            catch (ArgumentNullException ane)
            {
                result.Command = PacketHeader.NACK;
                result.ErrorMessage = ane.Message;
            }
            catch (IndexOutOfRangeException iora)
            {
                result.Command = PacketHeader.NACK;
                result.ErrorMessage = iora.Message + " - " + "Most likely some parameter is missing from the message.";
            }
            catch (ArgumentException ae)
            {
                result.Command = PacketHeader.NACK;
                result.ErrorMessage = ae.Message;
            }
            return result;
        }

        public string Comment { get; set; }

        public string Operator { get; set; }

        // Recipe name to be loaded from the current workspace
        public string RecipeName { get; set; }
        
        public static string Ack(string originalMessage)
        {
            return $"ACK|{originalMessage}";
        }

        public PacketHeader Command { get; set; }
        public string ErrorMessage { get; set; }
        public string SampleId { get; set; }

        public PacketHeader AckPackageCommand { get; set; }
        
        /// <summary>
        /// Creates a string, matching a string representation for sending via socket communication
        /// </summary>
        /// <returns></returns>
        public override string ToString() => (Command == PacketHeader.ACK)
            ? $"{Command}|{ToString(AckPackageCommand)}"
            : ToString(Command);
        
        private string ToString(PacketHeader commandTranslation)
        {
            switch (commandTranslation)
            {
                case PacketHeader.START:
                    return $"{commandTranslation}|{SampleId}|{RecipeName}|{Operator}|{Comment}";
                case PacketHeader.SAMPLING_DONE:
                    break;
                case PacketHeader.NACK:
                    return $"{commandTranslation}|{ErrorMessage}";
            }
            // Fallback to header + ActivityID
            return $"{commandTranslation}|{SampleId}";
        }
    }

    /// <summary>
    /// The header possibilites for a packet
    /// </summary>
    public enum PacketHeader
    {
        UNKNOWN,
        START,
        ACK,
        SAMPLING_DONE,
        NACK,
        FINISH,
        STOP,
        FLUSH,
    }
}
