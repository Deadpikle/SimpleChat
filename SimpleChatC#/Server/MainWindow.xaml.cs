using SharedCode;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Server
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            if (DataContext is ChangeNotifier)
            {
                ((ChangeNotifier)DataContext).PropertyChanged += MainWindow_PropertyChanged;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (DataContext is MainWindowViewModel)
            {
                (DataContext as MainWindowViewModel).Terminate();
            }
        }

        private void MainWindow_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ChatText")
            {
                Dispatcher.Invoke(() =>
                {
                    // if scroll viewer at bottom, keep auto scrolling. otherwise, stay at the top.
                    if (ShouldAutoScroll.IsChecked.Value || Math.Abs(MessagesScrollViewer.VerticalOffset - MessagesScrollViewer.ScrollableHeight) < 0.1)
                    {
                        MessagesScrollViewer.ScrollToBottom();
                    }
                });
            }
        }
    }
}
