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


        /// <summary>
        /// Inizializza l'oggetto Application singleton. Si tratta della prima riga del codice creato
        /// creato e, come tale, corrisponde all'equivalente logico di main() o WinMain().
        /// </summary>
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

            await GUIOutput("Hi, I'm Lawrence. How can I help you?", true);

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
                GUIOutput("Hi, I'm Lawrence. How can I help you?", false);
        }

        private void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // Execute tasks acoording to recognized speech
            if (args.Result.Text == "hey lawrence")
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
                    await GUIOutput("I'm sorry, I'm afraid I can't do that.", true);
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
                await GUIOutput($"It's {now.Hour}:{now.Minute}.", true);

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
            else
            {
                await GUIOutput("I'm sorry, I'm afraid I can't do that.", true);

                state = AssistantState.BACKGROUND_LISTEN;
            }

            return;
        }

        private async Task TellJokes()
        {
            var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
            var content = await http.GetAsync("https://icanhazdadjoke.com/");
            var joke = await content.Content.ReadAsStringAsync();

            await GUIOutput(joke, true);

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
            //TODO: scrivere il codice per controllare la riproduzione della canzone

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
                    await GUIOutput("I'm sorry, but the music is already paused.", true);
                else
                    player.Pause();
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
            else if (command == "play")
            {
                if (player.CurrentState == MediaPlayerState.Playing)
                    await GUIOutput("I'm sorry, but the music is already playing.", true);
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
                await GUIOutput("I'm sorry, I'm afraid I can't do that.", true);
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
                await GUIOutput("Sorry, I couldn't complete the task.", true);

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
                int idx1, idx2;
                do
                {
                    var rand_page = await http.GetAsync("http://en.wikipedia.org/wiki/Special:Random");
                    page_str = await rand_page.Content.ReadAsStringAsync();
                    idx2 = page_str.IndexOf(" - Wikipedia");
                } while (idx2 == -1);

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

                while (content.IndexOf("~") != -1)
                {
                    idx1 = content.IndexOf("~");
                    idx2 = content.IndexOf("\\u2014") + 6;
                    content = content.Remove(idx1, idx2 - idx1);
                }

                content = content.Replace(" \\u2014", ",");
                content = content.Replace("  ", " ");

                await GUIOutput(content, true);
            }
            catch
            {
                await GUIOutput("I'm sorry, I wasn't able to find any information.", true);
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
                await GUIOutput("I'm sorry, I couldn't retrieve any quote.", true);
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
                await GUIOutput("I'm sorry, I was unable to retrieve weather informations.", true);
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

        /// <summary>
        /// Richiamato quando l'applicazione viene avviata normalmente dall'utente. All'avvio dell'applicazione
        /// verranno usati altri punti di ingresso per aprire un file specifico.
        /// </summary>
        /// <param name="e">Dettagli sulla richiesta e sul processo di avvio.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Non ripetere l'inizializzazione dell'applicazione se la finestra già dispone di contenuto,
            // assicurarsi solo che la finestra sia attiva
            if (rootFrame == null)
            {
                // Creare un frame che agisca da contesto di navigazione e passare alla prima pagina
                rootFrame = new Frame();

                rootFrame.NavigationFailed += OnNavigationFailed;

                if (e.PreviousExecutionState == ApplicationExecutionState.Terminated)
                {
                    //TODO: caricare lo stato dall'applicazione sospesa in precedenza
                }

                // Posizionare il frame nella finestra corrente
                Window.Current.Content = rootFrame;
            }

            if (e.PrelaunchActivated == false)
            {
                if (rootFrame.Content == null)
                {
                    // Quando lo stack di esplorazione non viene ripristinato, passare alla prima pagina
                    // e configurare la nuova pagina passando le informazioni richieste come parametro
                    // parametro
                    rootFrame.Navigate(typeof(MainPage), e.Arguments);
                }
                // Assicurarsi che la finestra corrente sia attiva
                Window.Current.Activate();

                //Get Main page
                this_main_page = (MainPage)rootFrame.Content;
            }
        }

        /// <summary>
        /// Chiamato quando la navigazione a una determinata pagina ha esito negativo
        /// </summary>
        /// <param name="sender">Frame la cui navigazione non è riuscita</param>
        /// <param name="e">Dettagli sull'errore di navigazione.</param>
        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        /// <summary>
        /// Richiamato quando l'esecuzione dell'applicazione viene sospesa. Lo stato dell'applicazione viene salvato
        /// senza che sia noto se l'applicazione verrà terminata o ripresa con il contenuto
        /// della memoria ancora integro.
        /// </summary>
        /// <param name="sender">Origine della richiesta di sospensione.</param>
        /// <param name="e">Dettagli relativi alla richiesta di sospensione.</param>
        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            //TODO: salvare lo stato dell'applicazione e arrestare eventuali attività eseguite in background
            deferral.Complete();
        }
    }
}
