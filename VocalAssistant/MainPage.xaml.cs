using System;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;


namespace VocalAssistant
{
    public sealed partial class MainPage : Page
    {
        private BitmapImage animated_logo;
        private BitmapImage static_logo;
        private DispatcherTimer stopwatch;
        private uint stopwatch_count;

        public MainPage()
        {
            this.InitializeComponent();
            string animated_logo_uri = String.Format("ms-appx:///Assets/assistant_logos/logo_animated.gif");
            string static_logo_uri = String.Format("ms-appx:///Assets/assistant_logos/logo.png");
            animated_logo = new BitmapImage(new Uri(animated_logo_uri, UriKind.Absolute));
            static_logo = new BitmapImage(new Uri(static_logo_uri, UriKind.Absolute));
            dg_logo.Source = animated_logo;

            stopwatch_count = 0;
            stopwatch = new DispatcherTimer();
            stopwatch.Interval = new TimeSpan(0, 0, 1);
            stopwatch.Tick += ClockTick;
        }

        private void ClockTick(object sender, object e)
        {
            stopwatch_count++;
            uint hours = stopwatch_count / 3600;
            uint seconds = stopwatch_count % 3600;
            uint minutes = seconds / 60;
            seconds = seconds % 60;
            value.Text = hours.ToString("D2") + ":" + minutes.ToString("D2") + ":" + seconds.ToString("D2");
        }

        private void StartSwClick(object sender, RoutedEventArgs e) { stopwatch.Start(); }

        private void StopSwClick(object sender, RoutedEventArgs e) { stopwatch.Stop(); }

        private void ResetSwClick(object sender, RoutedEventArgs e)
        {
            stopwatch.Stop();
            stopwatch_count = 0;
            value.Text = "00:00:00";
        }


        public void SetVolumeSliderValue(double value) { volume.Value = value; }

        public void SetMediaPlayerText(string song_title, string song_artist)
        {
            title.Text = song_title;
            artist.Text = song_artist;
        }

        private void PlaySongClick(object sender, RoutedEventArgs e) { ((App)(Application.Current)).MediaPlayerPlay(); }

        private void StopSongClick(object sender, RoutedEventArgs e) { ((App)(Application.Current)).MediaPlayerStop(); }

        private void CloseMediaPlayerClick(object sender, RoutedEventArgs e) { ((App)(Application.Current)).MediaPlayerClose(); }

        private void PreviousSongClick(object sender, RoutedEventArgs e) { ((App)(Application.Current)).PreviousSong(); }

        private void NextSongClick(object sender, RoutedEventArgs e) { ((App)(Application.Current)).NextSong(); }

        private void SliderValueChanged(object sender, Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            ((App)(Application.Current)).SetMediaPlayerVolume(volume.Value);
        }


        public void PrintWeatherInfos(WeatherForecastObject weather_infos)
        {
            string icon_uri = String.Format("ms-appx:///Assets/weather_icons/{0}.png", weather_infos.weather[0].icon);
            icon.Source = new BitmapImage(new Uri(icon_uri, UriKind.Absolute));
            location.Text = weather_infos.name;
            description.Text = "Conditions: " + weather_infos.weather[0].description;
            temperature.Text = "Temperature: " + ((int)weather_infos.main.temp).ToString() + " °C   (min: " + ((int)weather_infos.main.temp_min).ToString() + " °C, max: " + ((int)weather_infos.main.temp_max).ToString() + " °C)";
            humidity.Text = "Humidity rate: " + ((int)weather_infos.main.humidity).ToString() + "%";
            wind.Text = "Wind speed: " + ((int)weather_infos.wind.speed).ToString() + " m/s";
        }


        public void SetRecipeName(string recipe_name) { name.Text = recipe_name; }

        public void SetRecipePhoto(string url) { photo.Source = new BitmapImage(new Uri(url, UriKind.Absolute)); }

        public void SetRecipeIngredients(string[] ingredients_list)
        {
            ingredients.Text = "";
            foreach(string ingredient in ingredients_list)
                ingredients.Text += "\u2022 " + ingredient + "\n";
        }

        private void ExitRecipeClick(object sender, RoutedEventArgs e) { ((App)(Application.Current)).RecipeGridToDefaultGrid(); }


        public void SetAnimatedLogo()
        {
            dg_logo.Source = animated_logo;
            mpg_logo.Source = animated_logo;
        }

        public void SetStaticLogo()
        {
            dg_logo.Source = static_logo;
            mpg_logo.Source = static_logo;
        }

        public void PrintText(string text) { dg_text.Text = text; }

        public void SwitchToDefaultGrid()
        {
            default_grid.Visibility = Visibility.Visible;
            weather_grid.Visibility = Visibility.Collapsed;
            media_player_grid.Visibility = Visibility.Collapsed;
            stopwatch_grid.Visibility = Visibility.Collapsed;
            recipe_grid.Visibility = Visibility.Collapsed;

            stopwatch.Stop();
            stopwatch_count = 0;
            value.Text = "00:00:00";
            dg_text.Text = "Hi, I'm Alan. How can I help you?";
        }

        public void SwitchToWeatherGrid()
        {
            default_grid.Visibility = Visibility.Collapsed;
            weather_grid.Visibility = Visibility.Visible;
            media_player_grid.Visibility = Visibility.Collapsed;
            stopwatch_grid.Visibility = Visibility.Collapsed;
            recipe_grid.Visibility = Visibility.Collapsed;
        }

        public void SwitchToMediaPlayerGrid()
        {
            default_grid.Visibility = Visibility.Collapsed;
            weather_grid.Visibility = Visibility.Collapsed;
            media_player_grid.Visibility = Visibility.Visible;
            stopwatch_grid.Visibility = Visibility.Collapsed;
            recipe_grid.Visibility = Visibility.Collapsed;
        }

        public void SwitchToStopwatchGrid()
        {
            default_grid.Visibility = Visibility.Collapsed;
            weather_grid.Visibility = Visibility.Collapsed;
            media_player_grid.Visibility = Visibility.Collapsed;
            stopwatch_grid.Visibility = Visibility.Visible;
            recipe_grid.Visibility = Visibility.Collapsed;
        }

        public void SwitchToRecipeGrid()
        {
            default_grid.Visibility = Visibility.Collapsed;
            weather_grid.Visibility = Visibility.Collapsed;
            media_player_grid.Visibility = Visibility.Collapsed;
            stopwatch_grid.Visibility = Visibility.Collapsed;
            recipe_grid.Visibility = Visibility.Visible;
        }

        private void RestartAppClick(object sender, RoutedEventArgs e) { CoreApplication.RequestRestartAsync(""); }

        private void RebootSysClick(object sender, RoutedEventArgs e)
        {
            Windows.System.ShutdownManager.BeginShutdown(Windows.System.ShutdownKind.Restart, TimeSpan.FromSeconds(0));
        }

        private void ShutdownSysClick(object sender, RoutedEventArgs e)
        {
            Windows.System.ShutdownManager.BeginShutdown(Windows.System.ShutdownKind.Shutdown, TimeSpan.FromSeconds(0));
        }
    }
}