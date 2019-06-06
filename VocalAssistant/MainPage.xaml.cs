using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;

// Il modello di elemento Pagina vuota è documentato all'indirizzo https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x410

namespace VocalAssistant
{
    /// <summary>
    /// Pagina vuota che può essere usata autonomamente oppure per l'esplorazione all'interno di un frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private BitmapImage logo_animated;
        private BitmapImage logo_static;

        public MainPage()
        {
            this.InitializeComponent();
            string icon_animated = String.Format("ms-appx:///Assets/assistant_logos/logo_animated.gif");
            string icon = String.Format("ms-appx:///Assets/assistant_logos/logo.png");
            logo_animated = new BitmapImage(new Uri(icon_animated, UriKind.Absolute));
            logo_static = new BitmapImage(new Uri(icon, UriKind.Absolute));
            logo.Source = logo_animated;
        }

        public void AnimatedLogo()
        {
            logo.Source = logo_animated;
            playerLogo.Source = logo_animated;
        }

        public void StaticLogo()
        {
            logo.Source = logo_static;
            playerLogo.Source = logo_static;

        }

        public void Output(string textToOutput)
        {
            output.Text = textToOutput;
        }

        public void SwitchToWeather()
        {
            defaultGrid.Visibility = Visibility.Collapsed;
            weatherGrid.Visibility = Visibility.Visible;
            musicPlayerGrid.Visibility = Visibility.Collapsed;
        }

        public void SwitchToDefault()
        {
            defaultGrid.Visibility = Visibility.Visible;
            weatherGrid.Visibility = Visibility.Collapsed;
            musicPlayerGrid.Visibility = Visibility.Collapsed;
        }

        public void SwitchToPlayer()
        {
            defaultGrid.Visibility = Visibility.Collapsed;
            weatherGrid.Visibility = Visibility.Collapsed;
            musicPlayerGrid.Visibility = Visibility.Visible;
        }

        public void WeatherOut(RootObject weather)
        {
            string icon = String.Format("ms-appx:///Assets/weather_icons/{0}.png", weather.weather[0].icon);
            ResultImage.Source = new BitmapImage(new Uri(icon, UriKind.Absolute));
            LocationTextBlock.Text = weather.name;
            DescriptionTextBlock.Text = "Conditions: " + weather.weather[0].description;
            TempTextBlock.Text = "Temperature: " + ((int)weather.main.temp).ToString() + " °C  (min: " + ((int)weather.main.temp_min).ToString() + " °C, max: " + ((int)weather.main.temp_max).ToString() + " °C)";
            HumidityTextBlock.Text = "Humidity: " + ((int)weather.main.humidity).ToString() + "%";
            WindTextBlock.Text = "Wind speed: " + ((int)weather.wind.speed).ToString() + " m/s";
        }

        public void SetVolumeSliderValue(double value)
        {
            volume.Value = value;
        }

        private void previous_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).SetMediaPlayerPrevious();
        }

        private void stop_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).CloseMediaPlayer();
        }

        private void play_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).SetMediaPlayerPlay();
        }

        private void pause_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).SetMediaPlayerStop();
        }

        private void next_Click(object sender, RoutedEventArgs e)
        {
            ((App)(Application.Current)).SetMediaPlayerNext();
        }

        private void slider_ValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            ((App)(Application.Current)).SetMediaPlayerVolume(volume.Value);
        }

        private void reboot_Click(object sender, RoutedEventArgs e)
        {
            Windows.System.ShutdownManager.BeginShutdown(Windows.System.ShutdownKind.Restart, TimeSpan.FromSeconds(0));
        }

        private void shutdown_Click(object sender, RoutedEventArgs e)
        {
            Windows.System.ShutdownManager.BeginShutdown(Windows.System.ShutdownKind.Shutdown, TimeSpan.FromSeconds(0));
        }

        private void restart_Click(object sender, RoutedEventArgs e)
        {
            CoreApplication.RequestRestartAsync("");
        }
    }
}
