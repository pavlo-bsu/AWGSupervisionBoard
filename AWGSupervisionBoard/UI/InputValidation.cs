using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Controls;

using System.Windows;

namespace Pavlo.AWGSupervisionBoard.UI.ValidationInput
{
    /// <summary>
    /// Class implements validation rules.
    /// </summary>
    public class InputDataValidation : ValidationRule
    {
        public override ValidationResult Validate(object value, System.Globalization.CultureInfo cultureInfo)
        {
            if (value != null)
            {
                string val = (value as string).Trim();

                if (this.InternalPropertyName.ToLower() == "boundedintvalue")
                {//for int values
                    int iValue = 0;
                    try
                    {
                        iValue = int.Parse(val);
                    }
                    catch
                    {
                        return new ValidationResult(false, "Input an integer number");
                    }

                    if (iValue < this.intSegmentBounds.LowerIntBound || iValue > this.intSegmentBounds.UpperIntBound)
                        return new ValidationResult(false, string.Format("The value has to be from {0} to {1}", this.intSegmentBounds.LowerIntBound, this.intSegmentBounds.UpperIntBound));

                    return ValidationResult.ValidResult;

                }
                else
                {//i.e. double values
                    double number = -1;
                    try
                    {
                        number = Double.Parse(val);
                    }
                    catch
                    {
                        return new ValidationResult(false, "Input a number");
                    }

                    switch (InternalPropertyName.ToLower())
                    {
                        case "boundeddoublevalue"://i.e. double value bounded from two ends
                            if (number < this.segmentBounds.LowerBound || number > this.segmentBounds.UpperBound)
                                return new ValidationResult(false, string.Format("The value has to be from {0} to {1}", this.segmentBounds.LowerBound, this.segmentBounds.UpperBound));
                            break;
                        default:
                            throw new Exception("Unknown InternalPropertyName");
                    }

                    return ValidationResult.ValidResult;
                }
            }
            else
            {
                return ValidationResult.ValidResult;
            }
        }

        string _internalPropertyName;
        public string InternalPropertyName
        {
            get
            {
                return _internalPropertyName;
            }
            set
            {
                _internalPropertyName = value;
            }
        }

        //BOUNDS FOR SOME PROPERTIES
        /// <summary>
        /// segment bounds for double variable
        /// </summary>
        public BoundedDouble_DepProp segmentBounds
        {
            get;
            set;
        }

        /// <summary>
        /// segment bounds for int variable
        /// </summary>
        public BoundedInt_DepProp intSegmentBounds
        {
            get;
            set;
        }
    }
    
    //https://stackoverflow.com/questions/3862385/wpf-validationrule-with-dependency-property
    /// <summary>
    ///  implement dep. prop. "lower/upper bound" for double type variable
    /// </summary>
    public class BoundedDouble_DepProp : DependencyObject
    {
        public double LowerBound
        {
            get { return (double)GetValue(LowerBoundProperty); }
            set { SetValue(LowerBoundProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LowerBound. This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LowerBoundProperty =
            DependencyProperty.Register("LowerBound", typeof(double), typeof(BoundedDouble_DepProp), new UIPropertyMetadata(-10.0));


        public double UpperBound
        {
            get { return (double)GetValue(UpperBoundProperty); }
            set { SetValue(UpperBoundProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LowerBound.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty UpperBoundProperty =
            DependencyProperty.Register("UpperBound", typeof(double), typeof(BoundedDouble_DepProp), new UIPropertyMetadata(-10.0));
    }

    /// <summary>
    ///  implement dep. prop. "lower bound" and "upper bound" for int type variable
    /// </summary>
    public class BoundedInt_DepProp : DependencyObject
    {
        public int LowerIntBound
        {
            get { return (int)GetValue(LowerIntBoundProperty); }
            set { SetValue(LowerIntBoundProperty, value); }
        }

        // Using a DependencyProperty as the backing store for LowerIntBound.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty LowerIntBoundProperty =
            DependencyProperty.Register("LowerIntBound", typeof(int), typeof(BoundedInt_DepProp), new UIPropertyMetadata(-1));


        public int UpperIntBound
        {
            get { return (int)GetValue(UpperIntBoundProperty); }
            set { SetValue(UpperIntBoundProperty, value); }
        }

        // Using a DependencyProperty as the backing store for UpperIntBound.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty UpperIntBoundProperty =
            DependencyProperty.Register("UpperIntBound", typeof(int), typeof(BoundedInt_DepProp), new UIPropertyMetadata(-1));
    }

    
    //https://social.technet.microsoft.com/wiki/contents/articles/31422.wpf-passing-a-data-bound-value-to-a-validation-rule.aspx
    public class BindingProxy : System.Windows.Freezable
    {
        protected override Freezable CreateInstanceCore()
        {
            return new BindingProxy();
        }

        public object Data
        {
            get { return (object)GetValue(DataProperty); }
            set { SetValue(DataProperty, value); }
        }

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register("Data", typeof(object), typeof(BindingProxy), new PropertyMetadata(null));
    }
}
