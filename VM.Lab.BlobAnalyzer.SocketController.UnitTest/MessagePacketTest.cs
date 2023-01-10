using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VM.BlobAnalyzer.SocketController;

namespace VM.Lab.BlobAnalyzer.SocketController.UnitTest
{
    [TestFixture]
    public class MessagePacketTest
    {
        public static IEnumerable<object> MessagePackagesToSocketCommandMapping()
        {
            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.START,
                    SampleId = "lot543887",
                    RecipeName = "Corn_2022_v2",
                    Operator = "CHG",
                    Comment = "A test measurement"
                },
                "START|lot543887|Corn_2022_v2|CHG|A test measurement"
            };
            
            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.ACK,
                    AckPackageCommand= PacketHeader.START,
                    SampleId = "lot543887",
                    RecipeName = "Corn_2022_v2",
                    Operator = "CHG",
                    Comment = "A test measurement"
                },
                "ACK|START|lot543887|Corn_2022_v2|CHG|A test measurement"
            };

            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.SAMPLING_DONE,
                    SampleId = "lot543887"
                },
                "SAMPLING_DONE|lot543887"
            };
            
            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.ACK,
                    AckPackageCommand =  PacketHeader.SAMPLING_DONE,
                    SampleId = "lot543887"
                },
                "ACK|SAMPLING_DONE|lot543887"
            };
            
            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.FINISH
                },
                "FINISH"
            };
            
            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.ACK,
                    AckPackageCommand = PacketHeader.FINISH,
                },
                "ACK|FINISH"
            };
            
            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.STOP
                },
                "STOP"
            };
            
            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.ACK,
                    AckPackageCommand = PacketHeader.STOP,
                },
                "ACK|STOP"
            };
            
            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.FLUSH
                },
                "FLUSH"
            };
            yield return new object[]
            {
                new BlobAnalyzerMessagePacket
                {
                    Command = PacketHeader.ACK,
                    AckPackageCommand = PacketHeader.FLUSH
                },
                "ACK|FLUSH"
            };
        }
        
        [Test, TestCaseSource(nameof(MessagePackagesToSocketCommandMapping))]
        public void TestGenerateStringFromMessage(
            BlobAnalyzerMessagePacket packetRepresentation, 
            string socketStringRepresentation )
        {
            Assert.AreEqual(
                socketStringRepresentation, 
                packetRepresentation.ToString());
        }
        
        [Test, TestCaseSource(nameof(MessagePackagesToSocketCommandMapping))]
        public void TestGenerateMessageFromString(
            BlobAnalyzerMessagePacket packetRepresentation, 
            string socketStringRepresentation )
        {
            Assert.AreEqual(
                    packetRepresentation,
                    BlobAnalyzerMessagePacket.FromMessage(socketStringRepresentation));
        }

        [Test]
        public void TestResponseIsNackFromInvalidHeader()
        {
            Assert.AreEqual(
                PacketHeader.NACK,
                BlobAnalyzerMessagePacket.FromMessage("hello").Command);
        }
        
        [Test]
        public void TestResponseIsNackFromInvalidContent()
        {
            Assert.AreEqual(
                PacketHeader.NACK,
                BlobAnalyzerMessagePacket.FromMessage("START|...").Command);
        }

        public IEnumerable<PacketHeader> PacketHeaders() =>
            Enum.GetNames(typeof(PacketHeader))
                .Select(X => 
                    (PacketHeader)Enum.Parse(typeof(PacketHeader), X))
                .Where(X => X!= PacketHeader.ACK)
                .Where(X => X!= PacketHeader.UNKNOWN)
                .ToArray();
        

        [Test, TestCaseSource(nameof(PacketHeaders))]
        public void CreateExampleTransitions(
            PacketHeader header)
        {
            BlobAnalyzerMessagePacket packet = new BlobAnalyzerMessagePacket()
            {
                Command = header,
                Comment = "Comment",
                Operator = "Operator",
                ErrorMessage = "Error message",
                RecipeName = "Recipe name",
                SampleId = "Sample Id",
                AckPackageCommand = PacketHeader.UNKNOWN
            };
            
            // Print command (for documentation)
            Console.WriteLine(packet.ToString());
            
            // Print acknowledge (for documentation)
            packet.Command = PacketHeader.ACK;
            packet.AckPackageCommand = header;
            Console.WriteLine(packet.ToString());
        }

       
    }
}
