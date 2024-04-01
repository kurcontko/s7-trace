# S7Trace
Real-time PLC Data Logging and Charting Application

S7Trace is a robust, WPF-based application designed for real-time data logging and charting from PLCs using old, battletested the Siemens S7 communication protocol. It offers a user-friendly interface to configure PLC variables, connect to PLCs, log data to CSV files, and visualize real-time data through dynamic charts.

## Features

- Configurable connection settings (IP address, rack, and slot)
- Configurable PLC variables (name, area ID, type, size, offset, and enable/disable)
- CSV Logging of data
- Real-time data visualization with charting
- Supports multiple PLC variable types

![](https://github.com/kurcontko/S7Trace/blob/main/S7Trace-Screenshot-Config.png)

## Roadmap

Here are some features and improvements planned for future releases:

- Improve logging
- Implement database logging
- Improve charting performance by optimizing data handling and rendering
- Add tools to manipulate, zoom, and measure values on the chart
- Implement user-defined chart styles and customization options
- Enhance error handling and user notifications
- Add support for UDP or TCP communication
- Add support for multiple PLC communication protocols
- Add support for more protocols and devices
- Implement MVVM (Model-View-ViewModel) design pattern

## Getting Started

### Prerequisites

- .NET Framework 4.7.2 or higher
- Visual Studio 2019 or newer
- Sharp7 library (https://snap7.sourceforge.net/sharp7.html)
- LiveCharts (https://lvcharts.net/)

## Usage

1. Launch the S7Trace application.
2. Configure the connection settings (IP address, rack, and slot).
3. Set up the PLC variables you wish to monitor.
4. Connect to your PLC by clicking "Connect".
5. Start Recording to begin logging data.
6. Activate charting if you would like to visualize the data in real-time.
7. Stop Recording when you're done logging data.
8. Disconnect from the PLC.

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License

This project is licensed under the [MIT License](https://choosealicense.com/licenses/mit/).

## Acknowledgments

- [Sharp7](https://snap7.sourceforge.net/sharp7.html) for providing the S7 communication library
- [LiveCharts](https://lvcharts.net/) for providing the charting library (alternatively, you can use other charting libraries like [OxyPlot](https://github.com/oxyplot/oxyplot) or [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2))
