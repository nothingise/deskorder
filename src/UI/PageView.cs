using System;
using System.Collections.Generic;
using System.Windows.Controls;
using DeskOrder.App;

namespace DeskOrder.UI
{
    public abstract class PageView : UserControl, IDisposable
    {
        protected readonly DeskOrderApp App;
        protected readonly MainWindow Host;
        protected readonly List<Action> Unsubscribe = new List<Action>();

        protected PageView(DeskOrderApp app, MainWindow host)
        {
            App = app;
            Host = host;
        }

        public abstract string Title { get; }
        public virtual string Subtitle { get { return ""; } }
        public virtual void Refresh() { }

        public virtual void Dispose()
        {
            foreach (Action a in Unsubscribe)
            {
                try { a(); } catch { }
            }
            Unsubscribe.Clear();
        }
    }
}
