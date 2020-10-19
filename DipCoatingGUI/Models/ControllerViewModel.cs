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
        


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
