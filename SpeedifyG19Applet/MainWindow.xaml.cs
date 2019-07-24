using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using JetBrains.Annotations;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using Logitech_LCD;
using SpeedifyCliWrapper;
using SpeedifyCliWrapper.Enums;
using SpeedifyCliWrapper.ReturnTypes;
using Utils.Misc;

namespace SpeedifyG19Applet
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        private readonly Speedify _wrapper = new Speedify();

        private readonly SpeedifyStats _stats;

        private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();

        private readonly Dictionary<Guid, StackedAreaSeries> _guidToSeries;
        private long _ticks = 0;

        private readonly Task _refresher;
        private double _axisMin;
        private double _axisMax;

        public MainWindow()
        {
            LogitechLcd.Instance.Init("Speedify", LcdType.Color);

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.Tick)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //lets save the mapper globally.
            Charting.For<MeasureModel>(mapper);

            this.YFormatter = val => $"{Misc.HumanReadableByteCount((long)val / 8, true).Replace('b', 'o')}/s";

            while (this._stats == null)
            {
                try
                {
                    this._stats = this._wrapper.Stats();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }

            this._refresher = this._wrapper.AsynRefreshStats(this._stats, this._cancellationSource.Token);

            this._guidToSeries = this._stats.Adapters.Where(a => a.State == AdapterState.Connected)
                .ToDictionary(k => k.AdapterId, v =>
                new StackedAreaSeries()
                {
                    Title = v.Name,
                    Values = new ChartValues<MeasureModel>(),
                    //LineSmoothness = 0
                }
            );

            this.SeriesCollection = new SeriesCollection();

            this.SeriesCollection.AddRange(this._guidToSeries.Values);

            this.SetAxisLimit(0);

            this.InitializeComponent();

            this.Applet.UpdateRate = 40; //limiting ourselves to 25ips to limit memory and CPU consumption

            this.DataContext = this;

            this._stats.PropertyChanged += this.StatsOnPropertyChanged;
            this.Closing += (sender, args) =>
            {
                this._cancellationSource.Cancel();

                Task.WaitAll(this._refresher, Task.Delay(500));
            };
        }



        private void StatsOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            this.Dispatcher.Invoke(() =>
            {
                if (propertyChangedEventArgs.PropertyName == nameof(this._stats.ConnectionStats))
                {
                    this._ticks++;
                    foreach (var connection in this._stats.ConnectionStats.Connections)
                    {
                        if (!this._guidToSeries.ContainsKey(connection.AdapterId))
                        {
                            var newAdapter = this._stats.Adapters.FirstOrDefault(a => a.AdapterId == connection.AdapterId);

                            //If we don't yet have any information on the new adapter, we just skip the value.
                            if (newAdapter == null) continue;

                            var serie = new StackedAreaSeries()
                            {
                                Title = newAdapter.Name,
                                Values = new ChartValues<MeasureModel>(),
                                //LineSmoothness = 0
                            };

                            this.SeriesCollection.Add(serie);

                            this._guidToSeries.Add(newAdapter.AdapterId, serie);
                        }

                        var chartValues = this._guidToSeries[connection.AdapterId].Values;

                        chartValues.Add(new MeasureModel(this._ticks, connection.TotalBps));

                        if (chartValues.Count > 50) chartValues.RemoveAt(0);
                    }

                    this.SetAxisLimit(this._ticks);
                }
            });

        }

        private void SetAxisLimit(long current)
        {
            this.AxisMax = current + 1;
            this.AxisMin = current - 24;
        }

        public double AxisMin
        {
            get { return this._axisMin; }
            set
            {
                if (value.Equals(this._axisMin)) return;
                this._axisMin = value;
                this.OnPropertyChanged();
            }
        }

        public double AxisMax
        {
            get { return this._axisMax; }
            set
            {
                if (value.Equals(this._axisMax)) return;
                this._axisMax = value;
                this.OnPropertyChanged();
            }
        }

        public SeriesCollection SeriesCollection { get; set; }
        public Func<double, string> YFormatter { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

