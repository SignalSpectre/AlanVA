using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.Media.SpeechRecognition;
using Windows.Storage;
using Windows.Media.Playback;
using System.Net;
using System.Threading.Tasks;
using AssistantSpeechSynthesis;
using System.Text;

namespace VocalAssistant
{
    public struct Remainder
    {
        public int day;
        public int month;
        public string details;
    }

    enum AssistantState { BACKGROUND_LISTEN, LISTENING, BACKGROUND_PLAYING_SONG, LISTENING_PLAYING_SONG };

    /// <summary>
    /// Fornisci un comportamento specifico dell'applicazione in supplemento alla classe Application predefinita.
    /// </summary>
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

            //DEBUG
            Debug.WriteLine("Background_recognizer language is: " + background_recognizer.CurrentLanguage.NativeName.ToString());

            while (this_main_page == null) ;

            //this_main_page.Output("Ciao.");

            // If compilation successful, start continuous recognition session
            if (backup_compilation_result.Status == SpeechRecognitionResultStatus.Success)
                await background_recognizer.ContinuousRecognitionSession.StartAsync();

            if (task_compilation_result.Status == SpeechRecognitionResultStatus.Success)
                Debug.WriteLine("Compilation result: Success.");

            // Set event handlers
            background_recognizer.ContinuousRecognitionSession.ResultGenerated += ContinuousRecognitionSession_ResultGenerated;
            background_recognizer.StateChanged += background_recognizer_StateChanged;
            player.MediaEnded += player_MediaEnded;
        }

        private void background_recognizer_StateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
        {
            Debug.WriteLine("State changed - " + args.State.ToString());
            if (args.State.ToString() == "Idle" && (state == AssistantState.BACKGROUND_LISTEN || state == AssistantState.BACKGROUND_PLAYING_SONG))
                background_recognizer.ContinuousRecognitionSession.StartAsync();
        }

        private void ContinuousRecognitionSession_ResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
        {
            // Execute tasks acoording to recognized speech

            Debug.WriteLine("Result recognized: " + args.Result.Text);

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
                player.Volume = 0.1;
            }

            // Stop continuous recognition session
            await background_recognizer.ContinuousRecognitionSession.StopAsync();

            await speaker.SayAsync("I'm listening.");
            //this_main_page.Output("I'm listening.");

            //listen for user command
            SpeechRecognitionResult command = await task_recognizer.RecognizeAsync();

            //DEBUG
            Debug.WriteLine("DEBUG: you said:" + command.Text);

            //select task list by state
            if (state == AssistantState.LISTENING)
                await NormalTaskList(command.Text);
            else if (state == AssistantState.LISTENING_PLAYING_SONG) 
            {
                await SongTaskList(command.Text);
                player.Volume = player_volume;
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
                await speaker.SayAsync($"It's {now.Hour} {now.Minute}.");

                state = AssistantState.BACKGROUND_LISTEN;
            }
            else if (command == "tell me a joke")
                TellJokes();
            else if (command == "play some music")
                await PlayMusic();
            else if (command == "take a reminder" || command == "make a reminder")
                await MakeRemainder();
            else if (command == "any plans for today" || command == "any plans today")
                await SearchRemainder();
            else if (command == "tell me something interesting")
                await RandomOnWiki();
            else
            {
                await speaker.SayAsync("I'm sorry, I'm afraid I can't do that.");

                state = AssistantState.BACKGROUND_LISTEN;
            }

            return;
        }

        private void TellJokes()
        {
            speaker.SayAsync("A neutron walks into a bar and asks: how much for a beer?, bartender responds: for you? no charge.");

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private async Task PlayMusic()
        {
            //Read the media file
            StorageFile song = await Package.Current.InstalledLocation.GetFileAsync(@"Music\" + songList[songIndex]);


            //Play it
            player.AutoPlay = false;
            player.SetFileSource(song);
            player.Play();
            player.Volume = 0.35;

            //Change the state of the assistant
            state = AssistantState.BACKGROUND_PLAYING_SONG;
        }

        private async Task SongTaskList(string command)
        {
            //TODO: scrivere il codice per controllare la riproduzione della canzone

            if (command == "close")
            {
                player.Pause();
                await speaker.SayAsync("Music player closed.");
                songIndex = 0;
                state = AssistantState.BACKGROUND_LISTEN;
            }
            else if (command == "stop")
            {
                if (player.CurrentState == MediaPlayerState.Paused)
                    await speaker.SayAsync("I'm sorry, but the music is already paused.");
                else
                    player.Pause();
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
            else if (command == "play")
            {
                if (player.CurrentState == MediaPlayerState.Playing)
                    await speaker.SayAsync("I'm sorry, but the music is already playing.");
                else
                    player.Play();
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
            else if (command == "volume up")
            {
                if (player_volume <= 0.8)
                    player_volume += 0.2;
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
            else if (command == "volume down")
            {
                if (player_volume >= 0.2)
                    player_volume -= 0.2;
                state = AssistantState.BACKGROUND_PLAYING_SONG;
            }
            else if (command == "next")
            {
                player.Pause();
                if (songIndex + 1 >= songList.Count)
                    songIndex = 0;
                else
                    songIndex++;
                PlayMusic();
            }
            else if (command == "previous")
            {
                player.Pause();
                if (songIndex - 1 < 0)
                    songIndex = songList.Count - 1;
                else
                    songIndex--;
                PlayMusic();
            }
            else
            {
                await speaker.SayAsync("I'm sorry, I'm afraid I can't do that.");
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

            if (compilation_status.Status != SpeechRecognitionResultStatus.Success)
                Debug.WriteLine("Failed to compile date constraint! \n");


            await speaker.SayAsync("For which day?");

            SpeechRecognitionResult date;
            SpeechRecognitionResult details;
            bool finished = false;
            do
            {
                date = await remainder_recon.RecognizeAsync();
                if (date.Text.Length == 0)
                    await speaker.SayAsync("Please, repeat what you said.");
                else
                {
                    await speaker.SayAsync($"You said: {date.Text}. Is it correct?");
                    SpeechRecognitionResult ack = await task_recognizer.RecognizeAsync();
                    Debug.WriteLine($"DEBUG: ack: {ack.Text} \n");
                    if (ack.Text == "yes")
                        finished = true;
                    else
                        await speaker.SayAsync("Please, repeat the date.");
                }
            } while (!finished);

            Debug.WriteLine("Data: " + date.Text);

            await speaker.SayAsync("What should I remember?");
            finished = false;
            int cnt = 0;
            do
            {
                details = await task_recognizer.RecognizeAsync();
                if (details.Text.Length == 0)
                {
                    cnt++;
                    if (cnt < 3)
                        await speaker.SayAsync("Please, repeat what you said.");
                }
                else
                {
                    await speaker.SayAsync($"You said: {details.Text}. Is it correct?");
                    SpeechRecognitionResult ack = await task_recognizer.RecognizeAsync();
                    if (ack.Text == "yes")
                        finished = true;
                    else
                    {
                        cnt++;
                        if (cnt < 3)
                            await speaker.SayAsync("Please, repeat the remainder.");
                    }
                }
            } while (!finished && cnt < 3);

            if (cnt < 3)
            {
                //add the reaminder to the list
                remainders.Add(CreateRemainder(date.Text, details.Text));

                await speaker.SayAsync("Remainder saved.");
            }
            else
                await speaker.SayAsync("Sorry, I couldn't complete the task.");

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private Remainder CreateRemainder(string date, string details)
        {
            string day, month;
            Remainder output;

            day = date.Split(' ')[1];
            month = date.Split(' ')[0];

            Debug.WriteLine($"DEBUG: day:{day}, month:{month} \n");
            Debug.WriteLine($"DEBUG: details: {details} \n");

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

            Debug.WriteLine($"DEBUG: int day: {output.day}  int month: {output.month}");

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
                    await speaker.SayAsync($"Remainder {rem}: {remainders[i].details}.");
                    Debug.WriteLine($"DEBUG: counter: {rem} \n");
                }
            }

            if (rem == 0)
                await speaker.SayAsync("You have no remainders for today.");

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private void player_MediaEnded(MediaPlayer sender, Object args)
        {
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
            string webPage = await GetStringFromWeb("http://en.wikipedia.org/wiki/Special:Random");
            //Debug.WriteLine(webPage + "\n");

            //Extrapolate first paragraph
            int idx1 = webPage.IndexOf("<div class=\"mw-parser-output\">");
            string temp = webPage.Substring(idx1);
            int idx2 = webPage.IndexOf("</p>") + "</p>".Length;
            temp = temp.Substring(0, idx2 - idx1);
            idx1 = temp.IndexOf("<p>");
            temp = temp.Substring(idx1);

            //Filter out HTML code
            int c = 0;
            var temp_builder = new StringBuilder();
            while (c < temp.Length)
            {
                if (temp[c] == '&')
                {
                    int i = c;
                    bool found = false;
                    while (!found && i < temp.Length)
                    {
                        i++;

                        if (temp[i] == ';')
                            found = true;
                    }

                    if (found)
                    {
                        //extract substring
                        string s = temp.Substring(c, (i - c) + 1);

                        if (s == "&#91;")
                        {
                            temp_builder.Append('[');
                            c = i + 1;
                        }
                        else if (s == "&#93;")
                        {
                            temp_builder.Append(']');
                            c = i + 1;
                        }
                    }
                }
                temp_builder.Append(temp[c]);
                c++;
            }

            temp = temp_builder.ToString();

            var bracket_stack = new Stack<string>();
            string output;
            var builder = new StringBuilder();
            int points = 0;
            for (int i = 0; i < temp.Length; i++)
            {
                if (temp[i] == '<' || temp[i] == '(' || temp[i] == '[' || temp[i] == '{')
                    bracket_stack.Push(temp[i].ToString());
                else if (temp[i] == '>' || temp[i] == ')' || temp[i] == ']' || temp[i] == '}')
                    bracket_stack.Pop();
                else if (bracket_stack.Count == 0)
                    builder.Append(temp[i]);

                if (temp[i] == '.')
                    points++;

                if (points == 2)
                    break;
            }
            output = builder.ToString();

            // TODO: eventualmente fare il filtraggi dei punti (massimo due punti per frase) in un altro ciclo

            await speaker.SayAsync(output);

            state = AssistantState.BACKGROUND_LISTEN;
        }

        private async Task<string> GetStringFromWeb(string url)
        {
            HttpWebRequest myReq = (HttpWebRequest)WebRequest.Create(url);
            myReq.Method = "GET";
            WebResponse myResp = await myReq.GetResponseAsync();
            StreamReader sr = new StreamReader(myResp.GetResponseStream(), System.Text.Encoding.UTF8);
            return sr.ReadToEnd();
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
