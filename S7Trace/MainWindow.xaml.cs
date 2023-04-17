using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Threading;
using LiveCharts;
using LiveCharts.Wpf;
using Sharp7;

namespace S7Trace
{
    public partial class MainWindow : Window
    {
        private const int MaxDataPoints = 500; // Maximum value of points in livechart to keep the fine performance - try to push to the limits

        private S7Client plcClient;
        private CancellationTokenSource cancellationTokenSource;
        private ObservableCollection<PLCVariable> plcVariables;
        private ConcurrentQueue<ChartData> chartDataQueue;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize PLC client
            plcClient = new S7Client();

            // Initialize chart
            liveChart.Series = new SeriesCollection
            {
                new LineSeries { Title = "PLC Data" }
            };

            // Initialize DataGrid
            plcVariables = new ObservableCollection<PLCVariable>
            {
                    new PLCVariable
                    {
                        Name = "Var1",
                        AreaID = S7Area.DB,
                        Type = S7WordLength.Real, 
                        DBNumber = 1,
                        Offset = 0,
                        Enable = true
                    }
            };
            ConfigDataGrid.ItemsSource = plcVariables;

            //  default values for IP address, rack, and slot
            IpAddressTextBox.Text = "192.168.1.200";
            RackTextBox.Text = "0";
            SlotTextBox.Text = "0";

            // Initialize chart data queue
            chartDataQueue = new ConcurrentQueue<ChartData>();

            UpdateButtonStates();


        }
        public IEnumerable<S7WordLength> S7WordLengthValues => Enum.GetValues(typeof(S7WordLength)).Cast<S7WordLength>();

        public IEnumerable<S7Area> S7AreaValues => Enum.GetValues(typeof(S7Area)).Cast<S7Area>();

        private bool ValidateInput(out string ipAddress, out int rack, out int slot)
        {
            ipAddress = IpAddressTextBox.Text.Trim();
            if (string.IsNullOrEmpty(ipAddress))
            {
                MessageBox.Show("Please enter a valid IP address.");
                rack = 0;
                slot = 0;
                return false;
            }

            if (!int.TryParse(RackTextBox.Text, out rack))
            {
                MessageBox.Show("Please enter a valid rack number.");
                slot = 0;
                return false;
            }

            if (!int.TryParse(SlotTextBox.Text, out slot))
            {
                MessageBox.Show("Please enter a valid slot number.");
                return false;
            }

            return true;
        }

        private void UpdateButtonStates()
        {
            bool isConnected = plcClient.Connected;

            ConnectButton.IsEnabled = !isConnected;
            DisconnectButton.IsEnabled = isConnected;
            StartRecordingButton.IsEnabled = isConnected && !isRecording;
            StopRecordingButton.IsEnabled = isConnected && isRecording;
        }

        private bool isRecording = false;

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            
            if (plcClient == null)
            {
                plcClient = new S7Client();
            }

            // Get the IP address, rack, and slot from the user input
            if (!ValidateInput(out string ipAddress, out int rack, out int slot))
            {
                return;
            }

            // Connect to the PLC
            int result = plcClient.ConnectTo(ipAddress, rack, slot);

            // Check the connection result
            if (result == 0)
            {
                //MessageBox.Show("Connected successfully!", "Connection", MessageBoxButton.OK, MessageBoxImage.Information);

                UpdateButtonStates();
            }
            else
            {
                MessageBox.Show($"Connection failed (Error code: {result})", "Connection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (plcClient == null)
            {
                plcClient = new S7Client();
            }

            try
            {
                // Disconnect from the PLC
                plcClient.Disconnect();

                // Check the disconnection result
                if (!plcClient.Connected)
                {
                    //MessageBox.Show("Disconnected successfully!");
                }
                else
                {
                    MessageBox.Show("Disconnection failed.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Disconnection failed due to an exception: {ex.Message}");
            }

            // Update button states
            UpdateButtonStates();
        }

        private void StartRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;

            // Start the ReadPlcData method in a separate thread
            Task.Run(() => ReadPlcDataLoop(token));

            // Start the UpdateChart method in a separate thread
            Task.Run(() => UpdateChart());

            isRecording = true;
            UpdateButtonStates();
        }

        private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();

            isRecording = false;
            UpdateButtonStates();
        }

        private void ReadPlcDataLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Create a new instance of the S7MultiVar class
                S7MultiVar reader = new S7MultiVar(plcClient);

                // Add enabled variables to the reader
                List<PLCVariable> enabledVariables = new List<PLCVariable>();
                int enabledVariableCount = plcVariables.Count(v => v.Enable);
                byte[][] buffers = new byte[enabledVariableCount][];

                int bufferIndex = 0;
                foreach (var variable in plcVariables)
                {
                    if (variable.Enable)
                    {
                        int variableSize = GetBufferSizeForVariableType(variable.Type);
                        int bufferSize = variableSize * 2; // Word to Byte
                        buffers[bufferIndex] = new byte[bufferSize]; 
                        reader.Add((int)variable.AreaID, (int)variable.Type, variable.DBNumber, variable.Offset, variableSize, ref buffers[bufferIndex]);
                        enabledVariables.Add(variable);
                        bufferIndex++;
                    }
                }

                // Read all variables in the reader
                int result = reader.Read();

                // Check if the read operation was successful
                if (result == 0)
                {
                    // Extract the values from the buffers and enqueue them
                    for (int i = 0; i < enabledVariables.Count; i++)
                    {
                        double value = ExtractValueFromBuffer(enabledVariables[i], buffers[i]);
                        chartDataQueue.Enqueue(new ChartData { Variable = enabledVariables[i], Value = value });
                    }
                }

                Thread.Sleep(10);

            }
        }

        private int GetBufferSizeForVariableType(S7WordLength variableType)
        {
            // These values are represented as Words
            switch (variableType)
            {
                case S7WordLength.Bit:
                    return 1; // 1 byte for a bit
                case S7WordLength.Byte:
                    return 1; // 1 byte
                case S7WordLength.Word:
                    return 1; // 2 bytes
                case S7WordLength.DWord:
                    return 2; // 4 bytes
                case S7WordLength.Int:
                    return 1; // 2 bytes
                case S7WordLength.DInt:
                    return 2; // 4 bytes
                case S7WordLength.Real:
                    return 2; // 4 bytes
                default:
                    throw new ArgumentOutOfRangeException(nameof(variableType), $"Unsupported variable type: {variableType}");
            }
        }

        private double ExtractValueFromBuffer(PLCVariable variable, byte[] buffer)
        {
            double value = 0.0;

            switch (variable.Type)
            {
                case S7WordLength.Bit:
                    value = S7.GetBitAt(buffer, 0, 0) ? 1.0 : 0.0;
                    break;
                case S7WordLength.Byte:
                    value = buffer[0];
                    break;
                case S7WordLength.Word:
                    value = S7.GetWordAt(buffer, 0);
                    break;
                case S7WordLength.DWord:
                    value = S7.GetDWordAt(buffer, 0);
                    break;
                case S7WordLength.Int:
                    value = S7.GetIntAt(buffer, 0);
                    break;
                case S7WordLength.DInt:
                    value = S7.GetDIntAt(buffer, 0);
                    break;
                case S7WordLength.Real:
                    value = S7.GetRealAt(buffer, 0);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(variable.Type), $"Unsupported variable type: {variable.Type}");
            }

            return value;
        }

        private void UpdateChart()
        {
            while (true)
            {
                if (chartDataQueue.Count > 0)
                {
                    while (chartDataQueue.TryDequeue(out ChartData chartData))
                    {
                        Dispatcher.Invoke(() =>
                        {
                            var chartSeries = liveChart.Series.FirstOrDefault(series => series.Title == chartData.Variable.Name);

                            if (chartSeries == null)
                            {
                                // Add a new series for the variable if it doesn't exist
                                chartSeries = new LineSeries
                                {
                                    Title = chartData.Variable.Name,
                                    Values = new ChartValues<double>(),
                                };
                                liveChart.Series.Add(chartSeries);
                            }

                            // Add the value to the corresponding series
                            chartSeries.Values.Add(chartData.Value);

                            // Remove the oldest data point if the maximum number of data points is exceeded
                            if (chartSeries.Values.Count > MaxDataPoints)
                            {
                                chartSeries.Values.RemoveAt(0);
                            }
                        });
                    }
                    
                }

                Thread.Sleep(200);
            }
        }

    }

    public class PLCVariable
    {
        public bool Enable { get; set; }
        public string Name { get; set; }
        public S7Area AreaID { get; set; }
        public int DBNumber { get; set; }
        public S7WordLength Type { get; set; }
        public int Offset { get; set; }
    }

    public class ChartData
    {
        public PLCVariable Variable { get; set; }
        public double Value { get; set; }
    }

}