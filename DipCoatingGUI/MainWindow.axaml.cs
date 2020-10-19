using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DipCoatingGUI.Models;
using Phidget22;
using Phidget22.Events;
using System;
using System.Diagnostics;
using System.Xml.Serialization;

namespace DipCoatingGUI
{
    public class MainWindow : Window
    {
        const int MIN_PULSEWIDTH = 800000;
        const int MAX_PULSEWIDTH = 2000000;
        const double POSITION_AT_MIN_PULSEWIDTH = 0;
        const double POSITION_AT_MAX_PULSEWIDTH = 180;
        private RCServo servo;
        private Stopwatch stopWatch;
        private DispatcherTimer timer;
        private int ticks;
        public NumericUpDown NumberOfCycles;

        public MainWindow()
        {
            InitializeComponent();
            this.DataContext = new ControllerViewModel();
#if DEBUG
            this.AttachDevTools();
#endif
        }

        public void onUpButtonClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.Content = $"{ticks}";
        }

        public void onDownButtonClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.Content = $"{ticks}";
        }

       public void onConnectClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
            button.Content = "Connected";
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            NumberOfCycles = new NumericUpDown();
            ticks = 0;
            timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, onTick);
            timer.Start();
            
        }

        private void onTick(object sender, EventArgs e)
        {
            ticks += 1;
        }
    }
}
