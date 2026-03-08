using System;
using System.Windows;
using System.Windows.Threading;

namespace StreamTweak
{
    public partial class StreamingAdjustmentAlert : Window
    {
        private DispatcherTimer? closeTimer;

        public StreamingAdjustmentAlert()
        {
            InitializeComponent();
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            // Auto-close after 8 seconds (giving time to read and for adjustment to complete)
            closeTimer = new DispatcherTimer();
            closeTimer.Interval = TimeSpan.FromSeconds(8);
            closeTimer.Tick += (s, args) =>
            {
                closeTimer?.Stop();
                this.Close();
            };
            closeTimer.Start();
        }

        protected override void OnClosed(EventArgs e)
        {
            closeTimer?.Stop();
            closeTimer = null;
            base.OnClosed(e);
        }
    }
}
