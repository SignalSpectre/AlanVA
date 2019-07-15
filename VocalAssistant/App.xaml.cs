using System;
using System.Collections.Generic;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using AssistantSpeechSynthesis;
using Windows.Media.Playback;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Windows.Storage;


namespace VocalAssistant
{
    public struct Reminder
    {
        public int day;
        public int month;
        public string content;
    }

    enum AssistantState { BACKGROUND_DEFAULT, LISTENING_DEFAULT, BACKGROUND_MEDIA_PLAYER, LISTENING_MEDIA_PLAYER, BACKGROUND_STOPWATCH, LISTENING_STOPWATCH };

    sealed partial class App : Application
    {
        private static MainPage main_page;
        private AssistantState state = AssistantState.BACKGROUND_DEFAULT;
        private SpeechRecognizer background_recognizer, task_recognizer;
        private SpeechService speaker;
        private MediaPlayer media_player;
        private List<Reminder> reminders = new List<Reminder>();
        private List<string> songs_list = new List<string>();
        private bool in_recipe_view = false;
        private double media_player_volume;
        private int song_idx;


        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
            AssistantSetup();
        }

        private async Task AssistantSetup()
        {
            // Initialize recognizers (speech-to-text) and speaker (text-to-speech)
            Windows.Globalization.Language language = new Windows.Globalization.Language("en-US");
            background_recognizer = new SpeechRecognizer(language);
            task_recognizer = new SpeechRecognizer(language);
            speaker = new SpeechService("Male", "en-US");

            task_recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(10);
            task_recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(5);

            // Initialize media player
            media_player = BackgroundMediaPlayer.Current;
            await CreateSongsList();

            // Compile recognizer's grammar
            SpeechRecognitionCompilationResult background_compilation_result = await background_recognizer.CompileConstraintsAsync();
            SpeechRecognitionCompilationResult task_compilation_result = await task_recognizer.CompileConstraintsAsync();

            // If compilation has been successful, start continuous recognition session
            if(background_compilation_result.Status == SpeechRecognitionResultStatus.Success)
                await background_recognizer.ContinuousRecognitionSession.StartAsync();

            await GUIOutput("Hi, I'm Alan. How can I help you?", true);

            // Set event handlers
            background_recognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSessionResultGenerated;
            background_recognizer.StateChanged += BackgroundRecognizerStateChanged;
            media_player.MediaEnded += MediaPlayerMediaEnded;
            media_player.CurrentStateChanged += MediaPlayerCurrentStateChanged;
        }

        // Vocal and visual output handling
        private async Task GUIOutput(string text, bool has_to_speak)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.PrintText(text);
                                        });
            if(has_to_speak)
                await speaker.SayAsync(text);
        }

        private void BackgroundRecognizerStateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            // Wakes up the background recognizer when it goes idle
            if (args.State.ToString() == "Idle" && (state == AssistantState.BACKGROUND_DEFAULT ||
                                                   state == AssistantState.BACKGROUND_MEDIA_PLAYER ||
                                                   state == AssistantState.BACKGROUND_STOPWATCH))
                background_recognizer.ContinuousRecognitionSession.StartAsync();

            // Switch from animated logo to static logo and vice versa
            if(state == AssistantState.BACKGROUND_DEFAULT || state == AssistantState.BACKGROUND_MEDIA_PLAYER)
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SetStaticLogo();
                                        });
            }
            else
            {
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SetAnimatedLogo();
                                        });
            }

            // Reset GUI            
            if(state == AssistantState.BACKGROUND_DEFAULT)
                GUIOutput("Hi, I'm Alan. How can I help you?", false);
        }

        private void ContinuousRecognitionSessionResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // Execute task according to the recognized speech
            if(args.Result.Text == "hey alan")
                TaskSelection();
        }

        private async Task TaskSelection()
        {
            if(state == AssistantState.BACKGROUND_DEFAULT)
                state = AssistantState.LISTENING_DEFAULT;
            else if(state == AssistantState.BACKGROUND_MEDIA_PLAYER)
            {
                state = AssistantState.LISTENING_MEDIA_PLAYER;
                media_player_volume = media_player.Volume;
                ChangeMediaPlayerVolume(10.0);
            }
            else if(state == AssistantState.BACKGROUND_STOPWATCH)
                state = AssistantState.LISTENING_STOPWATCH;

            // Stop continuous recognition session and wait for user's command
            await background_recognizer.ContinuousRecognitionSession.StopAsync();
            await GUIOutput("I'm listening.", true);
            SpeechRecognitionResult command = await task_recognizer.RecognizeAsync();

            // The list of possible tasks changes according to the state
            if(state == AssistantState.LISTENING_DEFAULT)
                await StandardTasksList(command.Text);
            else if(state == AssistantState.LISTENING_MEDIA_PLAYER)
                await MediaPlayerTasksList(command.Text);
            else if(state == AssistantState.LISTENING_STOPWATCH)
            {
                if(command.Text == "close")
                    CloseStopwatch();
                else
                {
                    await GUIOutput("I'm sorry, I couldn't understand.", true);
                    state = AssistantState.BACKGROUND_STOPWATCH;
                }
            }

            // Restart continuous recognition session
            await background_recognizer.ContinuousRecognitionSession.StartAsync();
            return;
        }

        private async Task StandardTasksList(string command)
        {
            if(command == "what time is it")
            {
                var now = System.DateTime.Now;
                await GUIOutput("It's " + now.Hour.ToString("D2") + ":" + now.Minute.ToString("D2"), true);
                state = AssistantState.BACKGROUND_DEFAULT;
            }
            else if(command == "what day is today")
            {
                await GUIOutput(System.DateTime.Today.ToString("D"), true);
                state = AssistantState.BACKGROUND_DEFAULT;
            }
            else if(command == "tell me a joke")
                await TellJoke();
            else if(command == "play some music")
                await PlayMusic();
            else if(command == "save a reminder")
                await SaveReminder();
            else if(command == "any plans for today")
                await SearchReminder();
            else if(command == "tell me something interesting")
                await RandomWikiArticle();
            else if(command == "what's the weather like today" || command == "how's the weather today")
                await GetWeatherInfos();
            else if(command == "inspire me")
                await GetInspiringQuote();
            else if(command == "open the stopwatch")
                await OpenStopwatch();
            else if(command == "search a recipe")
                await SearchRecipe();
            else
            {
                await GUIOutput("I'm sorry, I couldn't understand.", true);
                state = AssistantState.BACKGROUND_DEFAULT;
            }

            return;
        }


        private async Task TellJoke()
        {
            try
            {
                var http = new HttpClient();
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                var response = await http.GetAsync("https://icanhazdadjoke.com/");
                string joke = await response.Content.ReadAsStringAsync();
                await GUIOutput(joke, true);
            }
            catch
            {
                await GUIOutput("I'm sorry, I couldn't retrieve the informations from the web.", true);
            }

            state = AssistantState.BACKGROUND_DEFAULT;
        }


        private async Task PlayMusic()
        {
            StorageFile song = await Package.Current.InstalledLocation.GetFileAsync(@"Music\" + songs_list[song_idx]);

            // Switch from default grid to media player grid
            if(state == AssistantState.LISTENING_DEFAULT)
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                         () =>
                                         {
                                             main_page.SwitchToMediaPlayerGrid();
                                         });
            }

            await ChangeMediaPlayerVolume(0.35 * 100);
            media_player.AutoPlay = true;
            media_player.SetFileSource(song);
            state = AssistantState.BACKGROUND_MEDIA_PLAYER;
        }

        private async Task MediaPlayerTasksList(string command)
        {
            if(command == "close")
            {
                media_player.Pause();
                song_idx = 0;
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                       () =>
                                       {
                                           main_page.SwitchToDefaultGrid();
                                       });

                state = AssistantState.BACKGROUND_DEFAULT;
            }
            else if(command == "stop")
            {
                if(media_player.CurrentState == MediaPlayerState.Paused)
                    await GUIOutput("The music has already been stopped.", true);
                else
                    media_player.Pause();
                state = AssistantState.BACKGROUND_MEDIA_PLAYER;
            }
            else if(command == "play")
            {
                if(media_player.CurrentState == MediaPlayerState.Playing)
                    await GUIOutput("The music is already playing.", true);
                else
                    media_player.Play();
                state = AssistantState.BACKGROUND_MEDIA_PLAYER;
            }
            else if(command == "volume up")
            {
                if(media_player_volume <= 0.8)
                {
                    media_player_volume += 0.2;
                    ChangeMediaPlayerVolume(media_player_volume * 100);
                }

                state = AssistantState.BACKGROUND_MEDIA_PLAYER;
            }
            else if(command == "volume down")
            {
                if(media_player_volume >= 0.2)
                {
                    media_player_volume -= 0.2;
                    ChangeMediaPlayerVolume(media_player_volume * 100);
                }
                state = AssistantState.BACKGROUND_MEDIA_PLAYER;
            }
            else if(command == "next song")
            {
                media_player.Pause();
                if(song_idx + 1 >= songs_list.Count)
                    song_idx = 0;
                else
                    song_idx++;
                await PlayMusic();
            }
            else if(command == "previous song")
            {
                media_player.Pause();
                if(song_idx - 1 < 0)
                    song_idx = songs_list.Count - 1;
                else
                    song_idx--;
                await PlayMusic();
            }
            else
            {
                await GUIOutput("I'm sorry, I couldn't understand.", true);
                state = AssistantState.BACKGROUND_MEDIA_PLAYER;
            }
        }

        private async Task CreateSongsList()
        {
            StorageFolder installed_location = Package.Current.InstalledLocation;
            StorageFolder music_folder = await installed_location.GetFolderAsync(@"Music\");
            IReadOnlyList<StorageFile> files = await music_folder.GetFilesAsync();

            foreach(StorageFile file in files)
                if(file.Name.Contains(".wma") || file.Name.Contains(".mp3"))
                    songs_list.Add(file.Name);
            song_idx = 0;
        }

        private KeyValuePair<string, string> GetSongInfos(int idx)
        {
            string title, artist, temp;
            int i = songs_list[idx].LastIndexOf('.');
            temp = songs_list[idx].Remove(i);
            i = temp.LastIndexOf('-');

            if(i > 0)
            {
                title = temp.Remove(i);
                artist = temp.Remove(0, i + 1);
            }
            else
            {
                title = temp;
                artist = "";
            }

            return new KeyValuePair<string, string>(title, artist);
        }

        private void MediaPlayerMediaEnded(MediaPlayer sender, object args) { NextSong(); }

        // Slider volume control
        public void SetMediaPlayerVolume(double value) { media_player.Volume = value / 100.0; }

        // Media player Play button control
        public void MediaPlayerPlay()
        {
            if(media_player.CurrentState != MediaPlayerState.Playing)
                media_player.Play();
        }

        // Media player Close button control
        public async Task MediaPlayerClose()
        {
            media_player.Pause();
            song_idx = 0;

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                   () =>
                                   {
                                       main_page.SwitchToDefaultGrid();
                                   });

            state = AssistantState.BACKGROUND_DEFAULT;
        }

        // Media player Stop button control
        public void MediaPlayerStop()
        {
            if(media_player.CurrentState == MediaPlayerState.Playing)
                media_player.Pause();
        }

        // Media player Next button control
        public async Task NextSong()
        {
            media_player.Pause();
            if(song_idx + 1 >= songs_list.Count)
                song_idx = 0;
            else
                song_idx++;
            await PlayMusic();
        }

        // Media player Previous button control
        public async Task PreviousSong()
        {
            media_player.Pause();
            if(song_idx - 1 < 0)
                song_idx = songs_list.Count - 1;
            else
                song_idx--;
            await PlayMusic();
        }

        private void MediaPlayerCurrentStateChanged(MediaPlayer sender, object args)
        {
            if(media_player.CurrentState == MediaPlayerState.Playing)
            {
                KeyValuePair<string, string> song_infos = GetSongInfos(song_idx);
                ChangeMediaPlayerText(song_infos.Key, song_infos.Value);
            }
            else if(media_player.CurrentState == MediaPlayerState.Paused)
                ChangeMediaPlayerText("Stopped", "");
            else if(media_player.CurrentState == MediaPlayerState.Buffering)
                ChangeMediaPlayerText("Loading media", "");
        }

        // Media player volume control
        private async Task ChangeMediaPlayerVolume(double value)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SetVolumeSliderValue(value);
                                        });
        }

        // Media player text control
        private async Task ChangeMediaPlayerText(string title, string artist)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SetMediaPlayerText(title, artist);
                                        });
        }


        private async Task SaveReminder()
        {
            SpeechRecognizer reminder_recog = new SpeechRecognizer(new Windows.Globalization.Language("en-US"));
            StorageFile reminder_grammar = await Package.Current.InstalledLocation.GetFileAsync(@"reminder_grammar.xml");
            SpeechRecognitionGrammarFileConstraint date_rules = new SpeechRecognitionGrammarFileConstraint(reminder_grammar);
            reminder_recog.Constraints.Add(date_rules);
            var compilation_status = await reminder_recog.CompileConstraintsAsync();
            SpeechRecognitionResult date, content;
            bool done = false;
            await GUIOutput("For which day?", true);

            do
            {
                date = await reminder_recog.RecognizeAsync();
                if(date.Text.Length == 0)
                    await GUIOutput("Please, repeat what you said.", true);
                else
                {
                    await GUIOutput($"You said: {date.Text}. Is it correct?", true);
                    SpeechRecognitionResult ack = await task_recognizer.RecognizeAsync();
                    if(ack.Text == "yes")
                        done = true;
                    else
                        await GUIOutput("Please, repeat the date.", true);
                }
            } while(!done);

            await GUIOutput("What should I remember?", true);
            done = false;
            int cnt = 0;

            do
            {
                content = await task_recognizer.RecognizeAsync();
                if(content.Text.Length == 0)
                {
                    cnt++;
                    if(cnt < 3)
                        await GUIOutput("Please, repeat what you said.", true);
                }
                else
                {
                    await GUIOutput($"You said: {content.Text}. Is it correct?", true);
                    SpeechRecognitionResult ack = await task_recognizer.RecognizeAsync();
                    if(ack.Text == "yes")
                        done = true;
                    else
                    {
                        cnt++;
                        if(cnt < 3)
                            await GUIOutput("Please, repeat the reminder.", true);
                    }
                }
            } while(!done && cnt < 3);

            if(cnt < 3)
            {
                // Create reminder
                reminders.Add(CreateReminder(date.Text, content.Text));
                await GUIOutput("Reminder saved.", true);
            }
            else
                await GUIOutput("I'm sorry, I couldn't create the reminder.", true);

            state = AssistantState.BACKGROUND_DEFAULT;
        }

        private Reminder CreateReminder(string date, string content)
        {
            string day, month;
            Reminder rem;

            day = date.Split(' ')[1];
            month = date.Split(' ')[0];
            int.TryParse(day, out rem.day); // String to integer conversion

            switch(month)
            {
                case "january":
                    rem.month = 1;
                    break;
                case "february":
                    rem.month = 2;
                    break;
                case "march":
                    rem.month = 3;
                    break;
                case "april":
                    rem.month = 4;
                    break;
                case "may":
                    rem.month = 5;
                    break;
                case "june":
                    rem.month = 6;
                    break;
                case "july":
                    rem.month = 7;
                    break;
                case "august":
                    rem.month = 8;
                    break;
                case "september":
                    rem.month = 9;
                    break;
                case "october":
                    rem.month = 10;
                    break;
                case "november":
                    rem.month = 11;
                    break;
                case "december":
                    rem.month = 12;
                    break;
                default:
                    rem.month = 0;
                    break;
            }

            rem.content = content;
            return rem;
        }

        private async Task SearchReminder()
        {
            var today = System.DateTime.Now;
            int rem_count = 0;

            for(int i = 0; i < reminders.Count; i++)
            {
                if(reminders[i].month == today.Month && reminders[i].day == today.Day)
                {
                    rem_count++;
                    await GUIOutput($"Reminder {rem_count}: {reminders[i].content}.", true);
                }
            }

            if(rem_count == 0)
                await GUIOutput("You have no reminders for today.", true);

            state = AssistantState.BACKGROUND_DEFAULT;
        }


        private async Task RandomWikiArticle()
        {
            try
            {
                var http = new HttpClient();
                string rand_page_str, title, selected_page_str, content;
                int unvalid_seq, idx1, idx2;
                
                do
                {
                    var rand_page = await http.GetAsync("http://en.wikipedia.org/wiki/Special:Random");
                    rand_page_str = await rand_page.Content.ReadAsStringAsync();
                    unvalid_seq = rand_page_str.IndexOf("\\u");
                    idx2 = rand_page_str.IndexOf(" - Wikipedia");
                } while(idx2 == -1 || unvalid_seq != -1);

                idx1 = rand_page_str.IndexOf("<title>") + "<title>".Length;
                title = rand_page_str.Substring(idx1, idx2 - idx1).Replace(' ', '_');
                var selected_page = await http.GetAsync("https://en.wikipedia.org/w/api.php?format=json&action=query&prop=extracts&exintro=&explaintext=&exsentences=2&titles=" + title);
                selected_page_str = await selected_page.Content.ReadAsStringAsync();
                idx1 = selected_page_str.IndexOf("extract\":\"") + "extract\":\"".Length;
                idx2 = selected_page_str.IndexOf("\"}}}}");
                content = selected_page_str.Substring(idx1, idx2 - idx1);

                while(content.IndexOf(" (") != -1)
                {
                    idx1 = content.IndexOf(" (");
                    if(content.IndexOf("))") != -1)
                    {
                        idx2 = content.IndexOf("))") + 2;
                        content = content.Remove(idx1, idx2 - idx1);
                    }
                    else
                    {
                        idx2 = content.IndexOf(")") + 1;
                        content = content.Remove(idx1, idx2 - idx1);
                    }
                }

                while(content.IndexOf("\\n") != -1)
                {
                    idx1 = content.IndexOf("\\n");
                    content = content.Remove(idx1, 2);
                }

                content = content.Replace("  ", " ");
                content = content.Replace("   ", " ");
                content = content.Replace("\\\"", "\"");
                await GUIOutput(content, true);
            }
            catch
            {
                await GUIOutput("I'm sorry, I couldn't retrieve the informations from the web.", true);
            }
            
            state = AssistantState.BACKGROUND_DEFAULT;
        }


        private async Task GetWeatherInfos()
        {
            try
            {
                var position = await PositionManager.GetPosition();
                WeatherForecastObject weather_infos = await WeatherAPIManager.GetWeather(position.Coordinate.Latitude, position.Coordinate.Longitude);

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SwitchToWeatherGrid();
                                            main_page.PrintWeatherInfos(weather_infos);
                                        });

                await GUIOutput("The weather forecast for " + weather_infos.name + " are: " + weather_infos.weather[0].description +
                                " with temperature between " + ((int)weather_infos.main.temp_min).ToString() + " °C and " + ((int)weather_infos.main.temp_max).ToString() + " °C." +
                                " The humidity rate is equal to " + ((int)weather_infos.main.humidity).ToString() + " % and the wind speed is " + ((int)weather_infos.wind.speed).ToString() + " meters per second.", true);

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SwitchToDefaultGrid();
                                        });
            }
            catch
            {
                await GUIOutput("I'm sorry, I couldn't retrieve the informations from the web.", true);
            }

            state = AssistantState.BACKGROUND_DEFAULT;
        }


        private async Task GetInspiringQuote()
        {
            try
            {
                QuoteObject inspiring_quote = await QuoteAPIManager.GetQuote();
                await GUIOutput(inspiring_quote.contents.quotes[0].quote, true);
            }
            catch
            {
                await GUIOutput("I'm sorry, I couldn't retrieve the informations from the web.", true);
            }

            state = AssistantState.BACKGROUND_DEFAULT;
        }


        private async Task OpenStopwatch()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SwitchToStopwatchGrid();
                                        });

            state = AssistantState.BACKGROUND_STOPWATCH;
        }

        private async Task CloseStopwatch()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SwitchToDefaultGrid();
                                        });

            state = AssistantState.BACKGROUND_DEFAULT;
        }


        private async Task SearchRecipe()
        {
            await GUIOutput("What do you want to cook?", true);
            SpeechRecognitionResult food = await task_recognizer.RecognizeAsync();
            await GUIOutput("Searching a recipe for " + food.Text, false);

            try
            {
                var http = new HttpClient();
                var response = await http.GetAsync("https://api.edamam.com/search?q=" + food.Text + "&app_id=b4a3fd65&app_key=64c81e2e1ae114c948f814fc9c31041f&from=0&to=1");
                string recipe = await response.Content.ReadAsStringAsync();
                int idx1 = recipe.IndexOf("\"ingredientLines\" : [ \"") + "\"ingredientLines\" : [ \"".Length;
                int idx2 = recipe.IndexOf("\"ingredients\"") - 11;
                string ingredients = recipe.Substring(idx1, idx2 - idx1);
                ingredients = ingredients.Replace("\", \"", ";");
                var ingredients_list = ingredients.Split(';');

                idx1 = recipe.IndexOf("\"label\" : \"") + "\"label\" : \"".Length;
                idx2 = recipe.IndexOf("\"image\"") - 9;
                string recipe_name = recipe.Substring(idx1, idx2 - idx1);

                idx1 = recipe.IndexOf("\"image\" : \"") + "\"image\" : \"".Length;
                idx2 = recipe.IndexOf("\"source\"") - 9;
                string recipe_photo = recipe.Substring(idx1, idx2 - idx1);

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SwitchToRecipeGrid();
                                            main_page.SetRecipeName(recipe_name);
                                            main_page.SetRecipePhoto(recipe_photo);
                                            main_page.SetRecipeIngredients(ingredients_list);
                                        });

                // Stay in the recipe grid until the user clicks the exit button
                in_recipe_view = true;
                while(in_recipe_view);

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            main_page.SwitchToDefaultGrid();
                                        });
            }
            catch
            {
                await GUIOutput("I'm sorry, I couldn't retrieve the informations from the web.", true);
            }

            state = AssistantState.BACKGROUND_DEFAULT;
        }

        // Handles return to default grid from recipe grid
        public void RecipeGridToDefaultGrid() { in_recipe_view = false; }


        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if(rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.NavigationFailed += OnNavigationFailed;
                Window.Current.Content = rootFrame;
            }

            if(e.PrelaunchActivated == false)
            {
                if(rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }

                Window.Current.Activate();
                main_page = (MainPage)rootFrame.Content;
            }
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }
    }
}