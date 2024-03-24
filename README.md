# S7Trace
Real-time PLC Data Charting Application

This application is a WPF-based real-time charting tool for visualizing data from PLCs using the S7 communication protocol. The application allows users to configure PLC variables, connect to the PLC, and display real-time data on charts.

## Features

- Configurable connection settings (IP address, rack, and slot)
- Configurable PLC variables (name, area ID, type, size, offset, and enable/disable)
- Real-time data visualization with charting
- Start/stop recording of data
- Supports multiple PLC variable types

![](https://github.com/kurcontko/S7Trace/blob/main/S7Trace-Screenshot.png)

## Roadmap

Here are some features and improvements planned for future releases:

- Implement MVVM (Model-View-ViewModel) design pattern
- Improve charting performance by optimizing data handling and rendering
- Add tools to manipulate, zoom, and measure values on the chart
- Implement user-defined chart styles and customization options
- Enhance error handling and user notifications
- Add support for multiple PLC communication protocols

## Getting Started

### Prerequisites

- .NET Framework 4.7.2 or higher
- Visual Studio 2019 or newer
- Sharp7 library (https://snap7.sourceforge.net/sharp7.html)
- LiveCharts (https://lvcharts.net/)

## Usage

1. Launch the application
2. Configure the connection settings (IP address, rack, and slot)
3. Configure the PLC variables you want to monitor
4. Click "Connect" to establish a connection with the PLC
5. Click "Start Recording" to begin charting the data in real-time
6. Click "Stop Recording" to stop the charting process
7. Click "Disconnect" to close the connection with the PLC

## Contributing

Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

## License

This project is licensed under the [MIT License](https://choosealicense.com/licenses/mit/).

## Acknowledgments

- [Sharp7](https://snap7.sourceforge.net/sharp7.html) for providing the S7 communication library
- [LiveCharts](https://lvcharts.net/) for providing the charting library (alternatively, you can use other charting libraries like [OxyPlot](https://github.com/oxyplot/oxyplot) or [LiveCharts2](https://github.com/beto-rodriguez/LiveCharts2))
