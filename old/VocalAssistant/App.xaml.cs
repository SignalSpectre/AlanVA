using System;
using System.Collections.Generic;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using Windows.Storage;
using Windows.Media.Playback;
using System.Threading.Tasks;
using AssistantSpeechSynthesis;
using System.Net.Http;
using System.Net.Http.Headers;

namespace VocalAssistant
{
    public struct Remainder
    {
        public int day;
        public int month;
        public string details;
    }

    enum AssistantState { BACKGROUND_LISTEN, LISTENING, BACKGROUND_PLAYING_SONG, LISTENING_PLAYING_SONG, BACKGROUND_STOPWATCH, LISTENING_STOPWATCH };

    sealed partial class App : Application
    {
        private static MainPage this_main_page;

        private SpeechRecognizer background_recognizer;
        private SpeechRecognizer task_recognizer;
        private SpeechService speaker;

        private MediaPlayer player;
        private double player_volume;
        private AssistantState state = AssistantState.BACKGROUND_LISTEN;
        private List<string> songList = new List<string>();
        private int songIndex;
        private List<Remainder> remainders = new List<Remainder>();
        private bool remain_in_recipe_view = false;


        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;

            AssistantSetup();
        }

        //Assistant initialization function
        private async Task AssistantSetup()
        {
            // Initialize SpeechRecognizer object
            Windows.Globalization.Language language = new Windows.Globalization.Language("en-US");
            background_recognizer = new SpeechRecognizer(language);
            task_recognizer = new SpeechRecognizer(language);

            task_recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromSeconds(10);
            task_recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromSeconds(5);

            // Initialize media player
            player = BackgroundMediaPlayer.Current;

            //Create song list
            await CreateSongList();

            // Initialize SpeechSynthesizer object
            speaker = new SpeechService("Male", "en-US");

            // Compile grammar
            SpeechRecognitionCompilationResult backup_compilation_result = await background_recognizer.CompileConstraintsAsync();
            SpeechRecognitionCompilationResult task_compilation_result = await task_recognizer.CompileConstraintsAsync();

            // If compilation successful, start continuous recognition session
            if (backup_compilation_result.Status == SpeechRecognitionResultStatus.Success)
                await background_recognizer.ContinuousRecognitionSession.StartAsync();

            await GUIOutput("Hi, I'm Alan. How can I help you?", true);

            // Set event handlers
            background_recognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            background_recognizer.StateChanged += background_recognizer_StateChanged;
            player.MediaEnded += player_MediaEnded;
            player.CurrentStateChanged += player_CurrentStateChanged;
        }

        //Print function
        private async Task GUIOutput(string text, bool has_to_speak)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.Output(text);
                                        });

            if(has_to_speak)
                await speaker.SayAsync(text);
        }

        //Player volume control function
        private async Task PlayerVolumeSet(double value)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.SetVolumeSliderValue(value);
                                        });
        }

        //Player text control function
        private async Task PlayerTextSet(string title, string artist)
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.SetMediaPlayerText(title, artist);
                                        });
        }

        private void player_MediaEnded(MediaPlayer sender, object args)
        {
            SetMediaPlayerNext();
        }

        private void player_CurrentStateChanged(MediaPlayer sender, object args)
        {
            if (player.CurrentState == MediaPlayerState.Playing)
            {
                PlayerTextSet(ExtractMetadata(songIndex).Key, ExtractMetadata(songIndex).Value);
            }
            else if (player.CurrentState == MediaPlayerState.Paused)
                PlayerTextSet("Paused", "");
            else if (player.CurrentState == MediaPlayerState.Buffering)
                PlayerTextSet("Loading media", "");
        }

        private void background_recognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            if (args.State.ToString() == "Idle" && (state == AssistantState.BACKGROUND_LISTEN || state == AssistantState.BACKGROUND_PLAYING_SONG || state == AssistantState.BACKGROUND_STOPWATCH))
                background_recognizer.ContinuousRecognitionSession.StartAsync();

            //Change logo image
            if(state == AssistantState.BACKGROUND_LISTEN || state == AssistantState.BACKGROUND_PLAYING_SONG)
            {
                //Set image to static
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.StaticLogo();
                                        });
            }
            else
            {
                //Set image to animated
                Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.AnimatedLogo();
                                        });
            }

            //Reset GUI script            
            if (state == AssistantState.BACKGROUND_LISTEN)
                GUIOutput("Hi, I'm Alan. How can I help you?", false);
        }

        private void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // Execute tasks acoording to recognized speech
            if (args.Result.Text == "hey alan")
                VocalAssistantFunctionality();

        }

        private async Task VocalAssistantFunctionality()
        {
            //Change assistant state
            if (state == AssistantState.BACKGROUND_LISTEN)
                state = AssistantState.LISTENING;
            else if (state == AssistantState.BACKGROUND_PLAYING_SONG)
            {
                state = AssistantState.LISTENING_PLAYING_SONG;
                player_volume = player.Volume;
                PlayerVolumeSet(10.0);
            }
            else if (state == AssistantState.BACKGROUND_STOPWATCH)
                state = AssistantState.LISTENING_STOPWATCH;

            // Stop continuous recognition session
            await background_recognizer.ContinuousRecognitionSession.StopAsync();

            await GUIOutput("I'm listening.", true);

            //listen for user command
            SpeechRecognitionResult command = await task_recognizer.RecognizeAsync();

            //select task list by state
            if (state == AssistantState.LISTENING)
                await NormalTaskList(command.Text);
            else if (state == AssistantState.LISTENING_PLAYING_SONG)
            {
                await SongTaskList(command.Text);
                //player.Volume = player_volume;
            }
            else if (state == AssistantState.LISTENING_STOPWATCH)
            {
                if(command.Text == "close")
                {
                    CloseStopWatch();
                }
                else
                {
                    await GUIOutput("I'm sorry, I couldn't understand.", true);
                }
            }

            //Restart continuos recognition session
            await background_recognizer.ContinuousRecognitionSession.StartAsync();

            return;
        }

        private async Task NormalTaskList(string command)
        {
            //Select functionality by command
            if (command == "what time is it")
            {
                var now = System.DateTime.Now;
                await GUIOutput("It's " + now.Hour.ToString("D2") + ":" + now.Minute.ToString("D2"), true);

                state = AssistantState.BACKGROUND_LISTEN;
            }
            else if (command == "what day is today")
            {
                await GUIOutput(System.DateTime.Today.ToString("D"), true);

                state = AssistantState.BACKGROUND_LISTEN;
            }
            else if (command == "tell me a joke")
                await TellJokes();
            else if (command == "play some music")
                await PlayMusic();
            else if (command == "take a reminder" || command == "make a reminder")
                await MakeRemainder();
            else if (command == "any plans for today" || command == "any plans today")
                await SearchRemainder();
            else if (command == "tell me something interesting")
                await RandomOnWiki();
            else if (command == "what's the weather like today" || command == "how's the weather today")
                await GetWeatherInfo();
            else if (command == "inspire me")
                await GetInspiringQuote();
            else if (command == "open the stopwatch")
                await OpenStopWatch();
            else if (command == "find a recipe for me")
                await FindRecipe();
            else
            {
                await GUIOutput("I'm sorry, I couldn't understand.", true);

                state = AssistantState.BACKGROUND_LISTEN;
            }

            return;
        }

        private async Task TellJokes()
        {
            try
            {
                var http = new HttpClient();
                http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
                var content = await http.GetAsync("https://icanhazdadjoke.com/");
                var joke = await content.Content.ReadAsStringAsync();

                await GUIOutput(joke, true);
            }
            catch
            {
                await GUIOutput("I'm sorry, I couldn't retrieve the informations from the web.", true);
            }

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private async Task PlayMusic()
        {
            //Read the media file
            StorageFile song = await Package.Current.InstalledLocation.GetFileAsync(@"Music\" + songList[songIndex]);


            //Switch the main page grid to music player
            if (state == AssistantState.LISTENING)
            {
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                         () =>
                                         {
                                             this_main_page.SwitchToPlayer();
                                         });
            }

            await PlayerVolumeSet(0.35 * 100);

            //Play it
            player.AutoPlay = true;
            player.SetFileSource(song);

            //Change the state of the assistant
            state = AssistantState.BACKGROUND_PLAYING_SONG;
        }

        private async Task SongTaskList(string command)
        {
            if (command == "close")
            {
                player.Pause();
                songIndex = 0;

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                       () =>
                                       {
                                           this_main_page.SwitchToDefault();
                                       });

                state = AssistantState.BACKGROUND_LISTEN;
            }
            else if (command == "stop")
            {
                if (player.CurrentState == MediaPlayerState.Paused)
                    await GUIOutput("I'm sorry, the music player is already paused.", true);
                else
                    player.Pause();
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
            else if (command == "play")
            {
                if (player.CurrentState == MediaPlayerState.Playing)
                    await GUIOutput("I'm sorry, the music player is already playing.", true);
                else
                    player.Play();
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
            else if (command == "volume up")
            {
                if (player_volume <= 0.8)
                {
                    player_volume += 0.2;
                    PlayerVolumeSet(player_volume * 100);
                }
                    
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
            else if (command == "volume down")
            {
                if (player_volume >= 0.2)
                {
                    player_volume -= 0.2;
                    PlayerVolumeSet(player_volume * 100);
                }
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
            else if (command == "next")
            {
                player.Pause();
                if (songIndex + 1 >= songList.Count)
                    songIndex = 0;
                else
                    songIndex++;
                await PlayMusic();
            }
            else if (command == "previous")
            {
                player.Pause();
                if (songIndex - 1 < 0)
                    songIndex = songList.Count - 1;
                else
                    songIndex--;
                await PlayMusic();
            }
            else
            {
                await GUIOutput("I'm sorry, I couldn't understand.", true);
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
        }

        private async Task MakeRemainder()
        {
            SpeechRecognizer remainder_recon = new SpeechRecognizer(new Windows.Globalization.Language("en-US"));
            StorageFile date_grammar = await Package.Current.InstalledLocation.GetFileAsync(@"remainder_rules.xml");

            SpeechRecognitionGrammarFileConstraint date_constraint = new SpeechRecognitionGrammarFileConstraint(date_grammar);
            remainder_recon.Constraints.Add(date_constraint);

            var compilation_status = await remainder_recon.CompileConstraintsAsync();

            await GUIOutput("For which day?", true);

            SpeechRecognitionResult date;
            SpeechRecognitionResult details;
            bool finished = false;
            do
            {
                date = await remainder_recon.RecognizeAsync();
                if (date.Text.Length == 0)
                    await GUIOutput("Please, repeat what you said.", true);
                else
                {
                    await GUIOutput($"You said: {date.Text}. Is it correct?", true);
                    SpeechRecognitionResult ack = await task_recognizer.RecognizeAsync();
                    if (ack.Text == "yes")
                        finished = true;
                    else
                        await GUIOutput("Please, repeat the date.", true);
                }
            } while (!finished);

            await GUIOutput("What should I remember?", true);
            finished = false;
            int cnt = 0;
            do
            {
                details = await task_recognizer.RecognizeAsync();
                if (details.Text.Length == 0)
                {
                    cnt++;
                    if (cnt < 3)
                        await GUIOutput("Please, repeat what you said.", true);
                }
                else
                {
                    await GUIOutput($"You said: {details.Text}. Is it correct?", true);
                    SpeechRecognitionResult ack = await task_recognizer.RecognizeAsync();
                    if (ack.Text == "yes")
                        finished = true;
                    else
                    {
                        cnt++;
                        if (cnt < 3)
                            await GUIOutput("Please, repeat the remainder.", true);
                    }
                }
            } while (!finished && cnt < 3);

            if (cnt < 3)
            {
                //add the reaminder to the list
                remainders.Add(CreateRemainder(date.Text, details.Text));

                await GUIOutput("Remainder saved.", true);
            }
            else
                await GUIOutput("Sorry, I couldn't create the remainder.", true);

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private Remainder CreateRemainder(string date, string details)
        {
            string day, month;
            Remainder output;

            day = date.Split(' ')[1];
            month = date.Split(' ')[0];

            int.TryParse(day, out output.day);

            switch (month)
            {
                case "january":
                    output.month = 1;
                    break;
                case "february":
                    output.month = 2;
                    break;
                case "march":
                    output.month = 3;
                    break;
                case "april":
                    output.month = 4;
                    break;
                case "may":
                    output.month = 5;
                    break;
                case "june":
                    output.month = 6;
                    break;
                case "july":
                    output.month = 7;
                    break;
                case "august":
                    output.month = 8;
                    break;
                case "september":
                    output.month = 9;
                    break;
                case "october":
                    output.month = 10;
                    break;
                case "november":
                    output.month = 11;
                    break;
                case "december":
                    output.month = 12;
                    break;
                default:
                    output.month = 0;
                    break;
            }

            output.details = details;

            return output;
        }

        private async Task SearchRemainder()
        {
            var now = System.DateTime.Now;
            int rem = 0;

            for (int i = 0; i < remainders.Count; i++)
            {
                if (remainders[i].month == now.Month && remainders[i].day == now.Day)
                {
                    rem++;
                    await GUIOutput($"Remainder {rem}: {remainders[i].details}.", true);
                }
            }

            if (rem == 0)
                await GUIOutput("You have no remainders for today.", true);

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private async Task CreateSongList()
        {
            StorageFolder installed_location = Package.Current.InstalledLocation;
            StorageFolder music_folder = await installed_location.GetFolderAsync(@"Music\");
            IReadOnlyList<StorageFile> files = await music_folder.GetFilesAsync();

            foreach (StorageFile file in files)
            {
                if (file.Name.Contains(".wma") || file.Name.Contains(".mp3"))
                    songList.Add(file.Name);
            }

            songIndex = 0;
        }

        private async Task RandomOnWiki()
        {
            try
            {
                var http = new HttpClient();

                string page_str, title;
                int unvalid_seq, idx1, idx2;
                do
                {
                    var rand_page = await http.GetAsync("http://en.wikipedia.org/wiki/Special:Random");
                    page_str = await rand_page.Content.ReadAsStringAsync();
                    unvalid_seq = page_str.IndexOf("\\u");
                    idx2 = page_str.IndexOf(" - Wikipedia");
                } while (idx2 == -1 || unvalid_seq != -1);

                idx1 = page_str.IndexOf("<title>") + "<title>".Length;
                title = page_str.Substring(idx1, idx2 - idx1).Replace(' ', '_');
                var response = await http.GetAsync("https://en.wikipedia.org/w/api.php?format=json&action=query&prop=extracts&exintro=&explaintext=&exsentences=2&titles=" + title);
                string result = await response.Content.ReadAsStringAsync();
                idx1 = result.IndexOf("extract\":\"") + "extract\":\"".Length;
                idx2 = result.IndexOf("\"}}}}");
                string content = result.Substring(idx1, idx2 - idx1);

                while (content.IndexOf(" (") != -1)
                {
                    idx1 = content.IndexOf(" (");
                    if (content.IndexOf("))") != -1)
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

                while (content.IndexOf("\\n") != -1)
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
            

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private async Task GetInspiringQuote()
        {
            try
            {
                QuoteObject my_quote = await QuoteMapProxy.GetQuote();
                await GUIOutput(my_quote.contents.quotes[0].quote, true);
            }
            catch
            {
                await GUIOutput("I'm sorry, I couldn't retrieve the informations from the web.", true);
            }

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private async Task GetWeatherInfo()
        {
            //Get weather infos
            try
            {
                var position = await LocationManager.GetPosition();

                RootObject my_weather = await OpenWeatherMapProxy.GetWeather(position.Coordinate.Latitude, position.Coordinate.Longitude);

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.SwitchToWeather();
                                            this_main_page.WeatherOut(my_weather);
                                        });

                await GUIOutput("The weather forecast for " + my_weather.name + " are: " + my_weather.weather[0].description +
                                " with temperature between " + ((int)my_weather.main.temp_min).ToString() + " °C and " + ((int)my_weather.main.temp_max).ToString() + " °C. " +
                                " The humidity rate is equal to " + ((int)my_weather.main.humidity).ToString() + " % and the wind speed is " + ((int)my_weather.wind.speed).ToString() + " meters per second.", true);

                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.SwitchToDefault();
                                        });
            }
            catch
            {
                await GUIOutput("I'm sorry, I couldn't retrieve the informations from the web.", true);
            }

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private async Task OpenStopWatch()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.SwitchToStopWatch();
                                        });

            state = AssistantState.BACKGROUND_STOPWATCH;
        }

        private async Task CloseStopWatch()
        {
            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.SwitchToDefault();
                                        });

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private async Task FindRecipe()
        {
            await GUIOutput("What do you want to cook?", true);
            SpeechRecognitionResult food = await task_recognizer.RecognizeAsync();

            await GUIOutput("Searching a recipe for " + food.Text, false);

            try
            {
                //Get recipe from web
                var http = new HttpClient();
                var obj = await http.GetAsync("https://api.edamam.com/search?q=" + food.Text + "&app_id=b4a3fd65&app_key=64c81e2e1ae114c948f814fc9c31041f&from=0&to=1");
                string str = await obj.Content.ReadAsStringAsync();
                int start = str.IndexOf("\"ingredientLines\" : [ \"") + "\"ingredientLines\" : [ \"".Length;
                int end = str.IndexOf("\"ingredients\"") - 11;
                string ingredients = str.Substring(start, end - start);
                ingredients = ingredients.Replace("\", \"", ";");
                var ingredients_list = ingredients.Split(';');

                start = str.IndexOf("\"label\" : \"") + "\"label\" : \"".Length;
                end = str.IndexOf("\"image\"") - 9;
                string recipe_name = str.Substring(start, end - start);

                start = str.IndexOf("\"image\" : \"") + "\"image\" : \"".Length;
                end = str.IndexOf("\"source\"") - 9;
                string recipe_image = str.Substring(start, end - start);

                //Switch grid
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.SwitchToRecipes();
                                            this_main_page.SetRecipeName(recipe_name);
                                            this_main_page.SetRecipeImg(recipe_image);
                                            this_main_page.SetIngredients(ingredients_list);
                                        });

                //Wait until the user click the exit button
                remain_in_recipe_view = true;
                while (remain_in_recipe_view);

                //Switch back to default grid
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                        () =>
                                        {
                                            this_main_page.SwitchToDefault();
                                        });
            }
            catch
            {
                await GUIOutput("I'm sorry, I couldn't retrieve the informations from the web.", true);
            }

            state = AssistantState.BACKGROUND_LISTEN;
        }

        //Return to default grid from repice function
        public void RecipeReturnToDefault()
        {
            remain_in_recipe_view = false;
        }

        //Slider volume control function
        public void SetMediaPlayerVolume(double val)
        {
            player.Volume = val / 100.0;
        }

        //Play button control function
        public void SetMediaPlayerPlay()
        {
            if (player.CurrentState != MediaPlayerState.Playing)
                player.Play();
        }

        //Close button control function
        public async Task CloseMediaPlayer()
        {
            player.Pause();
            songIndex = 0;

            await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal,
                                   () =>
                                   {
                                       this_main_page.SwitchToDefault();
                                   });

            state = AssistantState.BACKGROUND_LISTEN;
        }

        //Stop button control function
        public void SetMediaPlayerStop()
        {
            if (player.CurrentState == MediaPlayerState.Playing)
                player.Pause();
        }

        //Next button control function
        public async Task SetMediaPlayerNext()
        {
            player.Pause();
            if (songIndex + 1 >= songList.Count)
                songIndex = 0;
            else
                songIndex++;
            await PlayMusic();
        }

        //Previous button control function
        public async Task SetMediaPlayerPrevious()
        {
            player.Pause();
            if (songIndex - 1 < 0)
                songIndex = songList.Count - 1;
            else
                songIndex--;
            await PlayMusic();
        }

        //Metadata extraction function
        private KeyValuePair<string, string> ExtractMetadata(int idx)
        {
            string title;
            string artist;

            int i = songList[idx].LastIndexOf('.');
            string temp = songList[idx].Remove(i);
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
            

            KeyValuePair<string, string> result = new KeyValuePair<string, string>(title, artist);

            return result;
        }


        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            if (rootFrame == null)
            {
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                Window.Current.Activate();

                //Get Main page
                this_main_page = (MainPage)rootFrame.Content;
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
