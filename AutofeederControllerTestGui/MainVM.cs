using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VM.BlobAnalyzer.SocketController;
using VM.Lab.Interfaces.Autofeeder;

namespace AutofeederControllerTestGui
{
    public class MainVM : IAutofeederControlListener
    {
        private AutofeederController controller;

        private string _lastId;

        public ObservableCollection<string> Messages { get; set; } = new ObservableCollection<string>();

        public MainVM()
        {
            controller = new AutofeederController(this);
        }
        
        public void ConveyourStopped()
        {
            // Stopped
            controller.StateChanged(AutofeederState.Stopping, AutofeederState.Stopped, _lastId, null);
        }


        public async Task Start(string id, string initials, string comments)
        {
            await Task.Run(() =>
            {
                _lastId = id;
                Messages.Add($"Start({id}, {initials}, {comments}");
            });
        }

        public async Task Stop(WaitCondition waitCondition, bool doFlush)
        {
            await Task.Run(() =>
            {
                Messages.Add("Stop");
            });
        }

        public async Task Flush()
        {
            await Task.Run(() =>
            {
                Messages.Add("Flush");
            });
        }

        public async Task Finish()
        {
            await Task.Run(() =>
            {
                Messages.Add("Finish");
            });
        }
    }
}
