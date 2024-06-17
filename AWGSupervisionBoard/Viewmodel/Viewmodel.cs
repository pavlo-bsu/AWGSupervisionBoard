using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media.Animation;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using Microsoft.Win32;
using Pavlo.MyDAL;
using Pavlo.MyHelpers.MVVM;
using OxyPlot;
using OxyPlot.Axes;
using Accessibility;

namespace Pavlo.AWGSupervisionBoard.Viewmodel
{
    public class Viewmodel : INPCBaseDotNet4_5
    {
        #region commands
        private CommandBrowse _TheCommandBrowse;
        public CommandBrowse TheCommandBrowse
        {
            get => _TheCommandBrowse;
            set { _TheCommandBrowse = value; }
        }

        public CommandStartSignalGeneration TheCommandStartSignalGeneration
        { get; set; }

        public CommandStop TheCommandStop
        { get; set; }

        public CommandConnect TheCommandConnect
        { get; set; }
        #endregion
        public Viewmodel()
        {
            //commands
            TheCommandConnect = new CommandConnect(this);
            TheCommandBrowse = new CommandBrowse(this);
            TheCommandStartSignalGeneration = new CommandStartSignalGeneration(this);
            TheCommandStop = new CommandStop(this);

            //default value and boundaries for UI properties
            DeviceAddress = "TCPIP0::127.0.0.1::hislip0::INSTR";

            awgChannelID = 1;
            awgSegmentID = 1;

            PRF_lowerBound = 0.1;
            PRF_upperBound = 100;

            InitPlotModel();

            AWGSettingsSetToDefault().Wait();
        }

        private AWG8195A _TheAWG8195A = null;
        /// <summary>
        /// instance of the device
        /// </summary>
        public AWG8195A TheAWG8195A
        {
            get { return _TheAWG8195A; }
            set => _TheAWG8195A = value;
        }

        /// <summary>
        /// Do the work during window closing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            //Dispose connection to the device
            TheAWG8195A?.Dispose();
        }

        /// <summary>
        /// ID of the output channel
        /// </summary>
        private int awgChannelID=-1;

        /// <summary>
        /// ID of a segment for the waveform
        /// </summary>
        private int awgSegmentID=-1;

        string _DeviceAddress;
        public string DeviceAddress
        {
            get => _DeviceAddress;
            set
            {
                if (_DeviceAddress == value)
                    return;
                else
                    _DeviceAddress = value;

                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// connect to the instrument, reset it, set default settings
        /// </summary>
        /// <param name="handleException">true - to throw exception if an error occurred </param>
        /// <returns></returns>
        public async Task ConnectAndResetAsync(bool handleException)
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );

            try
            {
                AWG8195A awg = new AWG8195A(DeviceAddress);
                bool res = awg.ConnectToTheAWG();
                if (res)
                {//i.e. new connection is established
                    //Dispose previous connection
                    TheAWG8195A?.Dispose();

                    //set instrument
                    TheAWG8195A = awg;
                    await TheAWG8195A.StopSignalGenearationAsync();
                    await TheAWG8195A.ResetAsync();
                    await TheAWG8195A.SetMemoryToExtendedModeAsync(awgChannelID);
                    await TheAWG8195A.ClearWaveformMemoryAsync(awgChannelID);
                    await TheAWG8195A.SetOutputAsync(awgChannelID, true);
                    await TheAWG8195A.SetInternalTriggerAsync();
                    await TheAWG8195A.RunInTriggerModeAsync();

                    //set settings to default
                    await AWGSettingsSetToDefault();
                }
                else
                {
                    MessageBox.Show($"Connection to \"{DeviceAddress}\" has not been established", "Connection", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception e)
            {
                if (handleException)
                {
                    string msg = e.Message;
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    throw;
                }
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               IsResposeAwaiting = false;
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
            }
        }

        /// <summary>
        /// set settings to default values: waveform, amplitude, PRF
        /// </summary>
        /// <returns></returns>
        public async Task AWGSettingsSetToDefault()
        {
            await SetWaveformDataToDefaultAsync();

            VoltageAmplitude = VoltageAmplitude_lowerBound;

            PRF = 50;
        }

        

        #region availability of UI elements
        private bool _IsResposeAwaiting = false;
        /// <summary>
        /// Was request send and is response awaiting
        /// </summary>
        public bool IsResposeAwaiting
        {
            get => _IsResposeAwaiting;
            set
            {
                _IsResposeAwaiting = value;
                NotifyPropertyChanged_AvailabilityOfUIElements();
            }
        }

        /// <summary>
        /// indicates whether AWG was connected
        /// </summary>
        public bool IsAWGConnected
        {
            get
            {
                if (TheAWG8195A != null)
                {
                    return true;
                }
                else return false;
            }
        }

        /// <summary>
        /// Can we raise request to AWG for pressetings of voltage ampl., PRF, etc.
        /// </summary>
        public bool CanWeRaiseRequestToAWG
        {
            get
            {
                if (TheAWG8195A == null || !TheAWG8195A.IsConnectionEstableshed)
                {//i.e. not connected to the instrument
                    return false;
                }
                else if(!IsResposeAwaiting && !IsSignalGenerating)
                {
                    return true;
                }

                //for all other states
                return false;
            }
            
        }

        private bool _IsSignalGenerating;
        /// <summary>
        /// indicates whether is signal generated
        /// </summary>
        public bool IsSignalGenerating
        {
            get=> _IsSignalGenerating;
            set
            {
                _IsSignalGenerating = value;
                NotifyPropertyChanged_AvailabilityOfUIElements();
            }
        }

        private void NotifyPropertyChanged_AvailabilityOfUIElements()
        {
            NotifyPropertyChanged(nameof(IsAWGConnected));
            NotifyPropertyChanged(nameof(IsResposeAwaiting));
            NotifyPropertyChanged(nameof(IsSignalGenerating));
            NotifyPropertyChanged(nameof(CanWeRaiseRequestToAWG));
        }
        #endregion

        #region Browse file and show the data
        public PlotModel ThePlotModel { get; private set; }
        /// <summary>
        /// Init PlotModel: set title, axes
        /// </summary>
        private void InitPlotModel()
        {
            ThePlotModel = new PlotModel();
            
            ThePlotModel.Title = $"Signal";
            ThePlotModel.Axes.Clear();

            var axisT = new LinearAxis()
            {
                Position = AxisPosition.Bottom,
                IsZoomEnabled = true,
                Title = "Time [sec]",
                MajorGridlineStyle = LineStyle.Dot
            };

            var axisV = new LinearAxis()
            {
                Position = AxisPosition.Left,
                IsZoomEnabled = true,
                MajorGridlineStyle = LineStyle.Dot,
                Title = "Voltage [V]"
            };

            ThePlotModel.Axes.Add(axisT);
            ThePlotModel.Axes.Add(axisV);
        }

        /// <summary>
        /// voltages of waveform data
        /// </summary>
        private double[] voltageData;
        /// <summary>
        /// times of waveform data
        /// </summary>
        private double[] timeData;


        /// <summary>
        /// Action for selecting data file: open file, check data, draw signal, send to the Instrument
        /// </summary>
        /// <returns></returns>
        public async Task BrowseDataFileAsync()
        {
            //start file dialog
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Data file (*.csv;*.txt)|*.csv;*.txt";
            if (dlg.ShowDialog() == true)
            {//i.e. file selected
                try
                {
                    //get data from file
                    GetDataFromFile(dlg.FileName, out timeData, out voltageData);
                    //check the data
                    TheAWG8195A.CheckTheData(timeData, voltageData);
                    //MessageBox.Show($"Data has been successfully read from file {dlg.FileName}", "Data file", MessageBoxButton.OK, MessageBoxImage.Information);
                    //draw signal
                    DrawWaveformInTheOXYPlot();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Unexpected file type\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

                    await SetWaveformDataToDefaultAsync();
                    //stop the method in case of error
                    return;
                }
                //Add waveform to the Instrument
                await AddWaveformToAWGAsync();
            }
        }

        /// <summary>
        /// Add waveform to the Instrument
        /// </summary>
        /// <returns></returns>
        private async Task AddWaveformToAWGAsync()
        {
            await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );

            try
            {
                var byteData = TheAWG8195A.ConvertWaveformToInternalByteRepresentation(timeData, voltageData);
                await TheAWG8195A.ClearWaveformMemoryAsync(awgChannelID);
                await TheAWG8195A.AddWaveformToSegmentAsync(byteData, awgChannelID, awgSegmentID);
            }
            catch (Exception e)
            {
                string msg = e.Message;
                MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Application.Current.Dispatcher.BeginInvoke(
                           new Action(() =>
                           {
                               IsResposeAwaiting = false;
                           }),
                           DispatcherPriority.ContextIdle,
                           null
                           );
            }
        }
        /// <summary>
        /// Set Waveform data to default: data to null, remove series from the plot and the Instrument 
        /// </summary>
        /// <returns></returns>
        private async Task SetWaveformDataToDefaultAsync()
        {
            timeData = null;
            voltageData = null;
            ThePlotModel?.Series.Clear();

            try
            {
                if (TheAWG8195A != null && TheAWG8195A.IsConnectionEstableshed)
                {
                    await TheAWG8195A.ClearWaveformMemoryAsync(awgChannelID);
                }
            }
            catch
            { }
        }

        /// <summary>
        /// Get data from file, preliminary check file type, perform time shift to start data from 0 s.
        /// </summary>
        /// <param name="fileName">file name</param>
        /// <param name="time">time array</param>
        /// <param name="voltage">voltage array</param>
        private void GetDataFromFile(string fileName, out double[] time, out double[] voltage)
        {
            time = null;
            voltage = null;

            using (FileStream dataFileFS = new FileStream(fileName, FileMode.Open))
            {
                using (StreamReader dataFileSR = new StreamReader(dataFileFS, Encoding.ASCII))
                {
                    //search of fileType

                    //1. FileTektronix7000Series
                    FileBaseDevice dataFileBaseDevice = new FileTektronix7000Series(dataFileSR);
                    bool res = dataFileBaseDevice.ProcessFileHeader();

                    //2. FileLecroyWave
                    if (!res)
                    {
                        dataFileSR.BaseStream.Position = 0L;
                        dataFileSR.DiscardBufferedData();
                        dataFileBaseDevice = new FileLecroyWave(dataFileSR);
                        res = dataFileBaseDevice.ProcessFileHeader();
                    }

                    //3. File with header of 1 str
                    if (!res)
                    {
                        dataFileSR.BaseStream.Position = 0L;
                        dataFileSR.DiscardBufferedData();
                        dataFileBaseDevice = new FileWithHeaderOf1str(dataFileSR);
                        res = dataFileBaseDevice.ProcessFileHeader();
                    }

                    //4. FileTektronix2kAnd3kSeries
                    if (!res)
                    {
                        dataFileSR.BaseStream.Position = 0L;
                        dataFileSR.DiscardBufferedData();
                        dataFileBaseDevice = new FileTektronix2kAnd3kSeries(dataFileSR);
                        res = dataFileBaseDevice.ProcessFileHeader();
                    }

                    if (!res)
                        dataFileBaseDevice = null;

                    if (dataFileBaseDevice == null || dataFileBaseDevice.ChannelsCount < 1 || !dataFileBaseDevice.FillChannelVoltages())
                    {//i.e. Unexpected file type
                        MessageBox.Show("Unexpected file type",  "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {// dataFile is tentative Ok.
                        
                        //we will take data of first channel (#0) and first "signal"(frame)
                        voltage = dataFileBaseDevice.Voltages[0][0];

                        time = dataFileBaseDevice.Times;
                        //time shift to start data from 0 sec.
                        if (time[0] != 0d)
                        {
                            double t0 = time[0];
                            for (int i = 0;i<time.Length;i++) 
                            {
                                time[i] -= t0;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// draw the waveform data in the OXYplot
        /// </summary>
        private void DrawWaveformInTheOXYPlot()
        {
            ThePlotModel.Series.Clear();

            //create series 
            var series = new OxyPlot.Series.LineSeries();

            for (int i = 0; i < voltageData.Length; i++)
            {
                series.Points.Add(new DataPoint(timeData[i], voltageData[i]));
            }
            ThePlotModel.Series.Add(series);

            ThePlotModel.InvalidatePlot(true);
        }
        #endregion

        #region PRF and voltage
        private double _PRF;
        /// <summary>
        /// pulse repitition frequency 
        /// </summary>
        public double PRF
        {
            get => _PRF;
            set
            {
                _PRF = value;
                NotifyPropertyChanged();

                if (TheAWG8195A == null)
                    return;

                Task.Run(async () =>
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );

                    try
                    {
                        await TheAWG8195A.SetInternalTriggerFrequencyAsync(PRF);
                    }
                    catch (Exception e)
                    {
                        string msg = e.Message;
                        MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        await Application.Current.Dispatcher.BeginInvoke(
                                   new Action(() =>
                                   {
                                       IsResposeAwaiting = false;
                                   }),
                                   DispatcherPriority.ContextIdle,
                                   null
                                   );
                    }
                });
            }
        }

        /// <summary>
        /// lower bound of PRF
        /// </summary>
        public double PRF_lowerBound
        { get; set; }

        /// <summary>
        /// upper bound of PRF
        /// </summary>
        public double PRF_upperBound
        { get; set; }

        private double _VoltageAmplitude;
        /// <summary>
        /// voltage amplitude
        /// </summary>
        public double VoltageAmplitude
        {
            get => _VoltageAmplitude;
            set
            {
                _VoltageAmplitude = value;
                NotifyPropertyChanged();

                if (TheAWG8195A == null)
                    return;

                Task.Run(async () =>
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );

                    try
                    {
                        await TheAWG8195A.SetOutputAmplitudeAsync(awgChannelID, VoltageAmplitude);
                    }
                    catch (Exception e)
                    {
                        string msg = e.Message;
                        MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally
                    {
                        await Application.Current.Dispatcher.BeginInvoke(
                                   new Action(() =>
                                   {
                                       IsResposeAwaiting = false;
                                   }),
                                   DispatcherPriority.ContextIdle,
                                   null
                                   );
                    }
                });
            }
        }

        /// <summary>
        /// lower bound of voltage amplitude
        /// </summary>
        public double VoltageAmplitude_lowerBound
        { get => AWG8195A.volageAmplMinValue; }

        /// <summary>
        /// upper bound of voltage amplitude
        /// </summary>
        public double VoltageAmplitude_upperBound
        { get => AWG8195A.volageAmplMaxValue; }
        #endregion

        #region signalGeneration
        /// <summary>
        /// Start generation of the signal
        /// </summary>
        /// <returns></returns>
        public async Task StartSignalGenerationAsync()
        {
            await Task.Run(async () =>
            {
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );

                try
                {
                    await TheAWG8195A.SetSegmentAsync(awgChannelID, awgSegmentID);

                    //check for any errors
                    bool res = await CheckErrorStateAsync();
                    if (res)
                        return;

                    await TheAWG8195A.StartSignalGenerationAsync();

                    await Application.Current.Dispatcher.BeginInvoke(
                               new Action(() =>
                               {
                                   IsSignalGenerating = true;
                               }),
                               DispatcherPriority.ContextIdle,
                               null
                               );
                }
                catch (Exception e)
                {
                    string msg = e.Message;
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                               new Action(() =>
                               {
                                   IsResposeAwaiting = false;
                               }),
                               DispatcherPriority.ContextIdle,
                               null
                               );
                }
            });
        }

        /// <summary>
        /// stop signal generation
        /// </summary>
        /// <returns></returns>
        public async Task StopSignalGenerationAsync()
        {
            await Task.Run(async () =>
            {
                await Application.Current.Dispatcher.BeginInvoke(
                            new Action(() =>
                            {
                                IsResposeAwaiting = true;
                            }),
                            DispatcherPriority.ContextIdle,
                            null
                            );

                try
                {
                    await TheAWG8195A.StopSignalGenearationAsync();
                    await Application.Current.Dispatcher.BeginInvoke(
                               new Action(() =>
                               {
                                   IsSignalGenerating = false;
                               }),
                               DispatcherPriority.ContextIdle,
                               null
                               );
                }
                catch (Exception e)
                {
                    string msg = e.Message;
                    MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    await Application.Current.Dispatcher.BeginInvoke(
                               new Action(() =>
                               {
                                   IsResposeAwaiting = false;
                               }),
                               DispatcherPriority.ContextIdle,
                               null
                               );
                }
            });
        }

        /// <summary>
        /// Check for instrument errors. If any - show it in a MsgBox.
        /// </summary>
        /// <returns></returns>
        public async Task<bool> CheckErrorStateAsync()
        {
            bool res = false;
            List<string> errors = await TheAWG8195A.GetErrorsAsync();
            if (errors.Count > 0)
            {
                res = true;

                string msg = string.Empty;
                foreach (string error in errors)
                {
                    msg += error + "\n\r";
                }
                MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
                res = false;

            return res;
        }
        #endregion

    }
}