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
using System.Text.Json;
using S7Trace.PLC;
using S7Trace.Logger;
using S7Trace.Models;
using S7Trace.Config;
using log4net;
using System.IO;
using Microsoft.Win32;
using S7Trace.Buffer;
using System.Windows.Threading;
using System.Windows.Media.Animation;
using System.Windows.Media;

namespace S7Trace
{
    public partial class MainWindow : Window
    {
        private const int MaxDataPoints = 500; // Maximum value of points in livechart to keep the fine performance - try to push to the limits

        private PlcService plcService;
        private CancellationTokenSource cancellationTokenSource;
        private ObservableCollection<PLCVariable> plcVariables;
        private ConcurrentQueue<ChartData> chartDataQueue;
        private ConcurrentQueue<LogData> logDataQueue;
        private static readonly ILog log = LogManager.GetLogger(typeof(MainWindow));
        private volatile bool isPlottingActive = false;
        private CircularBuffer<string> logBuffer = new CircularBuffer<string>(100);

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            // Initialize PLC client
            plcService = new PlcService();   


            // Initialize chart
            liveChart.Series = new SeriesCollection
            {
                new LineSeries { Title = "PLC Data" }
            };

            LoadConfiguration("config.json");

            // Initialize chart data queue
            chartDataQueue = new ConcurrentQueue<ChartData>();
            logDataQueue = new ConcurrentQueue<LogData>();

            UpdateButtonStates();

            DispatcherTimer logUpdateTimer = new DispatcherTimer();
            logUpdateTimer.Interval = TimeSpan.FromSeconds(1); // Update every second, adjust as needed
            logUpdateTimer.Tick += LogUpdateTimer_Tick;
            logUpdateTimer.Start();
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
            bool isConnected = plcService.IsConnected;

            ConnectButton.IsEnabled = !isConnected;
            DisconnectButton.IsEnabled = isConnected;
            StartRecordingButton.IsEnabled = isConnected && !isRecording;
            StopRecordingButton.IsEnabled = isConnected && isRecording;
        }

        private bool isRecording = false;

        private void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            
            if (plcService == null)
            {
                plcService = new PlcService();
            }

            // Get the IP address, rack, and slot from the user input
            if (!ValidateInput(out string ipAddress, out int rack, out int slot))
            {
                return;
            }

            // Connect to the PLC
            int result = plcService.Connect(ipAddress, rack, slot);

            // Check the connection result
            if (result == 0)
            {
                //MessageBox.Show("Connected successfully!", "Connection", MessageBoxButton.OK, MessageBoxImage.Information);

                UpdateButtonStates();
                ConnectionStatusIndicator.Fill = new SolidColorBrush(Colors.Green);
            }
            else
            {
                ConnectionStatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                MessageBox.Show($"Connection failed (Error code: {result})", "Connection", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (plcService == null)
            {
            plcService = new PlcService();
            }

            try
            {
                // Disconnect from the PLC
                plcService.Disconnect();

                // Check the disconnection result
                if (!plcService.IsConnected)
                {
                    //MessageBox.Show("Disconnected successfully!");
                    ConnectionStatusIndicator.Fill = new SolidColorBrush(Colors.Gray);
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
            var pulseAnimation = FindResource("PulseAnimation") as Storyboard;
            if (pulseAnimation != null)
            {
                pulseAnimation.Stop();
            }

            // Optionally, make the RecordingIndicator invisible after stopping the animation
            RecordingIndicator.Visibility = Visibility.Collapsed;
        }

        private void StartRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;

            // Start the ReadPlcData method in a separate thread
            Task.Run(() => plcService.ReadDataAsync(token, plcVariables, chartDataQueue, logDataQueue));

            // Start the UpdateChart method in a separate thread
            Task.Run(() => UpdateChart(token));

            // Start the logging thread
            StartLogging(token);

            isRecording = true;
            UpdateButtonStates();

            RecordingIndicator.Visibility = Visibility.Visible;

            var pulseAnimation = FindResource("PulseAnimation") as Storyboard;
            if (pulseAnimation != null)
            {
                Storyboard.SetTarget(pulseAnimation, RecordingIndicator);
                pulseAnimation.Begin();
            }
        }

        private void StopRecordingButton_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource?.Cancel();

            isRecording = false;
            UpdateButtonStates();
            var pulseAnimation = FindResource("PulseAnimation") as Storyboard;
            if (pulseAnimation != null)
            {
                pulseAnimation.Stop();
            }

            // Optionally, make the RecordingIndicator invisible after stopping the animation
            RecordingIndicator.Visibility = Visibility.Collapsed;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveConfiguration("config.json");
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            LoadConfiguration("config.json");
        }

        private void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*";
            saveFileDialog.DefaultExt = "json";
            saveFileDialog.AddExtension = true;

            if (saveFileDialog.ShowDialog() == true)
            {
            // Save the configuration to the chosen file
            SaveConfiguration(saveFileDialog.FileName);
            }
        }

        private void LoadAsButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON Files (*.json)|*.json|All files (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
            // Load the configuration from the selected file
            LoadConfiguration(openFileDialog.FileName);
            }
        }

        private void ActivatePlottingCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            isPlottingActive = true;
        }

        private void ActivatePlottingCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            isPlottingActive = false;
        }

        private void UpdateChart(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (chartDataQueue.Count > 0)
                {
                    while (chartDataQueue.TryDequeue(out ChartData chartData))
                    {
                        if (isPlottingActive)
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

        private void StartLogging(CancellationToken cancellationToken)
        {
            string filePath = "log.csv";
            string fallbackFilePath = "fallback_log.csv";

            CsvLogger logger = new CsvLogger(filePath, fallbackFilePath);

            Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested) // You might want a more graceful shutdown condition
                {
                    if (!logDataQueue.IsEmpty)
                    {
                        List<object> logObjects = new List<object>();

                        while (logDataQueue.TryDequeue(out LogData logData))
                        {
                            logObjects.Add(logData);
                            logBuffer.Add(logData.ToString());
                        }
                        
                        if (logObjects.Count > 0)
                        {
                            logger.EnqueueLogs(logObjects.ToArray());
                        }
                    }
                    else
                    {
                        await Task.Delay(100); // Use non-blocking wait
                    }
                }
            });
        }

        private void LogUpdateTimer_Tick(object sender, EventArgs e)
        {
            // Assuming logListView is your ListView control for displaying logs
            logListView.ItemsSource = logBuffer.ToArray();
        }

        public void SaveConfiguration(string filePath)
        {
            try
            {
            var config = new Configuration()
            {
                IPAddress = IpAddressTextBox.Text,
                Rack = int.TryParse(RackTextBox.Text, out int rack) ? rack : 0,
                Slot = int.TryParse(SlotTextBox.Text, out int slot) ? slot : 0,
                Variables = plcVariables.ToList()
            };

            string jsonString = JsonSerializer.Serialize(config);
            File.WriteAllText(filePath, jsonString);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to save configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void LoadConfiguration(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) throw new Exception("Configuration file does not exist.");

                string jsonString = File.ReadAllText(filePath);
                var config = JsonSerializer.Deserialize<Configuration>(jsonString);

                if (config == null) throw new Exception("Failed to load configuration.");

                IpAddressTextBox.Text = config.IPAddress ?? "192.168.0.1";
                RackTextBox.Text = config.Rack.ToString();
                SlotTextBox.Text = config.Slot.ToString();
                plcVariables = new ObservableCollection<PLCVariable>(config.Variables ?? new List<PLCVariable>());
                ConfigDataGrid.ItemsSource = plcVariables;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load configuration: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                IpAddressTextBox.Text = "192.168.0.1";
                RackTextBox.Text = "0";
                SlotTextBox.Text = "0";
            }
        }
    }
}