using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DipCoatingGUI.Models;
using Phidget22;
using Phidget22.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text;

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
        public NumericUpDown NumberOfCycles;
        private const double TICK_MILLESECONDS = 250;
        private const string START_STOP_BUTTON_STARTED = "Stop";
        private const string START_STOP_BUTTON_STOPPED = "Start";
        private const string CONNECT_BUTTON_CONNECTED_MSG = "-Already Connected-";
        private const string CONNECT_BUTTON_DISCONNECTED_MSG = "Connect";
        private const string CONNECT_LABEL_CONNECTED_TEXT_MSG = "Servo Controller: Connected";
        private const string CONNECT_LABEL_DISCONNECTED_TEXT_MSG = "Servo Controller: Disconnected";
        private const string CONNECTED_COLOR = "LightGreen";
        private const string DISCONNECTED_COLOR = "Red";
        private const double ARM_MAX = 90;
        private const double ARM_RETRACT = 89;
        private const double ARM_UP = 72;
        private const double ARM_DOWN = 35;
        private const double ARM_MIN = 32;
        private const double ARM_MID = (ARM_UP + ARM_DOWN + 1) / 2;
        private const double POSITION_AT_MAX_PULSEWIDTH = 180;
        private const double POSITION_AT_MIN_PULSEWIDTH = 0;
        private const double SMALL_ANGLE = 0.5;
        private const int CONNECTION_TIMEOUT = 2000;
        private RCServo servo;
        private double TargetPosition;
        private int ticks;
        private DispatcherTimer timer;

        private const double STARTING_WAIT_TIME = 2;
        private bool isAutomatedCommand = false;
        private int [] automationParameters;

        private enum States{
            DOWN,
            TRANSIT,
            UP,
            START,
            DONE
        }

        private States currentState;
        private States previousState;
        private int currentCycleRemaining;
        private double secondsUntilNextTransition;


        public MainWindow()
        {
            InitializeComponent();
            TargetPosition = ARM_MID;
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
                context.ArmSetpoint = ARM_MID;
            });
        }

        public void onRetractButtonClick(object sender, RoutedEventArgs e)
        {
            if (!isAutomatedCommand)
            {
                ServoCommand(ARM_RETRACT);
            }
        }

        public void onUpPositionButtonClick(object sender, RoutedEventArgs e)
        {
            if (!isAutomatedCommand)
            {
                ServoCommand(ARM_UP);
            }
        }

        public void onDownPositionClick(object sender, RoutedEventArgs e)
        {
            if (!isAutomatedCommand)
            {
                ServoCommand(ARM_DOWN);
            }
        }

        public void onConnectClick(object sender, RoutedEventArgs e)
        {
            if (servo == null || servo.Attached)
            {
                UpdateConnectionStatus();
                return;
            }

            try
            {
                servo.Open(CONNECTION_TIMEOUT);
            }
            catch (PhidgetException ex)
            {
                Debug.WriteLine($"Failed to connect phidget with msg {ex}");
            }

            UpdateConnectionStatus();
        }

        public void onDownButtonClick(object sender, RoutedEventArgs e)
        {
            if (!isAutomatedCommand)
            {
                ServoCommand(TargetPosition - 1);
            }
        }

        public void OnMainWindowClosing(object sender, CancelEventArgs e)
        {
            Debug.WriteLine("Closing");

            if (servo != null)
            {
                try
                {
                    if (servo.Attached)
                    {
                        servo.Engaged = false;
                        servo.Close();
                    }
                } 
                catch(PhidgetException exp)
                {
                    Debug.WriteLine($"Got Phidget Exception {exp}");
                }
            }
        }

        public void onStartStop(object sender, RoutedEventArgs e)
        {
            isAutomatedCommand = !isAutomatedCommand;

            if (isAutomatedCommand)
            {
                if (servo.Attached)
                {
                    var context = (ControllerViewModel)DataContext;
                    automationParameters = new int[] { (int)context.NumCycles , (int)context.SecondsDown , (int)context.MinutesUp };
                    currentCycleRemaining = automationParameters[0];
                    secondsUntilNextTransition = STARTING_WAIT_TIME;
                    ServoCommand(ARM_UP);
                    previousState = States.START;
                    currentState = States.TRANSIT;
                }
                else
                {
                    isAutomatedCommand = false;
                    DelegateUIStatusUpdate("Cannot start: Servo Disconnected");
                }
            }
            else
            {
                servo.Engaged = false;
                DelegateUIStatusUpdate("Operation Cancelled: Servo Stopped");
            }
            updateStartStopButton(isAutomatedCommand);
        }

        private void onTick(object sender, EventArgs e)
        {
            ticks += 1;
            UpdateConnectionStatus();

            if (isAutomatedCommand)
            {
                if (IsAtSetpoint)
                {
                    if (currentState == States.DOWN)
                    {
                        secondsUntilNextTransition -= TICK_MILLESECONDS /1000.0;
                        if (secondsUntilNextTransition <= 0)
                        {
                            ServoCommand(ARM_UP);
                            previousState = States.DOWN;
                            currentState = States.TRANSIT;
                        }
                    }
                    else if (currentState == States.TRANSIT)
                    {
                        if (TargetPosition == ARM_UP)
                        {
                            if (previousState == States.START)
                            {
                                ServoCommand(ARM_DOWN);
                                previousState = States.UP;
                                currentState = States.TRANSIT;
                            }
                            else
                            {
                                secondsUntilNextTransition = automationParameters[2] * 60;
                                previousState = States.TRANSIT;
                                currentState = States.UP;
                            }        
                        } 
                        else if (TargetPosition == ARM_DOWN)
                        {
                            secondsUntilNextTransition = automationParameters[1];
                            previousState = States.TRANSIT;
                            currentState = States.DOWN;
                        } 
                        else
                        {
                            throw new Exception("Target Position Invalid for Automated Command");
                        }
                    }
                    else if (currentState == States.UP)
                    {
                        secondsUntilNextTransition -= TICK_MILLESECONDS / 1000.0;
                        if (secondsUntilNextTransition <= 0)
                        {
                            currentCycleRemaining -= 1;
                            if (currentCycleRemaining <= 0)
                            {
                                previousState = currentState;
                                currentState = States.DONE; 
                                isAutomatedCommand = false;
                                updateStartStopButton(isAutomatedCommand);
                            }
                            else
                            {
                                ServoCommand(ARM_DOWN);
                                previousState = States.UP;
                                currentState = States.TRANSIT;
                            }
                        }
                    }
                    else
                    {
                        throw new Exception($"Invalid State {currentState}");
                    }
                }

             UpdateStatusUI(currentState, currentCycleRemaining, secondsUntilNextTransition);
            }
        }

        private void UpdateStatusUI(States state, int cyclesRemaining, double secondsLeft)
        {
            StringBuilder msg = new StringBuilder();
            msg.Append($"{cyclesRemaining} of { automationParameters[0]} cycles remain\n");
                
                
            if (state == States.START)
            {
                msg.Append("Starting Operation");
            }
            else if (state == States.DOWN)
            {
                msg.Append($"{secondsLeft} s of ({automationParameters[1]} s) remain in down position");
            }
            else if (state == States.TRANSIT)
            {
                msg.Append("In transit");
            }
            else if (state == States.UP)
            {
                msg.Append($"{(int)secondsLeft} s of {automationParameters[2]} minutes remain in up position");
            }
            else if (state == States.DONE)
            {
                msg.Append("Finished Operation");
            }
            else
            {
                throw new Exception($"Invalid state {state}");
            }

            DelegateUIStatusUpdate(msg.ToString());
        }

        private void DelegateUIStatusUpdate(string msg)
        {
            var uiThread = Dispatcher.UIThread;
            uiThread.InvokeAsync(delegate
            {
                ((ControllerViewModel)DataContext).StatusMessage = msg;
            });
        }

        private void updateStartStopButton(bool isStarted)
        {
            var uiThread = Dispatcher.UIThread;
            uiThread.InvokeAsync(delegate {
                ((ControllerViewModel)this.DataContext).StartStopButtonText = isStarted ? START_STOP_BUTTON_STARTED : START_STOP_BUTTON_STOPPED;
            });
        }



        public void onUpButtonClick(object sender, RoutedEventArgs e)
        {
            if (!isAutomatedCommand)
            {
                ServoCommand(TargetPosition + 1);
            }
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

            UpdateConnectionStatus();
        }

        public void UpdateConnectionStatus()
        {
            if (servo != null)
            {
                var isConnected = servo.Attached;

                var uiThread = Dispatcher.UIThread;
                uiThread.InvokeAsync(delegate {
                    var context = (ControllerViewModel)this.DataContext;
                    context.ConnectButtonText = isConnected ? CONNECT_BUTTON_CONNECTED_MSG : CONNECT_BUTTON_DISCONNECTED_MSG;
                    context.ConnectionStatus = isConnected ? CONNECT_LABEL_CONNECTED_TEXT_MSG : CONNECT_LABEL_DISCONNECTED_TEXT_MSG;
                    context.ConnectionColor = isConnected ? CONNECTED_COLOR : DISCONNECTED_COLOR;
                });
            }
            else
            {
                Debug.WriteLine("No servo reference set");
            }
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
            timer = new DispatcherTimer(TimeSpan.FromMilliseconds(TICK_MILLESECONDS), DispatcherPriority.Normal, onTick);
            timer.Start();
            Debug.WriteLine("Started Timer");
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

            UpdateConnectionStatus();
        }

        private void Servo_Detach(object sender, DetachEventArgs e)
        {
            UpdateConnectionStatus();
        }

        private void Servo_TargetPositionReached(object sender, RCServoTargetPositionReachedEventArgs e)
        {
            if (Math.Abs(e.Position - TargetPosition) < SMALL_ANGLE)
            {
                IsAtSetpoint = true;
            }
        }

        private void UpdateArmSetpointUI(double setpoint)
        {
            var uiThread = Dispatcher.UIThread;

            uiThread.InvokeAsync(delegate
            {
                ((ControllerViewModel)DataContext).ArmSetpoint = setpoint;
            });
        }

        private void ServoCommand(double position)
        {
            if (ARM_MIN < position && position < ARM_MAX)
            {
                if (servo.Attached)
                {
                    try
                    {
                        servo.Engaged = true;
                        AsyncCallback asyncCallBackDelegate = new AsyncCallback(AsyncCallBackForSetTargetPosition);
                        CallBackInfo callBackInfo = new CallBackInfo(servo, position);
                        TargetPosition = position;
                        IsAtSetpoint = false;
                        UpdateArmSetpointUI(TargetPosition);
                        servo.BeginSetTargetPosition(position, asyncCallBackDelegate, callBackInfo);
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
                Debug.WriteLine($"Postion {position} out of bounds {ARM_MIN}-{ARM_MAX}");
            }
        }
    }
}