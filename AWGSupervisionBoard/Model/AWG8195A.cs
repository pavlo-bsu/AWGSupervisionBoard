using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ivi.Visa.Interop;

namespace Pavlo.AWGSupervisionBoard
{
    /// <summary>
    /// class for KeySight (c) arbitrary waveform generator AWG8195A. Model level
    /// </summary>
    public class AWG8195A:IDisposable
    {
        /// <summary>
        /// dt of samples corresponds to the SampleRate of 64 GSamples/Second
        /// </summary>
        public static readonly double data_dt = 1d / 64e9;

        /// <summary>
        /// AvaliableError for the dt (or sample rate). Units: fraction (0.01= 1%)
        /// </summary>
        public static readonly double data_dtAvaliableError = 0.01;

        /// <summary>
        /// min samples count of data 
        /// </summary>
        public static readonly int data_minSamplesCount = 1280;

        /// <summary>
        /// granularity of the data (i.e. samples count of data should be equals to data_granularity * some_int_value)
        /// </summary>
        public static readonly int data_granularity = 256;

        /// <summary>
        /// min value of idle delay
        /// </summary>
        public static readonly int idleDelayMinValue = 2560;

        /// <summary>
        /// max value of voltage amplitude
        /// </summary>
        public static readonly double volageAmplMaxValue = 1d;

        /// <summary>
        /// min value of voltage amplitude
        /// </summary>
        public static readonly double volageAmplMinValue = 0.075;

        private int _TimeOut = -1;
        /// <summary>
        /// value of timeout for each request to the device.
        /// [ms]
        /// </summary>
        private int TimeOut
        {
            get => _TimeOut;
            set { _TimeOut = value; }
        }

        private string _AWG_address = string.Empty;
        /// <summary>
        /// address of the AWG
        /// </summary>
        public string AWG_address
        {
            get => _AWG_address;

            protected set
            { _AWG_address = value; }
        }

        private bool _IsConnectionEstableshed = false;
        /// <summary>
        /// Is connection to AWG estableshed
        /// </summary>
        public bool IsConnectionEstableshed
        {
            get => _IsConnectionEstableshed;
            set { _IsConnectionEstableshed = value; }
        }

        public AWG8195A()
        {
            TimeOut = 3000;
        }

        public AWG8195A(string deviceAddress) : this()
        {
            this.AWG_address = deviceAddress;
        }

        private FormattedIO488Class _awgIO;
        public FormattedIO488Class awgIO
        {
            get => _awgIO;
            set => _awgIO = value;
        }

        /// <summary>
        /// connect to the instrument
        /// </summary>
        /// <returns></returns>
        public bool ConnectToTheAWG()
        {
            try
            {
                ResourceManager rm = new ResourceManager();
                awgIO = new FormattedIO488Class();
                awgIO.IO = (IMessage)rm.Open(AWG_address, AccessMode.NO_LOCK, 0, $"Timeout = {TimeOut}");
                IsConnectionEstableshed = true;
            }
            catch
            {
                IsConnectionEstableshed = false;
            }
            return IsConnectionEstableshed;
        }
        
        /// <summary>
        /// send a SCPI command to AWG
        /// </summary>
        /// <param name="scpiCommand">command</param>
        /// <returns></returns>
        private async Task SendCommandToAWGAsync(string scpiCommand)
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            //check preset
            if (!IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("Connection to AWG is not established!"));
                await tcs.Task;
            }

            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);


            using (ct.Token.Register(() => tcs.SetCanceled(), false))//for cancellation
            {
                awgIO.WriteString(scpiCommand, true);
                tcs.SetResult();
                return;
            }
        }

        /// <summary>
        /// request AWG Identity
        /// </summary>
        /// <returns>AWG Identity (producer, model and etc.)</returns>
        public async Task<string> RequestAWGIdentityAsync()
        {
            TaskCompletionSource<string> tcs = new TaskCompletionSource<string>();

            if (!IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("Connection to AWG is not established!"));
                await tcs.Task;
            }

            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);


            using (ct.Token.Register(() => tcs.SetCanceled(), false))
            {
                awgIO.WriteString("*idn?");
                var v = awgIO.ReadString();
                tcs.SetResult(v);
                return await tcs.Task;
            }
        }

        /// <summary>
        /// Start the signal generation
        /// </summary>
        /// <returns></returns>
        public async Task StartSignalGenerationAsync()
        {
            await SendCommandToAWGAsync(":INIT:IMM");
            return;
        }

        /// <summary>
        /// Stop the signal generation 
        /// </summary>
        /// <returns></returns>
        public async Task StopSignalGenearationAsync()
        {
            await SendCommandToAWGAsync(":ABOR");
            return;
        }

        /// <summary>
        /// Reset AWG
        /// </summary>
        /// <returns></returns>
        public async Task ResetAsync()
        {
            await SendCommandToAWGAsync("*RST");
            return;
        }

        /// <summary>
        /// Clear waveform memory
        /// </summary>
        /// <param name="channelID">channel ID</param>
        /// <returns></returns>
        public async Task ClearWaveformMemoryAsync(int channelID)
        {
            await SendCommandToAWGAsync($":TRAC{channelID}:DEL:ALL");
            return;
        }

        /// <summary>
        /// set the instrument to the MARKer mode.
        /// </summary>
        /// <returns></returns>
        public async Task SetToMarkerModeAsync()
        {
            await SendCommandToAWGAsync(":INST:DACM MARK");
            return;
        }

        /// <summary>
        /// set memory to Extended mode
        /// </summary>
        /// <param name="channelID"></param>
        /// <returns></returns>
        public async Task SetMemoryToExtendedModeAsync(int channelID)
        {
            await SendCommandToAWGAsync($":TRAC{channelID}:MMOD EXT");
            return;
        }

        /// <summary>
        /// Set the instrument to the sequence mode
        /// </summary>
        /// <returns></returns>
        public async Task SetToSequenceModeAsync()
        {
            await SendCommandToAWGAsync(":FUNC:MODE STSequence");
            return;
        }

        /// <summary>
        /// Switch the amplifier of the output path for a channel On or Off
        /// </summary>
        /// <param name="channelID">channel ID</param>
        /// <param name="ON">true - ON, false - OFF</param>
        /// <returns></returns>
        public async Task SetOutputAsync(int channelID, bool ON)
        {
            string state = ON ? "ON" : "OFF";
            await SendCommandToAWGAsync($":OUTP{channelID} {state}");
            return;
        }

        /// <summary>
        /// Select the waveform segment, that will be executed immediately after starting the signal generation.
        /// </summary>
        /// <param name="channelID">channel ID</param>
        /// <param name="segmentID">segment ID</param>
        /// <returns></returns>
        public async Task SetSegmentAsync(int channelID, int segmentID)
        {
            await SendCommandToAWGAsync($":TRAC{channelID}:SEL {segmentID}");
            return;
        }

        /// <summary>
        /// set advancement mode to AUTO
        /// </summary>
        /// <returns></returns>
        public async Task SetAdvancementModeToAUTOAsync()
        {
            await SendCommandToAWGAsync(":TRAC:ADV AUTO");
            return;
        }

        /// <summary>
        /// run in trigger mode (continous is off)
        /// </summary>
        /// <returns></returns>
        public async Task RunInTriggerModeAsync()
        {
            await SendCommandToAWGAsync(":INIT:CONT OFF");
            return;
        }

        /// <summary>
        /// set internal trigger
        /// </summary>
        /// <returns></returns>
        public async Task SetInternalTriggerAsync()
        {
            await SendCommandToAWGAsync(":ARM:TRIG:SOUR INT");
            return;
        }


        /// <summary>
        /// Set internal trigger frequency to the specified value
        /// </summary>
        /// <param name="frequency"></param>
        /// <returns></returns>
        public async Task SetInternalTriggerFrequencyAsync(double frequency)
        {
            await SendCommandToAWGAsync($":ARM:TRIGger:FREQuency {frequency}");
            return;
        }

        /// <summary>
        /// Set output amplitude.
        /// </summary>
        /// <param name="channelID">channel ID</param>
        /// <param name="ampl">output amplitude</param>
        /// <returns></returns>
        public async Task SetOutputAmplitudeAsync(int channelID, double ampl)
        {
            //is ampl value correct
            if (ampl < volageAmplMinValue || ampl > volageAmplMaxValue)
            {//not correct value
                ampl = volageAmplMinValue;//let it be default value
            }
            string s = ampl.ToString("N3", new System.Globalization.CultureInfo("en-US"));

            await SendCommandToAWGAsync($":VOLT {s}");
            return;
        }

        /// <summary>
        /// Get errors list from the instrument
        /// </summary>
        /// <returns>errors list</returns>
        public async Task<List<string>> GetErrorsAsync()
        {

            TaskCompletionSource<List<string>> tcs = new TaskCompletionSource<List<string>>();

            if (!IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("Connection to AWG is not established!"));
                await tcs.Task;
            }

            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);

            using (ct.Token.Register(() => tcs.SetCanceled(), false))
            {
                List<string> errors = new List<string>();
                string[] strError;

                do
                {
                    awgIO.WriteString("SYSTem:ERRor?", true);
                    string errMsg = awgIO.ReadString();
                    
                    //check instrument responce
                    strError = errMsg.Split(",");
                    if (Convert.ToInt16(strError[0]) != 0)
                    {//error
                        errors.Add(errMsg);
                    }
                } while (Convert.ToInt16(strError[0]) != 0);

                tcs.SetResult(errors);
                return await tcs.Task;
            }
        }

        /// <summary>
        /// Close the resource
        /// </summary>
        public void CloseResource()
        {
            awgIO?.IO?.Close();
        }

        #region workWithDataFile
        /// <summary>
        /// Chech the input data array. If data not match the demands, an exception is thrown.
        /// </summary>
        /// <param name="times">equidistant time values</param>
        /// <param name="voltages">voltage values</param>
        /// <returns>true if Data is OK. If data is not valid, then exception will be thrown</returns>
        public bool CheckTheData(double[] times, double[] voltages)
        {
            double rawdata_dt = times[1] - times[0];

            //check rawData - sample rate
            if (Math.Abs(data_dt - rawdata_dt) > data_dt * data_dtAvaliableError)
            {
                throw new ArgumentException("Sample Rate of the data doesn't correspond to the Sample Rate of AWG8195A (64GSa)");
            }

            //check raw data - granularity
            if (voltages.Length % data_granularity != 0)
                throw new ArgumentException($"Granularity for data is {data_granularity}, i.e. samples count of data should be equals to {data_granularity}*some_int_value");

            //check min length
            if (voltages.Length < data_minSamplesCount)
                throw new ArgumentException($"Samples count should be no less than {data_minSamplesCount}");

            return true;
        }

        /// <summary>
        /// convert voltage values to internal byte representation 
        /// </summary>
        /// <param name="times">equidistant time values</param>
        /// <param name="voltages">voltage values</param>
        /// <returns>voltage values converted to byte</returns>
        public byte[] ConvertWaveformToInternalByteRepresentation(double[] times, double[] voltages)
        {
            CheckTheData(times,voltages);

            byte[] byteYArray = new byte[times.Length];
            
            //search for the ampl of the signal
            double maxVal = Math.Abs(voltages.Max());
            double minVal = Math.Abs(voltages.Min());
            double ampl = maxVal > minVal ? maxVal : minVal;

            for (int i = 0; i < byteYArray.Length; i++) 
            {
                byte b = (byte)(127d*voltages[i]); ;
                byteYArray[i] = b;
            }
            return byteYArray;
        }

        /// <summary>
        /// Add waveform to specified segment of specified channel
        /// </summary>
        /// <param name="byteYArray">voltage data in representation of byte array</param>
        /// <param name="channelID">Channel ID</param>
        /// <param name="segmentID">Segment ID</param>
        /// <returns></returns>
        public async Task AddWaveformToSegmentAsync(byte[] byteYArray, int channelID, int segmentID)
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            if (!IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("Connection to AWG is not established!"));
                await tcs.Task;
            }

            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);


            using (ct.Token.Register(() => tcs.SetCanceled(), false))
            {
                awgIO.WriteString($":TRAC{channelID}:DEF {segmentID},{byteYArray.Length}", true);
                awgIO.WriteIEEEBlock($":TRAC{channelID}:data {segmentID},0,", byteYArray, true);
                tcs.SetResult();
                return;
            }
        }
        #endregion

        #region SequenceTable
        /// <summary>
        /// Create a data entry of the sequence table
        /// </summary>
        /// <param name="sequenceTableID">ID of record in sequence table</param>
        /// <param name="controlMarker">control marker</param>
        /// <param name="sequenceLoopCount">loop count of the sequence</param>
        /// <param name="segmentLoopCount">loop count of the segment</param>
        /// <param name="segmentID">ID of segment</param>
        /// <param name="segmentStartOffset">Segment Start Offset</param>
        /// <param name="segmentEndOffset">Segment End Offset</param>
        /// <returns></returns>
        public async Task CreateSequenceTableEntryAsync(int sequenceTableID, SequenceControlMarker controlMarker, int sequenceLoopCount, int segmentLoopCount, int segmentID, int segmentStartOffset, int segmentEndOffset)
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            if (!IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("Connection to AWG is not established!"));
                await tcs.Task;
            }

            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);


            using (ct.Token.Register(() => tcs.SetCanceled(), false))
            {
                awgIO.WriteString($":SOURce:STABle:DATA {sequenceTableID},{(UInt32)controlMarker},{sequenceLoopCount},{segmentLoopCount},{segmentID},{segmentStartOffset},{segmentEndOffset}", true);
                tcs.SetResult();
                return;
            }
        }

        /// <summary>
        /// Create an Idle entry of the sequence table
        /// </summary>
        /// <param name="sequenceTableID">ID of record in sequence table</param>
        /// <param name="controlMarker">control marker</param>
        /// <param name="sequenceLoopCount">loop count of the sequence</param>
        /// <param name="idleSampleValue">the DAC valued displayed during idle delay time</param>
        /// <param name="idleDelay">time in samples a constant idle sample is displayed</param>
        /// <returns></returns>
        public async Task CreateIdleTableEntryAsync(int sequenceTableID, SequenceControlMarker controlMarker, int sequenceLoopCount, sbyte idleSampleValue, uint idleDelay)
        {
            TaskCompletionSource tcs = new TaskCompletionSource();

            if (!IsConnectionEstableshed)
            {
                tcs.SetException(new NullReferenceException("Connection to AWG is not established!"));
                await tcs.Task;
            }

            CancellationTokenSource ct = new CancellationTokenSource();
            ct.CancelAfter(this.TimeOut);


            using (ct.Token.Register(() => tcs.SetCanceled(), false))
            {
                awgIO.WriteString($":SOURce:STABle:DATA {sequenceTableID},{(UInt32)controlMarker},{sequenceLoopCount},0,{idleSampleValue},{idleDelay},0", true);
                tcs.SetResult();
                return;
            }
        }
        #endregion

        #region IDisposable by Microsoft recommendation
        private bool disposed = false;

        // realization of IDisposable.
        public void Dispose()
        {
            Dispose(true);
            // suppress finilization
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed) return;
            if (disposing)
            {
                //dispose managed resources
                //serialport is a managed resource, which itself owns an unmanaged resource 
                //see https://stackoverflow.com/questions/4826702/is-serialport-in-net-unmanaged-resource-is-my-wrapped-class-correct
                awgIO?.IO?.Close();
            }
            //dispose unmanaged resources
            //there is no unmanaged resources

            disposed = true;
        }

        // Деструктор
        ~AWG8195A()
        {
            Dispose(false);
        }
        #endregion
    }

    /// <summary>
    /// Sequence control marker
    /// </summary>
    public enum SequenceControlMarker: UInt32
    {
        None = 0,
        InitMarkerSequence = 0x10000000,
        EndMarkerSequence = 0x40000000,
        MarkerEnable = 0x01000000,
        CommandFlag = 0x80000000,
    }
}
