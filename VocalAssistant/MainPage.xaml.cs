using System;
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
        }

        public void StaticLogo()
        {
            logo.Source = logo_static;
        }

        public void Output(string textToOutput)
        {
            output.Text = textToOutput;
        }

        public void SwitchToWeather()
        {
            defaultGrid.Visibility = Visibility.Collapsed;
            weatherGrid.Visibility = Visibility.Visible;
        }

        public void SwitchToDefault()
        {
            defaultGrid.Visibility = Visibility.Visible;
            weatherGrid.Visibility = Visibility.Collapsed;
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
    }
}
