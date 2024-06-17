using Pavlo.AWGSupervisionBoard;
using Pavlo.AWGSupervisionBoard.Viewmodel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows;
using System.Windows.Input;

namespace Pavlo.AWGSupervisionBoard.Viewmodel
{
    public class CommandStartSignalGeneration : ICommand
    {
        /// <summary>
        /// VM of the window
        /// </summary>
        protected Viewmodel vm = null;

        public CommandStartSignalGeneration(Viewmodel vm)
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
            //https://stackoverflow.com/questions/127477/detecting-wpf-validation-errors
            return (vm.CanWeRaiseRequestToAWG && vm.ThePlotModel.Series.Count>0 && IsValid(parameter as DependencyObject));
        }

        private bool IsValid(DependencyObject obj)
        {
            // The dependency object is valid if it has no errors and all
            // of its children (that are dependency objects) are error-free.
            bool res = !Validation.GetHasError(obj) &&
            LogicalTreeHelper.GetChildren(obj)
            .OfType<DependencyObject>()
            .All(IsValid);

            return res;
        }

        public void Execute(object? parameter)
        {
            vm?.StartSignalGenerationAsync();
        }
    }
}
