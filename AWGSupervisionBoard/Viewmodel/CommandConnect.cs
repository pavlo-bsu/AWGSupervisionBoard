using Pavlo.AWGSupervisionBoard;
using Pavlo.AWGSupervisionBoard.Viewmodel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Pavlo.AWGSupervisionBoard.Viewmodel
{
    public class CommandConnect : ICommand
    {
        /// <summary>
        /// VM of the window
        /// </summary>
        protected Viewmodel vm = null;

        public CommandConnect(Viewmodel vm)
        {
            this.vm = vm;
            this.vm.PropertyChanged += vm_PropertyChanged;
        }

        private void vm_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (CanExecuteChanged != null)
            {
                //rise check at any property change 
                CanExecuteChanged(this, new EventArgs());
            }
        }

        public event EventHandler CanExecuteChanged;

        public virtual bool CanExecute(object parameter)
        {
            if (vm.IsAWGConnected==false)
            {
                return true;
            }
            else if (!vm.IsResposeAwaiting && !vm.IsSignalGenerating)
                return true;

            //any other conditions
            return false;
        }

        public void Execute(object? parameter)
        {
            vm?.ConnectAndResetAsync(true);
        }
    }
}
