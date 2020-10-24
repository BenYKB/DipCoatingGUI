using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace DipCoatingGUI.Models
{
    class ControllerViewModel : INotifyPropertyChanged
    {
        private double _numCycles = 5;
        private double _secondsDown = 1;
        private double _minutesUp = 10;
        private double _armSetpoint = 100;
        private string _connectionStatus;
        private string _connectButtonText;
        private string _startStopButtonText;
        private string _statusMsg;
        private string _connectionColor;

        public String ConnectionColor
        {
            get => _connectionColor;
            set
            {
                if (_connectionColor != value)
                {
                    _connectionColor = value;
                    OnPropertyChanged();
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMsg;
            set
            {
                if (_statusMsg != value)
                {
                    _statusMsg = value;
                    OnPropertyChanged();
                }
            }
        }
        public string StartStopButtonText
        {
            get => _startStopButtonText;
            set
            {
                if (_startStopButtonText != value)
                {
                    _startStopButtonText = value;
                    OnPropertyChanged();
                }
            }
        }
        public string ConnectButtonText
        {
            get => _connectButtonText;
            set
            {
                if (_connectButtonText != value)
                {
                    _connectButtonText = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set
            {
                if (_connectionStatus != value)
                {
                    _connectionStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public double ArmSetpoint
        {
            get => _armSetpoint;
            set
            {
                if (_armSetpoint != value)
                {
                    _armSetpoint = value;
                    OnPropertyChanged();
                }
            }
        }

        public double NumCycles
        {
            get => _numCycles;
            set
            {
                if (Math.Abs(value - _numCycles) > 0.25) {
                    _numCycles = value;
                    OnPropertyChanged();
                }
            }
        }

        public double SecondsDown
        {
            get => _secondsDown;
            set
            {
                if (Math.Abs(value - _secondsDown) > 0.25)
                {
                    _secondsDown = value;
                    OnPropertyChanged();
                }
            }
        }
        public double MinutesUp
        {
            get => _minutesUp;
            set
            {
                if (Math.Abs(value - _minutesUp) > 0.25)
                {
                    _minutesUp = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
