using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DipCoatingGUI.Models;
using Phidget22;
using Phidget22.Events;
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace DipCoatingGUI
{
    /// <summary>
    /// Class created to pass information to the callback
    /// </summary>
    public class CallBackInfo
    {
        public CallBackInfo(RCServo servo, double intendedTargetPosition)
        {
            Owner = servo;
            IntendedTargetPosition = intendedTargetPosition;
        }

        public double IntendedTargetPosition { get; private set; }
        public RCServo Owner { get; private set; }
    }

    public class MainWindow : Window
    {
        public bool IsAtSetpoint;
        public bool IsPhidgetConnected;
        public NumericUpDown NumberOfCycles;
        private const string START_STOP_BUTTON_STARTED = "Stop";
        private const string START_STOP_BUTTON_STOPPED = "Start";
        private const string CONNECT_BUTTON_CONNECTED_MSG = "-Already Connected-";
        private const string CONNECT_BUTTON_DISCONNECTED_MSG = "Connect";
        private const string CONNECT_LABEL_CONNECTED_TEXT_MSG = "Servo Controller Connected";
        private const string CONNECT_LABEL_DISCONNECTED_TEXT_MSG = "Servo Controller Disconnected";
        private const double DEFAULT_SETPOINT = 50;
        private const double ARM_MAX = 89;
        private const double ARM_RETRACT = 89;
        private const double ARM_UP = 72;
        private const double ARM_DOWN = 35;
        private const double ARM_MIN = 32;
        private const double POSITION_AT_MAX_PULSEWIDTH = 180;
        private const double POSITION_AT_MIN_PULSEWIDTH = 0;
        private const double SMALL_ANGLE = 0.5;
        private const int CONNECTION_TIMEOUT = 2000;
        private RCServo servo;
        private Stopwatch stopWatch;
        private double TargetPosition;
        private int ticks;
        private DispatcherTimer timer;

        public MainWindow()
        {
            InitializeComponent();
            TargetPosition = DEFAULT_SETPOINT;
            IsPhidgetConnected = false;
            DataContext = new ControllerViewModel();
            SetDataContextDefaults();
            SetupPhidget();

#if DEBUG
            this.AttachDevTools();
#endif
        }

        private void SetDataContextDefaults()
        {
            var uiThread = Dispatcher.UIThread;
            uiThread.InvokeAsync(delegate
            {
                var context = (ControllerViewModel)DataContext;
                context.StartStopButtonText = START_STOP_BUTTON_STOPPED;
                context.ArmSetpoint = DEFAULT_SETPOINT;
            });

            UpdateConnectionStatus(false);
        }

        public void onConnectClick(object sender, RoutedEventArgs e)
        {
            try
            {
                servo.Open(CONNECTION_TIMEOUT);
            }
            catch (PhidgetException ex)
            {
                Debug.WriteLine($"Failed to connect phidget with msg {ex}");
            }

            UpdateConnectionStatus(servo.Attached);
        }

        public void onDownButtonClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            ServoCommand(TargetPosition - 1);
        }

        public void OnMainWindowClosing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("Closing");

            if (servo != null)
            {
                try
                {
                    servo.Engaged = false;
                    servo.Close();
                } 
                catch(PhidgetException exp)
                {
                    Debug.WriteLine($"Got Phidget Exception {exp}");
                }
            }
        }

        public void onStartStop(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;
        }

        public void onUpButtonClick(object sender, RoutedEventArgs e)
        {
            var button = (Button)sender;

            ServoCommand(TargetPosition + 1);
        }

        public void SetupPhidget()
        {
            servo = new RCServo();
            servo.Attach += Servo_Attach;
            servo.Detach += Servo_Detach;
            servo.TargetPositionReached += Servo_TargetPositionReached;
            servo.DeviceSerialNumber = 306371;
            servo.Channel = 0;
            servo.IsHubPortDevice = false;
            try
            {
                servo.Open(CONNECTION_TIMEOUT);
            }
            catch (PhidgetException e)
            {
                Debug.WriteLine($"Failed to connect phidget with msg {e}");
            }

            UpdateConnectionStatus(servo.Attached);
        }

        public void UpdateConnectionStatus(bool isConnected)
        {
            IsPhidgetConnected = isConnected;

            var uiThread = Dispatcher.UIThread;
            uiThread.InvokeAsync(delegate
            {
                ((ControllerViewModel)this.DataContext).ConnectButtonText = isConnected ? CONNECT_BUTTON_CONNECTED_MSG : CONNECT_BUTTON_DISCONNECTED_MSG;
                ((ControllerViewModel)this.DataContext).ConnectionStatus = isConnected ? CONNECT_LABEL_CONNECTED_TEXT_MSG : CONNECT_LABEL_DISCONNECTED_TEXT_MSG;
            });
        }

        private void AsyncCallBackForSetTargetPosition(IAsyncResult result)
        {
            try
            {
                CallBackInfo callBackInfo = (CallBackInfo)result.AsyncState;
                callBackInfo.Owner.EndSetTargetPosition(result);
            }
            catch (Exception exp)
            {
                Debug.WriteLine($"Servo command callback error {exp}");
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);

            NumberOfCycles = new NumericUpDown();
            ticks = 0;
            timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal, onTick);
            timer.Start();
            Debug.WriteLine("Started Timer");
        }

        private void onTick(object sender, EventArgs e)
        {
            ticks += 1;
        }

        public void Servo_Attach(object sender, AttachEventArgs e)
        {
            RCServo servo = (RCServo)sender;

            //servo.Voltage = RCServoVoltage.Volts_5_0;
            //servo.MinPulseWidth = MIN_PULSEWIDTH;
            //servo.MaxPulseWidth = MAX_PULSEWIDTH;
            servo.MinPosition = POSITION_AT_MIN_PULSEWIDTH;
            servo.MaxPosition = POSITION_AT_MAX_PULSEWIDTH;
            servo.VelocityLimit = servo.MaxVelocityLimit;
            servo.Acceleration = servo.MaxAcceleration;
            servo.TargetPosition = TargetPosition;
            servo.Engaged = true;

            UpdateConnectionStatus(true);
        }

        private void Servo_Detach(object sender, DetachEventArgs e)
        {
            UpdateConnectionStatus(false);
        }

        private void Servo_TargetPositionReached(object sender, RCServoTargetPositionReachedEventArgs e)
        {
            if (Math.Abs(e.Position - TargetPosition) < SMALL_ANGLE)
            {
                IsAtSetpoint = true;
            }
        }
        private void ServoCommand(double position)
        {
            if (ARM_MIN < position && position < ARM_MAX)
            {
                if (IsPhidgetConnected)
                {
                    try
                    {
                        AsyncCallback asyncCallBackDelegate = new AsyncCallback(AsyncCallBackForSetTargetPosition);
                        CallBackInfo callBackInfo = new CallBackInfo(servo, position);
                        servo.BeginSetTargetPosition(position, asyncCallBackDelegate, callBackInfo);
                        TargetPosition = position;
                        ((ControllerViewModel)this.DataContext).ArmSetpoint = TargetPosition;
                    }
                    catch (Exception e)
                    {
                        Debug.WriteLine($"Phidget command failed with msg {e}");
                    }
                }
                else
                {
                    Debug.WriteLine("Servo not connected");
                }
            }
            else
            {
                Debug.WriteLine($"Postion {position} out of bounds {MIN_ANGLE}-{MAX_ANGLE}");
            }
        }
    }
}